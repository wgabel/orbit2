using System.Text.Json;
using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Models;
using Orbit.Audio.Analysis;
using Orbit.Audio.Compilation;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

// Produce a graph via ordinary commands, not hand-written Faust fixture code.
// A parent LFO fans out through named rack inputs to pitch and gain inside a drone.
// Offline tests verify the complete compiled path through the child output rack.
if (args.Length != 1)
    throw new ArgumentException("Pass an output directory for the generated probe.");
Directory.CreateDirectory(args[0]);
var registry = BuiltInNodes.CreateRegistry();
registry.Register(CompositeNode.NodeType, id => new CompositeNode(id, "Drone",
    [new ExposedPort(Guid.NewGuid(), "Pitch", SignalType.Modulation),
     new ExposedPort(Guid.NewGuid(), "Gain", SignalType.Modulation)],
    [new ExposedPort(Guid.NewGuid(), "Audio", SignalType.Audio)]));
using var session = new PatchSession(registry);
var root = session.GraphId;
var lfo = Add(root, OscillatorGenerator.NodeType);
var drone = Add(root, CompositeNode.NodeType);
var master = Add(root, MasterOutput.NodeType);
var composite = drone.Composite!;
var child = composite.Graph.Id;
var oscillator = Add(child, OscillatorGenerator.NodeType);
var gain = Add(child, GainProcessor.NodeType);
session.Execute(new SetParameterValueCommand(root, new ParameterAddress(lfo.Id, lfo.Parameters[0].Id), 0.5));
session.Execute(new SetParameterValueCommand(child, new ParameterAddress(gain.Id, gain.Parameters[0].Id), 0.1));
Connect(root, lfo.Id, lfo.Outputs[0].Id, drone.Id, drone.Inputs[0].Id, 100);
Connect(root, lfo.Id, lfo.Outputs[0].Id, drone.Id, drone.Inputs[1].Id, 0.05);
Connect(child, composite.InputRackId, drone.Inputs[0].Id, oscillator.Id, oscillator.Parameters[0].ModulationPortId!.Value);
Connect(child, composite.InputRackId, drone.Inputs[1].Id, gain.Id, gain.Parameters[0].ModulationPortId!.Value);
Connect(child, oscillator.Id, oscillator.Outputs[0].Id, gain.Id, gain.Inputs[0].Id);
Connect(child, gain.Id, gain.Outputs[0].Id, composite.OutputRackId, drone.Outputs[0].Id);
Connect(root, drone.Id, drone.Outputs[0].Id, master.Id, master.Inputs[0].Id);
var program = FaustGraphCompiler.CreateBuiltIn().Compile(session.Snapshot);
var frequency = program.Controls.Single(control => control.Address == new ParameterAddress(oscillator.Id,
    oscillator.Parameters.Single(parameter => parameter.Definition.Key == "frequency").Id));
var amplitude = program.Controls.Single(control => control.Address == new ParameterAddress(oscillator.Id,
    oscillator.Parameters.Single(parameter => parameter.Definition.Key == "amplitude").Id));
File.WriteAllText(Path.Combine(args[0], "program.dsp"), program.Source);
var observed = FaustGraphCompiler.CreateBuiltIn().Compile(session.Snapshot, SignalTapManifest.MaxAuxiliaryChannels);
File.WriteAllText(Path.Combine(args[0], "observation.json"), JsonSerializer.Serialize(new
{
    source = observed.Source, values = observed.InitialControlValues(), channels = observed.SignalTaps.ChannelCount,
    drone = observed.SignalTaps.Channels[new Orbit.Application.Models.SignalObservationAddress(root, root,
        drone.Id, drone.Outputs[0].Id, PortDirection.Output)],
    gain = observed.SignalTaps.Channels[new Orbit.Application.Models.SignalObservationAddress(root, child,
        gain.Id, gain.Outputs[0].Id, PortDirection.Output)],
    carrier = observed.SignalTaps.Channels[new Orbit.Application.Models.SignalObservationAddress(root, child,
        oscillator.Id, oscillator.Outputs[0].Id, PortDirection.Output)],
    pitchRack = observed.SignalTaps.Channels[new Orbit.Application.Models.SignalObservationAddress(root, child,
        composite.InputRackId, drone.Inputs[0].Id, PortDirection.Output)],
    lfo = observed.SignalTaps.Channels[new Orbit.Application.Models.SignalObservationAddress(root, root,
        lfo.Id, lfo.Outputs[0].Id, PortDirection.Output)],
    mute = observed.AuditionControls.Single(control => control.NodeId == drone.Id && control.Kind == NodeAuditionKind.Mute).Name,
    bypass = observed.AuditionControls.Single(control => control.NodeId == gain.Id && control.Kind == NodeAuditionKind.Bypass).Name
}));
File.WriteAllText(Path.Combine(args[0], "controls.json"), JsonSerializer.Serialize(new
{
    values = program.Controls.ToDictionary(control => control.Name, control => control.InitialValue),
    frequency = frequency.Name,
    frequencyMeter = frequency.MeterName,
    amplitude = amplitude.Name
}));

// Inspect a leaf and its containing composite using the same real graph.
File.WriteAllText(Path.Combine(args[0], "mute.json"), JsonSerializer.Serialize(new
{
    source = program.Source, values = program.InitialControlValues(),
    composite = program.AuditionControls.Single(control => control.NodeId == drone.Id && control.Kind == NodeAuditionKind.Mute).Name,
    leaf = program.AuditionControls.Single(control => control.NodeId == gain.Id && control.Kind == NodeAuditionKind.Mute).Name
}));

// One root and one child tempo LFO share the same external beat signal. A
// summed boundary lets the offline browser compare their phase to an oracle.
var tempoRegistry = BuiltInNodes.CreateRegistry();
tempoRegistry.Register(CompositeNode.NodeType, id => new CompositeNode(id, "Tempo probe",
    [new ExposedPort(Guid.NewGuid(), "External", SignalType.Audio)],
    [new ExposedPort(Guid.NewGuid(), "Mixed", SignalType.Audio, FanIn: FanInPolicy.Sum)]));
using var tempoSession = new PatchSession(tempoRegistry);
var tempoRoot = tempoSession.GraphId;
var outerLfo = TempoAdd(tempoRoot, OscillatorGenerator.NodeType);
var tempoDrone = TempoAdd(tempoRoot, CompositeNode.NodeType);
var tempoMaster = TempoAdd(tempoRoot, MasterOutput.NodeType);
var tempoChild = tempoDrone.Composite!.Graph.Id;
var innerLfo = TempoAdd(tempoChild, OscillatorGenerator.NodeType);
foreach (var (graph, node) in new[] { (tempoRoot, outerLfo), (tempoChild, innerLfo) })
    tempoSession.Execute(new SetParameterValueCommand(graph,
        new ParameterAddress(node.Id, node.Parameters.Single(p => p.Definition.Key == "timing").Id),
        (int)OscillatorTimingMode.Synchronized));
TempoConnect(tempoRoot, outerLfo.Id, outerLfo.Outputs[0].Id, tempoDrone.Id, tempoDrone.Inputs[0].Id);
TempoConnect(tempoChild, tempoDrone.Composite.InputRackId, tempoDrone.Inputs[0].Id,
    tempoDrone.Composite.OutputRackId, tempoDrone.Outputs[0].Id, 0.5);
TempoConnect(tempoChild, innerLfo.Id, innerLfo.Outputs[0].Id,
    tempoDrone.Composite.OutputRackId, tempoDrone.Outputs[0].Id, 0.5);
TempoConnect(tempoRoot, tempoDrone.Id, tempoDrone.Outputs[0].Id, tempoMaster.Id, tempoMaster.Inputs[0].Id);
var tempoProgram = FaustGraphCompiler.CreateBuiltIn().Compile(tempoSession.Snapshot);
File.WriteAllText(Path.Combine(args[0], "tempo.dsp"), tempoProgram.Source);
File.WriteAllText(Path.Combine(args[0], "tempo-controls.json"), JsonSerializer.Serialize(
    tempoProgram.Controls.ToDictionary(control => control.Name, control => control.InitialValue)));

// Export every waveform using the same real domain node and compiler. These
// programs are rendered by the browser's pinned Faust compiler at LFO/audio rates.
using var waveformSession = new PatchSession(BuiltInNodes.CreateRegistry());
var waveformGraph = waveformSession.GraphId;
var waveNode = waveformSession.Execute(new AddNodeCommand(waveformGraph, OscillatorGenerator.NodeType))
    .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
var waveMaster = waveformSession.Execute(new AddNodeCommand(waveformGraph, MasterOutput.NodeType))
    .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
waveformSession.Execute(new ConnectNodesCommand(waveformGraph, new PortAddress(waveNode.Id, waveNode.Outputs[0].Id),
    new PortAddress(waveMaster.Id, waveMaster.Inputs[0].Id)));
var waveformAddress = new ParameterAddress(waveNode.Id,
    waveNode.Parameters.Single(parameter => parameter.Definition.Key == "waveform").Id);
var timingAddress = new ParameterAddress(waveNode.Id,
    waveNode.Parameters.Single(parameter => parameter.Definition.Key == "timing").Id);
var waveformProbes = (from mode in Enum.GetValues<OscillatorTimingMode>()
    from waveform in Enum.GetValues<OscillatorWaveform>()
    select (mode, waveform)).Select(pair =>
{
    var (mode, waveform) = pair;
    waveformSession.Execute(new SetParameterValueCommand(waveformGraph, timingAddress, (int)mode));
    waveformSession.Execute(new SetParameterValueCommand(waveformGraph, waveformAddress, (int)waveform));
    var compiled = FaustGraphCompiler.CreateBuiltIn().Compile(waveformSession.Snapshot);
    string ControlName(string key) => compiled.Controls.Single(control => control.Address.ParameterId ==
        waveNode.Parameters.Single(parameter => parameter.Definition.Key == key).Id).Name;
    return new
    {
        waveform = waveform.ToString(),
        timing = mode.ToString(),
        source = compiled.Source,
        values = compiled.Controls.ToDictionary(control => control.Name, control => control.InitialValue),
        rateControl = ControlName(mode == OscillatorTimingMode.Free ? "frequency" : "division"),
        phase = ControlName("phase"),
        pulseWidth = waveform == OscillatorWaveform.Square ? ControlName(OscillatorGenerator.PulseWidthKey) : null
    };
}).ToArray();
File.WriteAllText(Path.Combine(args[0], "waveforms.json"), JsonSerializer.Serialize(waveformProbes));

using var noiseSession = new PatchSession(BuiltInNodes.CreateRegistry());
var noiseGraph = noiseSession.GraphId;
var noise = noiseSession.Execute(new AddNodeCommand(noiseGraph, NoiseGenerator.NodeType))
    .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
var noiseMaster = noiseSession.Execute(new AddNodeCommand(noiseGraph, MasterOutput.NodeType))
    .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
noiseSession.Execute(new ConnectNodesCommand(noiseGraph, new PortAddress(noise.Id, noise.Outputs[0].Id),
    new PortAddress(noiseMaster.Id, noiseMaster.Inputs[0].Id)));
var noiseColor = new ParameterAddress(noise.Id, noise.Parameters.Single(p => p.Definition.Key == "color").Id);
var noiseProbes = Enum.GetValues<NoiseColor>().Select(color =>
{
    noiseSession.Execute(new SetParameterValueCommand(noiseGraph, noiseColor, (int)color));
    var compiled = FaustGraphCompiler.CreateBuiltIn().Compile(noiseSession.Snapshot);
    return new { color = color.ToString(), source = compiled.Source, amplitude = compiled.Controls.Single().Name };
}).ToArray();
File.WriteAllText(Path.Combine(args[0], "noise.json"), JsonSerializer.Serialize(noiseProbes));

using var layeredSession = new PatchSession(BuiltInNodes.CreateEditorRegistry());
var layeredRoot = layeredSession.GraphId;
var layered = layeredSession.Execute(new AddNodeCommand(layeredRoot, DroneTemplate.LayeredPresetKey))
    .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
var layeredMaster = layeredSession.Execute(new AddNodeCommand(layeredRoot, MasterOutput.NodeType))
    .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
layeredSession.Execute(new ConnectNodesCommand(layeredRoot, new PortAddress(layered.Id, layered.Outputs[0].Id),
    new PortAddress(layeredMaster.Id, layeredMaster.Inputs[0].Id)));
var layeredProgram = FaustGraphCompiler.CreateBuiltIn().Compile(layeredSession.Snapshot);
File.WriteAllText(Path.Combine(args[0], "layered.json"), JsonSerializer.Serialize(new
{
    source = layeredProgram.Source,
    values = layeredProgram.Controls.ToDictionary(control => control.Name, control => control.InitialValue)
}));

using var filterSession = new PatchSession(BuiltInNodes.CreateRegistry());
var filterRoot = filterSession.GraphId;
var carrier = FilterAdd(OscillatorGenerator.NodeType);
var modulation = FilterAdd(OscillatorGenerator.NodeType);
var filter = FilterAdd(FilterProcessor.NodeType);
var filterMaster = FilterAdd(MasterOutput.NodeType);
ParameterAddress Address(GraphNodeSnapshot node, string key) => new(node.Id, node.Parameters.Single(p => p.Definition.Key == key).Id);
void FilterSet(GraphNodeSnapshot node, string key, double value) => filterSession.Execute(new SetParameterValueCommand(filterRoot, Address(node, key), value));
void FilterConnect(GraphNodeSnapshot source, GraphNodeSnapshot target, string key, double amount) => filterSession.Execute(new ConnectNodesCommand(filterRoot,
    new PortAddress(source.Id, source.Outputs.Single().Id), new PortAddress(target.Id, target.Inputs.Single(p => p.Key == key).Id), amount));
GraphNodeSnapshot FilterAdd(string type) => filterSession.Execute(new AddNodeCommand(filterRoot, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
FilterSet(carrier, "amplitude", 0.1);
FilterSet(modulation, "amplitude", 0);
FilterSet(modulation, "frequency", 200);
FilterConnect(carrier, filter, "audio-in", 1);
FilterConnect(modulation, filter, "cutoff-modulation-in", 19000);
FilterConnect(modulation, filter, "resonance-modulation-in", 8);
FilterConnect(filter, filterMaster, "audio-in", 1);
var filterProbes = Enum.GetValues<FilterMode>().Select(mode =>
{
    FilterSet(filter, "mode", (int)mode);
    var compiled = FaustGraphCompiler.CreateBuiltIn().Compile(filterSession.Snapshot);
    string Name(GraphNodeSnapshot node, string key) => compiled.Controls.Single(c => c.Address == Address(node, key)).Name;
    return new { mode = mode.ToString(), source = compiled.Source,
        values = compiled.Controls.ToDictionary(c => c.Name, c => c.InitialValue),
        frequency = Name(carrier, "frequency"), cutoff = Name(filter, "cutoff"), resonance = Name(filter, "resonance"), modulation = Name(modulation, "amplitude"),
        predictions = new[] { 44100, 48000 }.Select(rate =>
        {
            var response = new ProcessorResponseParameters((ProcessorResponseKind)(int)mode, 1000, 0.707);
            double At(double frequency) => StateVariableResponse.Magnitude(response, rate, frequency);
            return new { rate, low = At(100), center = At(1000), high = At(8000),
                resonant = StateVariableResponse.Magnitude(response with { Q = 4 }, rate, 1000),
                nearNyquist = StateVariableResponse.Magnitude(response with { Frequency = 20000 }, rate, 19000) };
        }).ToArray() };
}).ToArray();
File.WriteAllText(Path.Combine(args[0], "filters.json"), JsonSerializer.Serialize(filterProbes));

// Reuse the same carrier and modulation source, replacing only the processor.
filterSession.Execute(new RemoveNodeCommand(filterRoot, filter.Id));
var eq = FilterAdd(ParametricEqProcessor.NodeType);
FilterConnect(carrier, eq, "audio-in", 1);
FilterConnect(modulation, eq, "frequency-modulation-in", 19000);
FilterConnect(modulation, eq, "gain-modulation-in", 18);
FilterConnect(modulation, eq, "q-modulation-in", 9);
FilterConnect(eq, filterMaster, "audio-in", 1);
var eqProgram = FaustGraphCompiler.CreateBuiltIn().Compile(filterSession.Snapshot);
string EqControl(GraphNodeSnapshot node, string key) => eqProgram.Controls.Single(c => c.Address == Address(node, key)).Name;
File.WriteAllText(Path.Combine(args[0], "eq.json"), JsonSerializer.Serialize(new
{
    source = eqProgram.Source, values = eqProgram.Controls.ToDictionary(c => c.Name, c => c.InitialValue),
    carrier = EqControl(carrier, "frequency"), frequency = EqControl(eq, "frequency"),
    gain = EqControl(eq, "gain"), q = EqControl(eq, "q"), modulation = EqControl(modulation, "amplitude"),
    predictions = new[] { 44100, 48000 }.Select(rate =>
    {
        var response = new ProcessorResponseParameters(ProcessorResponseKind.Peaking, 1000, 1, 12);
        double At(double frequency) => StateVariableResponse.Decibels(response, rate, frequency);
        return new { rate, boost = At(1000), low = At(20), high = At(12000),
            flat = StateVariableResponse.Decibels(response with { GainDb = 0 }, rate, 1000),
            broad = StateVariableResponse.Decibels(response with { Q = 0.5 }, rate, 1500),
            narrow = StateVariableResponse.Decibels(response with { Q = 8 }, rate, 1500),
            cut = StateVariableResponse.Decibels(response with { Q = 8, GainDb = -12 }, rate, 1000),
            moved = StateVariableResponse.Decibels(response with { Frequency = 2000, Q = 8, GainDb = -12 }, rate, 2000) };
    }).ToArray()
}));

filterSession.Execute(new RemoveNodeCommand(filterRoot, eq.Id));
var delay = FilterAdd(DelayProcessor.NodeType);
FilterSet(carrier, "frequency", 100);
FilterSet(carrier, "amplitude", 0);
FilterSet(delay, "time", 0.25);
FilterSet(delay, "feedback", 0.5);
FilterSet(delay, "mix", 1);
FilterConnect(carrier, delay, "audio-in", 1);
FilterConnect(modulation, delay, "time-modulation-in", 0.1);
FilterConnect(modulation, delay, "feedback-modulation-in", 0.5);
FilterConnect(modulation, delay, "mix-modulation-in", 0.5);
FilterConnect(delay, filterMaster, "audio-in", 1);
var delayProbes = Enum.GetValues<DelayTimingMode>().Select(mode =>
{
    FilterSet(delay, "timing", (int)mode);
    var compiled = FaustGraphCompiler.CreateBuiltIn().Compile(filterSession.Snapshot);
    string Name(GraphNodeSnapshot node, string key) => compiled.Controls.Single(c => c.Address == Address(node, key)).Name;
    return new { mode = mode.ToString(), source = compiled.Source,
        values = compiled.Controls.ToDictionary(c => c.Name, c => c.InitialValue),
        amplitude = Name(carrier, "amplitude"), feedback = Name(delay, "feedback"), mix = Name(delay, "mix"),
        modulation = Name(modulation, "amplitude") };
}).ToArray();
File.WriteAllText(Path.Combine(args[0], "delay.json"), JsonSerializer.Serialize(delayProbes));

filterSession.Execute(new RemoveNodeCommand(filterRoot, delay.Id));
var reverb = FilterAdd(ReverbProcessor.NodeType);
var excitation = FilterAdd(NoiseGenerator.NodeType);
FilterSet(excitation, "amplitude", 0);
FilterSet(reverb, "mix", 1);
FilterConnect(excitation, reverb, "audio-in", 1);
FilterConnect(modulation, reverb, "decay-modulation-in", 12);
FilterConnect(modulation, reverb, "damping-modulation-in", 1);
FilterConnect(modulation, reverb, "mix-modulation-in", 1);
FilterConnect(reverb, filterMaster, "audio-in", 1);
var reverbProgram = FaustGraphCompiler.CreateBuiltIn().Compile(filterSession.Snapshot);
string ReverbControl(GraphNodeSnapshot node, string key) => reverbProgram.Controls.Single(c => c.Address == Address(node, key)).Name;
File.WriteAllText(Path.Combine(args[0], "reverb.json"), JsonSerializer.Serialize(new
{
    source = reverbProgram.Source, values = reverbProgram.InitialControlValues(),
    bypass = reverbProgram.AuditionControls.Single(control => control.NodeId == reverb.Id && control.Kind == NodeAuditionKind.Bypass).Name,
    mute = reverbProgram.AuditionControls.Single(control => control.NodeId == reverb.Id && control.Kind == NodeAuditionKind.Mute).Name,
    amplitude = ReverbControl(excitation, "amplitude"), decay = ReverbControl(reverb, "decay"),
    damping = ReverbControl(reverb, "damping"), mix = ReverbControl(reverb, "mix"), modulation = ReverbControl(modulation, "amplitude")
}));

filterSession.Execute(new RemoveNodeCommand(filterRoot, reverb.Id));
var chorus = FilterAdd(ChorusProcessor.NodeType);
FilterSet(carrier, "frequency", 440);
FilterSet(carrier, "amplitude", 0.1);
FilterConnect(carrier, chorus, "audio-in", 1);
FilterConnect(modulation, chorus, "rate-modulation-in", 5);
FilterConnect(modulation, chorus, "depth-modulation-in", 10);
FilterConnect(modulation, chorus, "delay-modulation-in", 40);
FilterConnect(modulation, chorus, "mix-modulation-in", 1);
FilterConnect(chorus, filterMaster, "audio-in", 1);
var chorusProgram = FaustGraphCompiler.CreateBuiltIn().Compile(filterSession.Snapshot);
string ChorusControl(GraphNodeSnapshot node, string key) => chorusProgram.Controls.Single(c => c.Address == Address(node, key)).Name;
File.WriteAllText(Path.Combine(args[0], "chorus.json"), JsonSerializer.Serialize(new
{
    source = chorusProgram.Source, values = chorusProgram.Controls.ToDictionary(c => c.Name, c => c.InitialValue),
    rate = ChorusControl(chorus, "rate"), depth = ChorusControl(chorus, "depth"), delay = ChorusControl(chorus, "delay"),
    mix = ChorusControl(chorus, "mix"), modulation = ChorusControl(modulation, "amplitude")
}));

// Shared pitch control drives a three-oscillator bank through ordinary modulation edges.
using var pitchSession = new PatchSession(registry);
var pitchGraph = pitchSession.GraphId;
GraphNodeSnapshot PitchAdd(string type) => pitchSession.Execute(new AddNodeCommand(pitchGraph, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
void PitchSet(GraphNodeSnapshot node, string key, double value) => pitchSession.Execute(new SetParameterValueCommand(pitchGraph, Address(node, key), value));
void PitchWire(GraphNodeSnapshot source, GraphNodeSnapshot target, string port, double amount = 1) =>
    pitchSession.Execute(new ConnectNodesCommand(pitchGraph, new PortAddress(source.Id, source.Outputs.Single().Id),
        new PortAddress(target.Id, target.Inputs.Single(p => p.Key == port).Id), amount));
var pitchSource = PitchAdd(PitchControlGenerator.NodeType);
var pitchMixer = PitchAdd(MixerProcessor.NodeType);
var pitchMaster = PitchAdd(MasterOutput.NodeType);
var pitchFrequencies = new[] { 110d, 165d, 220d };
var pitchVoices = new List<GraphNodeSnapshot>();
foreach (var hz in pitchFrequencies)
{
    var voice = PitchAdd(OscillatorGenerator.NodeType);
    pitchVoices.Add(voice);
    PitchSet(voice, "frequency", hz);
    PitchSet(voice, "amplitude", 0.1);
    PitchSet(voice, OscillatorGenerator.PitchTrackingKey, 1);
    PitchWire(pitchSource, voice, OscillatorGenerator.PitchPortKey);
    PitchWire(voice, pitchMixer, "audio-in");
}
PitchWire(pitchMixer, pitchMaster, "audio-in");
var pitchProgram = FaustGraphCompiler.CreateBuiltIn().Compile(pitchSession.Snapshot);
File.WriteAllText(Path.Combine(args[0], "pitch.json"), JsonSerializer.Serialize(new
{
    source = pitchProgram.Source, values = pitchProgram.InitialControlValues(), frequencies = pitchFrequencies,
    frequencyMeters = pitchVoices.Select(voice => pitchProgram.Controls.Single(c => c.Address == Address(voice, "frequency")).MeterName).ToArray(),
    transpose = pitchProgram.Controls.Single(c => c.Address == Address(pitchSource, PitchControlGenerator.SemitonesKey)).Name,
    cents = pitchProgram.Controls.Single(c => c.Address == Address(pitchSource, PitchControlGenerator.CentsKey)).Name
}));

// Direct signal fixture exercises the exact production state machine with sample-accurate
// arbitrary edges and controls. The impact fixture exercises normal nested graph routing.
var ad = FaustEnvelopeSegments.EmitAd(FaustSchmittEdge.Emit("signal"), "a", "d", "ac", "dc");
using var impactSession = new PatchSession(BuiltInNodes.CreateEditorRegistry());
var impactGraph = impactSession.GraphId;
var impact = impactSession.Execute(new AddNodeCommand(impactGraph, ImpactTemplate.PresetKey)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
var impactMaster = impactSession.Execute(new AddNodeCommand(impactGraph, MasterOutput.NodeType)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
impactSession.Execute(new ConnectNodesCommand(impactGraph, new PortAddress(impact.Id, impact.Outputs.Single().Id),
    new PortAddress(impactMaster.Id, impactMaster.Inputs.Single().Id)));
var impactProgram = FaustGraphCompiler.CreateBuiltIn().Compile(impactSession.Snapshot, SignalTapManifest.MaxAuxiliaryChannels);
var impactChild = impact.Composite!.Graph;
var envelopes = impactChild.Nodes.Where(n => n.TypeKey == EnvelopeGenerator.NodeType).ToArray();
var impactPitch = envelopes.Single(n => n.Parameters.Single(p => p.Definition.Key == EnvelopeGenerator.AttackKey).BaseValue == 1);
var impactAmp = envelopes.Single(n => n.Id != impactPitch.Id);
var impactTrigger = impactChild.Nodes.Single(n => n.TypeKey == OscillatorGenerator.NodeType && n.Parameters.Single(p => p.Definition.Key == "frequency").BaseValue == 1);
int Tap(GraphNodeSnapshot node) => impactProgram.SignalTaps.Channels[new SignalObservationAddress(impactGraph, impactChild.Id, node.Id, node.Outputs.Single().Id, PortDirection.Output)];
File.WriteAllText(Path.Combine(args[0], "envelope.json"), JsonSerializer.Serialize(new
{
    directSource = $"import(\"stdfaust.lib\"); process(signal,a,d,ac,dc) = {ad};",
    source = impactProgram.Source, values = impactProgram.InitialControlValues(),
    pitch = Tap(impactPitch), amplitude = Tap(impactAmp), trigger = Tap(impactTrigger)
}));

// Compare direct and rack-routed event fan-out through the actual compiler.
var rhythms = new[] { SignalType.Trigger, SignalType.Gate }.Select(kind =>
{
    var rhythmRegistry = BuiltInNodes.CreateRegistry();
    rhythmRegistry.Register(CompositeNode.NodeType, id => new CompositeNode(id, "Rhythm route",
        [new ExposedPort(Guid.NewGuid(), "In", kind)], [new ExposedPort(Guid.NewGuid(), "Out", kind)]));
    using var rhythm = new PatchSession(rhythmRegistry);
    var graph = rhythm.GraphId;
    GraphNodeSnapshot AddRhythm(string type) => rhythm.Execute(new AddNodeCommand(graph, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
    void WireRhythm(GraphNodeSnapshot from, GraphNodeSnapshot to, string key, double amount = 1) => rhythm.Execute(new ConnectNodesCommand(graph,
        new(from.Id, from.Outputs.Single().Id), new(to.Id, to.Inputs.Single(p => p.Key == key).Id), amount));
    void SetRhythm(GraphNodeSnapshot node, string key, double value) => rhythm.Execute(new SetParameterValueCommand(graph, Address(node, key), value));
    var oscillator = AddRhythm(OscillatorGenerator.NodeType);
    SetRhythm(oscillator, "frequency", 2); SetRhythm(oscillator, "waveform", (int)OscillatorWaveform.Square);
    var adapter = AddRhythm(kind == SignalType.Trigger ? SignalToTriggerGenerator.NodeType : SignalToGateGenerator.NodeType);
    var route = AddRhythm(CompositeNode.NodeType);
    var inner = route.Composite!;
    rhythm.Execute(new ConnectNodesCommand(inner.Graph.Id, new(inner.InputRackId, route.Inputs[0].Id), new(inner.OutputRackId, route.Outputs[0].Id)));
    var first = AddRhythm(EnvelopeGenerator.NodeType); var second = AddRhythm(EnvelopeGenerator.NodeType);
    var voice = AddRhythm(OscillatorGenerator.NodeType); var output = AddRhythm(MasterOutput.NodeType);
    var mode = kind == SignalType.Trigger ? 1 : 2;
    var key = kind == SignalType.Trigger ? EnvelopeGenerator.EventTriggerPortKey : EnvelopeGenerator.GatePortKey;
    foreach (var node in new[] { first, second })
    {
        SetRhythm(node, EnvelopeGenerator.TriggerSourceKey, mode);
        SetRhythm(node, EnvelopeGenerator.AttackKey, 8); SetRhythm(node, EnvelopeGenerator.DecayKey, 100);
    }
    WireRhythm(oscillator, adapter, RhythmicAdapter.InputKey);
    WireRhythm(adapter, route, route.Inputs[0].Key);
    WireRhythm(adapter, first, key); WireRhythm(route, second, key);
    WireRhythm(first, voice, "amplitude-modulation-in", 0.5); WireRhythm(second, voice, "frequency-modulation-in", 100);
    WireRhythm(voice, output, "audio-in");
    var compiled = FaustGraphCompiler.CreateBuiltIn().Compile(rhythm.Snapshot, SignalTapManifest.MaxAuxiliaryChannels);
    int Channel(GraphNodeSnapshot n) => compiled.SignalTaps.Channels[new(graph, graph, n.Id, n.Outputs[0].Id, PortDirection.Output)];
    return new { kind = kind.ToString(), source = compiled.Source, values = compiled.InitialControlValues(), adapter = Channel(adapter),
        route = Channel(route), first = Channel(first), second = Channel(second),
        mute = compiled.AuditionControls.Single(c => c.NodeId == adapter.Id && c.Kind == NodeAuditionKind.Mute).Name,
        routeMute = compiled.AuditionControls.Single(c => c.NodeId == route.Id && c.Kind == NodeAuditionKind.Mute).Name };
}).ToArray();
File.WriteAllText(Path.Combine(args[0], "rhythms.json"), JsonSerializer.Serialize(new
{
    directSource = $"import(\"stdfaust.lib\"); process(signal) = ({FaustSchmittEdge.Emit("signal")}), ({FaustSchmittEdge.EmitGate("signal")});",
    rhythms
}));

// Actual mixed transport/manual graph: a clock feeds a timed gate, and two
// independent manual channels feed separate envelope paths into one carrier.
using var sourcesSession = new PatchSession(BuiltInNodes.CreateRegistry());
var sourcesGraph = sourcesSession.GraphId;
GraphNodeSnapshot AddSource(string type) => sourcesSession.Execute(new AddNodeCommand(sourcesGraph, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
void SetSource(GraphNodeSnapshot node, string key, double value) => sourcesSession.Execute(new SetParameterValueCommand(sourcesGraph, Address(node, key), value));
void WireSource(GraphNodeSnapshot from, GraphNodeSnapshot to, string key, double amount = 1) => sourcesSession.Execute(new ConnectNodesCommand(sourcesGraph,
    new(from.Id, from.Outputs[0].Id), new(to.Id, to.Inputs.Single(p => p.Key == key).Id), amount));
var sourceClock = AddSource(ClockGenerator.NodeType);
var clockGate = AddSource(GateGenerator.NodeType); SetSource(clockGate, GateGenerator.SourceKey, 1); SetSource(clockGate, GateGenerator.DurationKey, 50);
WireSource(sourceClock, clockGate, GateGenerator.TriggerPortKey);
var manual = AddSource(ManualTriggerGenerator.NodeType);
var manualGate = AddSource(GateGenerator.NodeType); SetSource(manualGate, GateGenerator.DurationKey, 100);
var sourceVoice = AddSource(OscillatorGenerator.NodeType); SetSource(sourceVoice, "amplitude", 0);
var sourceMaster = AddSource(MasterOutput.NodeType); WireSource(sourceVoice, sourceMaster, "audio-in");
foreach (var rhythmic in new[] { manual, manualGate, clockGate })
{
    var env = AddSource(EnvelopeGenerator.NodeType);
    bool gateSignal = rhythmic.Outputs[0].SignalType == SignalType.Gate;
    SetSource(env, EnvelopeGenerator.TriggerSourceKey, gateSignal ? 2 : 1);
    SetSource(env, EnvelopeGenerator.DecayKey, 100);
    WireSource(rhythmic, env, gateSignal ? EnvelopeGenerator.GatePortKey : EnvelopeGenerator.EventTriggerPortKey);
    WireSource(env, sourceVoice, "amplitude-modulation-in", 0.25);
}
var sourcesProgram = FaustGraphCompiler.CreateBuiltIn().Compile(sourcesSession.Snapshot, SignalTapManifest.MaxAuxiliaryChannels);
int SourceChannel(GraphNodeSnapshot node) => sourcesProgram.SignalTaps.Channels[new(sourcesGraph, sourcesGraph, node.Id, node.Outputs[0].Id, PortDirection.Output)];
File.WriteAllText(Path.Combine(args[0], "rhythmic-sources.json"), JsonSerializer.Serialize(new
{
    source = sourcesProgram.Source, values = sourcesProgram.InitialControlValues(),
    phaseChannels = sourcesProgram.TransportInputChannels,
    actions = sourcesProgram.RhythmicInputs.Select(input => new { graphId = input.GraphId, nodeId = input.NodeId, allowClose = input.AllowClose }),
    manualIndex = sourcesProgram.RhythmicInputs.ToList().FindIndex(input => input.NodeId == manual.Id),
    gateIndex = sourcesProgram.RhythmicInputs.ToList().FindIndex(input => input.NodeId == manualGate.Id),
    manual = SourceChannel(manual), manualGate = SourceChannel(manualGate), clock = SourceChannel(sourceClock), clockGate = SourceChannel(clockGate),
    channels = sourcesProgram.SignalTaps.ChannelCount,
    clockSource = $"import(\"stdfaust.lib\"); process(beat,reset,choice) = {FaustRhythmicSources.SynchronizedClock("beat", "reset", "choice")};",
    freeSource = $"import(\"stdfaust.lib\"); process = {FaustRhythmicSources.FreeClock("2.0")};",
    gateSource = $"import(\"stdfaust.lib\"); process(input,ms) = {FaustRhythmicSources.TimedGate("input", "ms")};"
}));

GraphNodeSnapshot TempoAdd(Guid graph, string type) =>
    tempoSession.Execute(new AddNodeCommand(graph, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;

void TempoConnect(Guid graph, Guid source, Guid output, Guid destination, Guid input, double amount = 1) =>
    tempoSession.Execute(new ConnectNodesCommand(graph, new PortAddress(source, output), new PortAddress(destination, input), amount));

GraphNodeSnapshot Add(Guid graph, string type) =>
    session.Execute(new AddNodeCommand(graph, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;

void Connect(Guid graph, Guid source, Guid output, Guid destination, Guid input, double amount = 1) =>
    session.Execute(new ConnectNodesCommand(graph, new PortAddress(source, output), new PortAddress(destination, input), amount));

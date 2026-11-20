using System.Text.Json;
using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Audio.Compilation;
using Orbit.Domain.Nodes.Composites;
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
File.WriteAllText(Path.Combine(args[0], "controls.json"), JsonSerializer.Serialize(new
{
    values = program.Controls.ToDictionary(control => control.Name, control => control.InitialValue),
    frequency = frequency.Name,
    frequencyMeter = frequency.MeterName,
    amplitude = amplitude.Name
}));

// One root and one child tempo LFO share the same external beat signal. A
// summed boundary lets the offline browser compare their phase to an oracle.
var tempoRegistry = BuiltInNodes.CreateRegistry();
tempoRegistry.Register(CompositeNode.NodeType, id => new CompositeNode(id, "Tempo probe",
    [new ExposedPort(Guid.NewGuid(), "External", SignalType.Audio)],
    [new ExposedPort(Guid.NewGuid(), "Mixed", SignalType.Audio, FanIn: FanInPolicy.Sum)]));
using var tempoSession = new PatchSession(tempoRegistry);
var tempoRoot = tempoSession.GraphId;
var outerLfo = TempoAdd(tempoRoot, TempoSineGenerator.NodeType);
var tempoDrone = TempoAdd(tempoRoot, CompositeNode.NodeType);
var tempoMaster = TempoAdd(tempoRoot, MasterOutput.NodeType);
var tempoChild = tempoDrone.Composite!.Graph.Id;
var innerLfo = TempoAdd(tempoChild, TempoSineGenerator.NodeType);
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
var waveformProbes = Enum.GetValues<OscillatorWaveform>().Select(waveform =>
{
    waveformSession.Execute(new SetParameterValueCommand(waveformGraph, waveformAddress, (int)waveform));
    var compiled = FaustGraphCompiler.CreateBuiltIn().Compile(waveformSession.Snapshot);
    string ControlName(string key) => compiled.Controls.Single(control => control.Address.ParameterId ==
        waveNode.Parameters.Single(parameter => parameter.Definition.Key == key).Id).Name;
    return new
    {
        waveform = waveform.ToString(),
        source = compiled.Source,
        values = compiled.Controls.ToDictionary(control => control.Name, control => control.InitialValue),
        frequency = ControlName("frequency"),
        phase = ControlName("phase")
    };
}).ToArray();
File.WriteAllText(Path.Combine(args[0], "waveforms.json"), JsonSerializer.Serialize(waveformProbes));

GraphNodeSnapshot TempoAdd(Guid graph, string type) =>
    tempoSession.Execute(new AddNodeCommand(graph, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;

void TempoConnect(Guid graph, Guid source, Guid output, Guid destination, Guid input, double amount = 1) =>
    tempoSession.Execute(new ConnectNodesCommand(graph, new PortAddress(source, output), new PortAddress(destination, input), amount));

GraphNodeSnapshot Add(Guid graph, string type) =>
    session.Execute(new AddNodeCommand(graph, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;

void Connect(Guid graph, Guid source, Guid output, Guid destination, Guid input, double amount = 1) =>
    session.Execute(new ConnectNodesCommand(graph, new PortAddress(source, output), new PortAddress(destination, input), amount));

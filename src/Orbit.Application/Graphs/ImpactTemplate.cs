using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Application.Graphs;

/// <summary>Editable worked impact patch: one oscillator triggers independent pitch and amplitude envelopes.</summary>
public static class ImpactTemplate
{
    public const string PresetKey = "preset.impact.ad-percussion";

    public static CompositeNode Create(Guid id)
    {
        var output = new ExposedPort(Guid.NewGuid(), "Audio", SignalType.Audio);
        var impact = new CompositeNode(id, "AD percussion", [], [output]);
        var graph = impact.InternalGraph;
        var trigger = new OscillatorGenerator(Guid.NewGuid());
        var pitch = new EnvelopeGenerator(Guid.NewGuid());
        var amplitude = new EnvelopeGenerator(Guid.NewGuid());
        var voice = new OscillatorGenerator(Guid.NewGuid());
        foreach (var node in new ISignalNode[] { trigger, pitch, amplitude, voice }) graph.AddNode(node);
        Set(trigger, "waveform", (int)OscillatorWaveform.Square);
        Set(trigger, "frequency", 1);
        Set(pitch, EnvelopeGenerator.AttackKey, 1);
        Set(pitch, EnvelopeGenerator.DecayKey, 120);
        Set(voice, "frequency", 55);
        Set(voice, "amplitude", 0);
        Connect(trigger, pitch, EnvelopeGenerator.TriggerPortKey);
        Connect(trigger, amplitude, EnvelopeGenerator.TriggerPortKey);
        Connect(pitch, voice, "frequency-modulation-in", 300);
        Connect(amplitude, voice, "amplitude-modulation-in", 0.8);
        graph.Connect(new PortAddress(voice.Id, voice.Outputs.Single().Id), new PortAddress(impact.OutputRack.Id, output.Id));
        return impact;

        void Set(ISignalNode node, string key, double value) => graph.SetParameterValue(
            new ParameterAddress(node.Id, node.Parameters.Single(p => p.Definition.Key == key).Id), value);
        void Connect(ISignalNode source, ISignalNode destination, string port, double amount = 1) => graph.Connect(
            new PortAddress(source.Id, source.Outputs.Single().Id), new PortAddress(destination.Id, destination.Inputs.Single(p => p.Key == port).Id), amount);
    }
}

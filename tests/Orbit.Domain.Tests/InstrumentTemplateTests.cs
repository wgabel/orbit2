using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Persistence;
using Orbit.Audio.Compilation;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class InstrumentTemplateTests
{
    [Theory]
    [InlineData(InstrumentTemplates.PlayableOrgan)]
    [InlineData(InstrumentTemplates.PlayableFlute)]
    [InlineData(InstrumentTemplates.PlayableBrass)]
    [InlineData(InstrumentTemplates.PlayableStrings)]
    [InlineData(InstrumentTemplates.PlayableDidgeridoo)]
    public void PlayablePresetsExposeTypedNoteInputsAndRoundTrip(string key)
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var root = session.GraphId;
        var instrument = NestedFaustPatch.Add(session, root, key);
        var other = NestedFaustPatch.Add(session, root, key);
        var note = NestedFaustPatch.Add(session, root, NoteSourceGenerator.NodeType);
        var master = NestedFaustPatch.Add(session, root, MasterOutput.NodeType);
        Assert.Equal(new[] { "Pitch (st)", "Gate", "Velocity" }, instrument.Inputs.Select(p => p.Name));
        Assert.Equal(new[] { SignalType.Modulation, SignalType.Gate, SignalType.Modulation }, instrument.Inputs.Select(p => p.SignalType));
        var graph = instrument.Composite!.Graph;
        Assert.Empty(graph.Nodes.Select(n => n.Id).Intersect(other.Composite!.Graph.Nodes.Select(n => n.Id)));
        var envelope = Assert.Single(graph.Nodes, n => n.TypeKey == EnvelopeGenerator.NodeType);
        Assert.Equal((int)EnvelopeMode.Adsr, envelope.Parameters.Single(p => p.Definition.Key == EnvelopeGenerator.ModeKey).BaseValue);
        Assert.Contains(graph.Connections, c => c.Source.PortId == instrument.Inputs[1].Id && c.Destination.NodeId == envelope.Id);
        var gains = graph.Nodes.Where(n => n.TypeKey == GainProcessor.NodeType && n.Parameters.Single(p => p.Definition.Key == "gain").BaseValue == 0).ToArray();
        Assert.Equal(2, gains.Length);
        var velocityGain = Assert.Single(gains, g => graph.Connections.Any(c => c.Source.PortId == instrument.Inputs[2].Id && c.Destination.NodeId == g.Id));
        var envelopeGain = Assert.Single(gains, g => g.Id != velocityGain.Id);
        Assert.Contains(graph.Connections, c => c.Source.NodeId == envelope.Id && c.Destination.NodeId == envelopeGain.Id);
        Assert.Contains(graph.Connections, c => c.Source.NodeId == velocityGain.Id && c.Destination.NodeId == envelopeGain.Id);
        Assert.Contains(graph.Connections, c => c.Source.NodeId == envelopeGain.Id && graph.GetNode(c.Destination.NodeId).TypeKey == ReverbProcessor.NodeType);
        foreach (var (output, index) in new[] { ("pitch-out", 0), ("gate-out", 1), ("velocity-out", 2) })
            session.Execute(new ConnectNodesCommand(root, new(note.Id, note.Outputs.Single(p => p.Key == output).Id), new(instrument.Id, instrument.Inputs[index].Id)));
        session.Execute(new ConnectNodesCommand(root, new(instrument.Id, instrument.Outputs[0].Id), new(master.Id, master.Inputs[0].Id)));
        var serializer = new PatchDocumentSerializer();
        var json = serializer.Serialize(serializer.Capture(session, 120, root, []));
        using var restored = serializer.Restore(serializer.Deserialize(json), registry);
        Assert.Equal(json, serializer.Serialize(serializer.Capture(restored, 120, root, [])));
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var program = compiler.Compile(session.Snapshot);
        Assert.Single(program.RhythmicInputs);
        Assert.Equal(program.Source, compiler.Compile(restored.Snapshot).Source);
    }

    [Theory]
    [InlineData(InstrumentTemplates.Organ, 5)]
    [InlineData(InstrumentTemplates.Flute, 3)]
    [InlineData(InstrumentTemplates.Brass, 3)]
    [InlineData(InstrumentTemplates.Strings, 3)]
    [InlineData(InstrumentTemplates.Didgeridoo, 2)]
    public void PresetsAreIndependentEditablePitchTrackedComposites(string key, int voiceCount)
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var root = session.GraphId;
        var first = NestedFaustPatch.Add(session, root, key);
        var second = NestedFaustPatch.Add(session, root, key);
        var graph = first.Composite!.Graph;
        var other = second.Composite!.Graph;
        Assert.Empty(graph.Nodes.Select(n => n.Id).Intersect(other.Nodes.Select(n => n.Id)));
        Assert.Equal(new[] { "Pitch (st)", "Gain offset" }, first.Inputs.Select(p => p.Name));
        Assert.Equal("Audio", Assert.Single(first.Outputs).Name);
        var voices = graph.Nodes.Where(n => n.TypeKey == OscillatorGenerator.NodeType).ToArray();
        Assert.Equal(voiceCount, voices.Length);
        foreach (var voice in voices)
        {
            Assert.Equal(1, voice.Parameters.Single(p => p.Definition.Key == OscillatorGenerator.PitchTrackingKey).BaseValue);
            var pitch = voice.Inputs.Single(p => p.Key == OscillatorGenerator.PitchPortKey);
            Assert.Contains(graph.Connections, edge => edge.Source.NodeId == first.Composite.InputRackId &&
                edge.Source.PortId == first.Inputs[0].Id && edge.Destination.NodeId == voice.Id && edge.Destination.PortId == pitch.Id && edge.Amount == 1);
        }
        foreach (var noise in graph.Nodes.Where(n => n.TypeKey == NoiseGenerator.NodeType))
        {
            Assert.InRange(noise.Parameters.Single(p => p.Definition.Key == "amplitude").BaseValue, 0, .03);
            var edge = Assert.Single(graph.Connections, e => e.Source.NodeId == noise.Id);
            Assert.Equal(FilterProcessor.NodeType, graph.GetNode(edge.Destination.NodeId).TypeKey);
        }
        var master = NestedFaustPatch.Add(session, root, MasterOutput.NodeType);
        session.Execute(new ConnectNodesCommand(root, new(first.Id, first.Outputs[0].Id), new(master.Id, master.Inputs[0].Id)));
        var frequency = voices[0].Parameters.Single(p => p.Definition.Key == "frequency");
        session.Execute(new SetParameterValueCommand(graph.Id, new ParameterAddress(voices[0].Id, frequency.Id), 123));
        Assert.DoesNotContain(other.Nodes.Where(n => n.TypeKey == OscillatorGenerator.NodeType),
            n => n.Parameters.Single(p => p.Definition.Key == "frequency").BaseValue == 123);
        var serializer = new PatchDocumentSerializer();
        var json = serializer.Serialize(serializer.Capture(session, 120, root, []));
        using var restored = serializer.Restore(serializer.Deserialize(json), registry);
        Assert.Equal(json, serializer.Serialize(serializer.Capture(restored, 120, root, [])));
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        Assert.Equal(compiler.Compile(session.Snapshot).Source, compiler.Compile(restored.Snapshot).Source);
    }
}

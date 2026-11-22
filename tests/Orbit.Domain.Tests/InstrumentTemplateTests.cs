using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Persistence;
using Orbit.Audio.Compilation;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class InstrumentTemplateTests
{
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

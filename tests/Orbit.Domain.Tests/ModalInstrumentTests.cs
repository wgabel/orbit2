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

public sealed class ModalInstrumentTests
{
    [Theory]
    [InlineData(ModalInstrumentTemplates.Handpan, 4, 220)]
    [InlineData(ModalInstrumentTemplates.Bell, 5, 330)]
    [InlineData(ModalInstrumentTemplates.Gong, 6, 83)]
    [InlineData(ModalInstrumentTemplates.Wood, 3, 294)]
    public void PresetsRetainIndependentModesAndTypedBoundaryRouting(string key, int count, double fundamental)
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var root = session.GraphId;
        var first = NestedFaustPatch.Add(session, root, key);
        var second = NestedFaustPatch.Add(session, root, key);
        var graph = first.Composite!.Graph;
        Assert.Empty(graph.Nodes.Select(n => n.Id).Intersect(second.Composite!.Graph.Nodes.Select(n => n.Id)));
        Assert.Equal(new[] { "Trigger", "Pitch (st)", "Strength offset", "Excitation" }, first.Inputs.Select(p => p.Name));
        Assert.Equal(new[] { SignalType.Trigger, SignalType.Modulation, SignalType.Modulation, SignalType.Audio }, first.Inputs.Select(p => p.SignalType));
        Assert.Equal(SignalType.Audio, Assert.Single(first.Outputs).SignalType);
        var modes = graph.Nodes.Where(n => n.TypeKey == ResonatorProcessor.NodeType).ToArray();
        Assert.Equal(count, modes.Length);
        Assert.Equal(fundamental, modes.Min(m => m.Parameters.Single(p => p.Definition.Key == "frequency").BaseValue));
        Assert.Equal(count, modes.Select(m => m.Parameters.Single(p => p.Definition.Key == "decay").BaseValue).Distinct().Count());
        foreach (var mode in modes)
        {
            Assert.Equal(1, mode.Parameters.Single(p => p.Definition.Key == "mix").BaseValue);
            var pitch = mode.Inputs.Single(p => p.Key == ResonatorProcessor.PitchPortKey);
            Assert.Contains(graph.Connections, e => e.Source.NodeId == first.Composite.InputRackId && e.Source.PortId == first.Inputs[1].Id &&
                e.Destination.NodeId == mode.Id && e.Destination.PortId == pitch.Id && e.Amount == 1);
            Assert.Equal(MixerProcessor.NodeType, graph.GetNode(Assert.Single(graph.Connections, e => e.Source.NodeId == mode.Id).Destination.NodeId).TypeKey);
        }
        var envelope = Assert.Single(graph.Nodes, n => n.TypeKey == EnvelopeGenerator.NodeType);
        Assert.Equal(1, envelope.Parameters.Single(p => p.Definition.Key == EnvelopeGenerator.TriggerSourceKey).BaseValue);
        Assert.Contains(graph.Connections, e => e.Source.PortId == first.Inputs[0].Id && e.Destination.PortId == envelope.Inputs.Single(p => p.Key == EnvelopeGenerator.EventTriggerPortKey).Id);
        var clock = NestedFaustPatch.Add(session, root, ClockGenerator.NodeType);
        var master = NestedFaustPatch.Add(session, root, MasterOutput.NodeType);
        session.Execute(new ConnectNodesCommand(root, new(clock.Id, clock.Outputs[0].Id), new(first.Id, first.Inputs[0].Id)));
        session.Execute(new ConnectNodesCommand(root, new(first.Id, first.Outputs[0].Id), new(master.Id, master.Inputs[0].Id)));
        var frequency = modes[0].Parameters.Single(p => p.Definition.Key == "frequency");
        session.Execute(new SetParameterValueCommand(graph.Id, new ParameterAddress(modes[0].Id, frequency.Id), 123));
        Assert.DoesNotContain(second.Composite.Graph.Nodes.Where(n => n.TypeKey == ResonatorProcessor.NodeType),
            n => n.Parameters.Single(p => p.Definition.Key == "frequency").BaseValue == 123);
        var serializer = new PatchDocumentSerializer();
        var json = serializer.Serialize(serializer.Capture(session, 120, root, []));
        using var restored = serializer.Restore(serializer.Deserialize(json), registry);
        Assert.Equal(json, serializer.Serialize(serializer.Capture(restored, 120, root, [])));
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        Assert.Equal(compiler.Compile(session.Snapshot).Source, compiler.Compile(restored.Snapshot).Source);
    }
}

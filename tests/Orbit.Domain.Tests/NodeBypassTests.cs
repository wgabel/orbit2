using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Models;
using Orbit.Application.Persistence;
using Orbit.Audio.Analysis;
using Orbit.Audio.Compilation;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class NodeBypassTests
{
    [Theory]
    [InlineData(GainProcessor.NodeType)]
    [InlineData(FilterProcessor.NodeType)]
    [InlineData(ParametricEqProcessor.NodeType)]
    [InlineData(DelayProcessor.NodeType)]
    [InlineData(ReverbProcessor.NodeType)]
    [InlineData(ResonatorProcessor.NodeType)]
    [InlineData(ChorusProcessor.NodeType)]
    public void EffectsDeclareAnExplicitDryPath(string type)
    {
        var node = BuiltInNodes.CreateRegistry().Create(type);
        Assert.Equal("audio-in", node.BypassInputKey);
        Assert.False(node.IsBypassed);
    }

    [Theory]
    [InlineData(OscillatorGenerator.NodeType)]
    [InlineData(MixerProcessor.NodeType)]
    [InlineData(MasterOutput.NodeType)]
    public void NonEffectsRejectBypassWithoutRevision(string type)
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var node = NestedFaustPatch.Add(session, session.GraphId, type);
        var revision = session.Snapshot.Revision;
        Assert.Null(node.BypassInputKey);
        Assert.Throws<GraphValidationException>(() => session.Execute(new SetNodeBypassedCommand(session.GraphId, node.Id, true)));
        Assert.Equal(revision, session.Snapshot.Revision);
    }

    [Fact]
    public void BypassUsesWeightedDryRangeAndMuteTakesPrecedence()
    {
        using var session = new PatchSession(BuiltInNodes.CreateRegistry());
        var graph = session.GraphId;
        var source = NestedFaustPatch.Add(session, graph, OscillatorGenerator.NodeType);
        var gain = NestedFaustPatch.Add(session, graph, GainProcessor.NodeType);
        var carrier = NestedFaustPatch.Add(session, graph, OscillatorGenerator.NodeType);
        var master = NestedFaustPatch.Add(session, graph, MasterOutput.NodeType);
        var frequency = new ParameterAddress(carrier.Id, carrier.Parameters[0].Id);
        session.Execute(new SetParameterValueCommand(graph, new ParameterAddress(gain.Id, gain.Parameters[0].Id), 0));
        session.Execute(new ConnectNodesCommand(graph, new PortAddress(source.Id, source.Outputs[0].Id), new PortAddress(gain.Id, gain.Inputs[0].Id), 0.5));
        session.Execute(new ConnectNodesCommand(graph, new PortAddress(gain.Id, gain.Outputs[0].Id), new PortAddress(carrier.Id, carrier.Parameters[0].ModulationPortId!.Value), 100));
        session.Execute(new ConnectNodesCommand(graph, new PortAddress(carrier.Id, carrier.Outputs[0].Id), new PortAddress(master.Id, master.Inputs[0].Id)));
        var before = session.Snapshot;
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var sourceCode = compiler.Compile(before).Source;
        var estimator = new GraphModulationRangeEstimator(BuiltInFaustEmitters.Create());
        ParameterRange Range() => estimator.Estimate(session.Snapshot)[frequency]!.Value;
        Assert.Equal(new ParameterRange(440, 440), Range());
        var change = session.Execute(new SetNodeBypassedCommand(graph, gain.Id, true));
        Assert.False(change.ChangeSet!.TopologyChanged);
        Assert.Equal(new NodeBypassedChanged(graph, gain.Id, false, true), Assert.Single(change.ChangeSet.Events));
        Assert.False(session.Execute(new SetNodeBypassedCommand(graph, gain.Id, true)).Changed);
        Assert.Equal(new ParameterRange(390, 490), Range());
        var program = compiler.Compile(session.Snapshot);
        Assert.Equal(sourceCode, program.Source);
        Assert.Equal(1, program.AuditionControls.Single(control => control.NodeId == gain.Id && control.Kind == NodeAuditionKind.Bypass).InitialValue);
        session.Execute(new SetNodeMutedCommand(graph, gain.Id, true));
        Assert.Equal(new ParameterRange(440, 440), Range());
        session.Undo();
        Assert.Equal(new ParameterRange(390, 490), Range());
        session.Undo();
        Assert.Equal(new ParameterRange(440, 440), Range());
        session.Redo();
        Assert.True(session.Snapshot.GetNode(gain.Id).IsBypassed);
        Assert.Equal(before.Connections.ToArray(), session.Snapshot.Connections.ToArray());
    }

    [Fact]
    public void NestedBypassAndMutePersistIndependentlyAndOldDocumentsDefaultOff()
    {
        using var patch = new NestedFaustPatch();
        var session = patch.Session;
        var gain = NestedFaustPatch.Add(session, patch.ChildId, GainProcessor.NodeType);
        session.Execute(new SetNodeBypassedCommand(patch.ChildId, gain.Id, true));
        session.Execute(new SetNodeMutedCommand(patch.ChildId, gain.Id, true));
        var serializer = new PatchDocumentSerializer();
        var json = serializer.Serialize(serializer.Capture(session, 120, patch.ChildId, []));
        using var restored = serializer.Restore(serializer.Deserialize(json), BuiltInNodes.CreateEditorRegistry());
        var node = restored.Snapshot.GetGraph(patch.ChildId).GetNode(gain.Id);
        Assert.True(node.IsMuted);
        Assert.True(node.IsBypassed);
        using var legacy = serializer.Restore(serializer.Deserialize(json.Replace(",\"isBypassed\":true", "").Replace("\"isBypassed\":true,", "")), BuiltInNodes.CreateEditorRegistry());
        Assert.False(legacy.Snapshot.GetGraph(patch.ChildId).GetNode(gain.Id).IsBypassed);
        Assert.True(legacy.Snapshot.GetGraph(patch.ChildId).GetNode(gain.Id).IsMuted);
    }
}

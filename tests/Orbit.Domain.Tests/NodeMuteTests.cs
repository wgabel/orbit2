using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Models;
using Orbit.Application.Persistence;
using Orbit.Audio.Analysis;
using Orbit.Audio.Compilation;
using Orbit.Domain.Graphs;
using Orbit.Domain.Parameters;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class NodeMuteTests
{
    [Fact]
    public void MuteCommitsOnceWithStableTopologyAndZeroModulationRange()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings(440, 0.5, 100));
        var original = session.Snapshot;
        var source = original.Nodes[0];
        var carrier = original.Nodes[1];
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var before = compiler.Compile(original);
        var result = session.Execute(new SetNodeMutedCommand(original.Id, source.Id, true));
        Assert.Equal(new NodeMutedChanged(original.Id, source.Id, false, true), Assert.Single(result.ChangeSet!.Events));
        Assert.False(result.ChangeSet.TopologyChanged);
        Assert.True(session.Snapshot.GetNode(source.Id).IsMuted);
        Assert.False(source.IsMuted);
        Assert.Equal(original.Connections.ToArray(), session.Snapshot.Connections.ToArray());
        Assert.False(session.Execute(new SetNodeMutedCommand(original.Id, source.Id, true)).Changed);
        var muted = compiler.Compile(session.Snapshot);
        Assert.Equal(before.Source, muted.Source);
        Assert.Equal(0, muted.MuteControls.Single(control => control.NodeId == source.Id).InitialValue);
        var estimator = new GraphModulationRangeEstimator(BuiltInFaustEmitters.Create());
        Assert.Equal(new ParameterRange(440, 440), estimator.Estimate(session.Snapshot)[new ParameterAddress(carrier.Id, carrier.Parameters[0].Id)]!.Value);
        session.Undo();
        Assert.False(session.Snapshot.GetNode(source.Id).IsMuted);
        session.Redo();
        Assert.True(session.Snapshot.GetNode(source.Id).IsMuted);
    }

    [Fact]
    public void CompositeAndChildMuteRoundTripAndRacksRejectMute()
    {
        using var patch = new NestedFaustPatch();
        var session = patch.Session;
        var root = session.Snapshot;
        var composite = root.Nodes.Single(node => node.Composite is not null);
        var rack = composite.Composite!.InputRackId;
        Assert.Throws<GraphValidationException>(() => session.Execute(new SetNodeMutedCommand(root.Id, root.Nodes.Last().Id, true)));
        Assert.Throws<GraphValidationException>(() => session.Execute(new SetNodeMutedCommand(patch.OuterId, rack, false)));
        session.Execute(new SetNodeMutedCommand(root.Id, composite.Id, true));
        session.Execute(new SetNodeMutedCommand(patch.ChildId, patch.Frequency.NodeId, true));
        var serializer = new PatchDocumentSerializer();
        var json = serializer.Serialize(serializer.Capture(session, 120, root.Id, []));
        using var restored = serializer.Restore(serializer.Deserialize(json), BuiltInNodes.CreateEditorRegistry());
        Assert.True(restored.Snapshot.GetNode(composite.Id).IsMuted);
        Assert.True(restored.Snapshot.GetGraph(patch.ChildId).GetNode(patch.Frequency.NodeId).IsMuted);
        // Schema 1 documents written before this optional field default to unmuted.
        using var legacy = serializer.Restore(serializer.Deserialize(json.Replace("\"isMuted\":true,", "").Replace(",\"isMuted\":true", "")), BuiltInNodes.CreateEditorRegistry());
        Assert.False(legacy.Snapshot.GetNode(composite.Id).IsMuted);
        Assert.False(legacy.Snapshot.GetGraph(patch.ChildId).GetNode(patch.Frequency.NodeId).IsMuted);
    }
}

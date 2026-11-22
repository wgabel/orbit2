using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Persistence;
using Orbit.Audio.Analysis;
using Orbit.Audio.Compilation;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class ResonatorTests
{
    [Fact]
    public void InstancesOwnIndependentControlsAndPorts()
    {
        var registry = BuiltInNodes.CreateRegistry();
        var first = registry.Create(ResonatorProcessor.NodeType);
        var second = registry.Create(ResonatorProcessor.NodeType);
        Assert.Empty(first.Parameters.Select(p => p.Id).Intersect(second.Parameters.Select(p => p.Id)));
        Assert.Empty(first.Inputs.Select(p => p.Id).Intersect(second.Inputs.Select(p => p.Id)));
        Assert.NotEqual(first.Outputs[0].Id, second.Outputs[0].Id);
    }

    [Fact]
    public void ResonatorUsesSharedPitchMathLiveControlsAndPersistence()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var root = session.GraphId;
        var pitch = NestedFaustPatch.Add(session, root, PitchControlGenerator.NodeType);
        var voice = NestedFaustPatch.Add(session, root, OscillatorGenerator.NodeType);
        var resonator = NestedFaustPatch.Add(session, root, ResonatorProcessor.NodeType);
        var output = NestedFaustPatch.Add(session, root, MasterOutput.NodeType);
        void Wire(GraphNodeSnapshot from, GraphNodeSnapshot to, string key, double amount = 1) =>
            session.Execute(new ConnectNodesCommand(root, new(from.Id, from.Outputs[0].Id), new(to.Id, to.Inputs.Single(p => p.Key == key).Id), amount));
        ParameterAddress Address(GraphNodeSnapshot node, string key) => new(node.Id, node.Parameters.Single(p => p.Definition.Key == key).Id);
        void Set(GraphNodeSnapshot node, string key, double value) => session.Execute(new SetParameterValueCommand(root, Address(node, key), value));
        Wire(voice, resonator, "audio-in");
        Wire(pitch, resonator, ResonatorProcessor.PitchPortKey);
        Wire(pitch, resonator, "frequency-modulation-in", .5);
        Wire(resonator, output, "audio-in");
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var before = compiler.Compile(session.Snapshot);
        Assert.All(resonator.Parameters, parameter => Assert.NotNull(parameter.ModulationPortId));
        Set(pitch, PitchControlGenerator.SemitonesKey, 12);
        Set(resonator, "decay", 4);
        var ranges = new GraphModulationRangeEstimator(BuiltInFaustEmitters.Create()).Estimate(session.Snapshot);
        Assert.Equal(new ParameterRange(446, 446), ranges[Address(resonator, "frequency")]);
        var after = compiler.Compile(session.Snapshot);
        Assert.Equal(before.Source, after.Source);
        Assert.NotNull(after.Controls.Single(c => c.Address == Address(resonator, "frequency")).MeterName);
        var serializer = new PatchDocumentSerializer();
        var json = serializer.Serialize(serializer.Capture(session, 120, root, []));
        using var restored = serializer.Restore(serializer.Deserialize(json), registry);
        Assert.Equal(json, serializer.Serialize(serializer.Capture(restored, 120, root, [])));
        Assert.Equal(after.Source, compiler.Compile(restored.Snapshot).Source);
    }
}

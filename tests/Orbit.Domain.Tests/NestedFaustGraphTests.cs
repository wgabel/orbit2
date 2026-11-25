using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Audio.Analysis;
using Orbit.Audio.Compilation;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

/// <summary>Exercises the same boundary routes with the production compiler and range estimator.</summary>
public sealed class NestedFaustGraphTests
{
    [Fact]
    public void NestedWeightedFanInAndFanOutResolveToLeafControls()
    {
        using var patch = new NestedFaustPatch();
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var first = compiler.Compile(patch.Session.Snapshot);
        Assert.Equal(9, first.Controls.Count);
        var control = first.Controls.Single(c => c.GraphId == patch.ChildId && c.Address == patch.Frequency);
        Assert.NotNull(control.MeterName);
        Assert.Contains(" * 20.0", first.Source);
        Assert.Contains(" * -30.0", first.Source);
        Assert.Contains(" * 2.0", first.Source);
        Assert.Contains(" * 0.5", first.Source);
        Assert.DoesNotContain("rack.input", first.Source);
        var estimator = new GraphModulationRangeEstimator(BuiltInFaustEmitters.Create());
        Assert.Empty(estimator.Estimate(patch.Session.Snapshot));
        Assert.Equal(new ParameterRange(340, 540), estimator.Estimate(patch.Session.Snapshot, patch.ChildId)[patch.Frequency]!.Value);
        patch.Session.Execute(new SetParameterValueCommand(patch.ChildId, patch.Frequency, 330));
        var edited = compiler.Compile(patch.Session.Snapshot);
        Assert.Equal(first.Source, edited.Source);
        Assert.Equal(330, edited.Controls.Single(c => c.Name == control.Name).InitialValue);
        Assert.Equal(new ParameterRange(230, 430), estimator.Estimate(patch.Session.Snapshot, patch.ChildId)[patch.Frequency]!.Value);
    }

    [Fact]
    public void MacroBasesResolveInNestedGraphsWithoutChangingManualValuesOrDspSource()
    {
        using var patch = new NestedFaustPatch();
        var session = patch.Session;
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var original = session.Snapshot;
        var source = compiler.Compile(original).Source;
        var wrapper = original.GetGraph(patch.OuterId).Nodes.Single(n => n.Composite?.Graph.Id == patch.ChildId);
        var oscillator = original.GetGraph(patch.ChildId).GetNode(patch.Frequency.NodeId);
        var pulse = oscillator.Parameters.Single(p => p.Definition.Key == OscillatorGenerator.PulseWidthKey);
        var pulseAddress = new ParameterAddress(oscillator.Id, pulse.Id);
        var macro = new CompositeMacroDefinition(Guid.NewGuid(), "Size", 0, 0.5,
            new([new(patch.Frequency, 440, 240, 2), new(pulseAddress, 0.2, 0.4)]));
        var change = session.Execute(new ReplaceCompositeMacrosCommand(patch.OuterId, wrapper.Id, [macro])).ChangeSet!;
        Assert.False(change.TopologyChanged);
        var authored = session.Snapshot;
        var resolved = authored.WithEffectiveMacroBaseValues().GetGraph(patch.ChildId).GetNode(oscillator.Id);
        Assert.Equal(440, authored.GetGraph(patch.ChildId).GetNode(oscillator.Id).Parameters.Single(p => p.Id == patch.Frequency.ParameterId).BaseValue);
        Assert.Equal(390, resolved.Parameters.Single(p => p.Id == patch.Frequency.ParameterId).BaseValue);
        Assert.Equal(pulse.BaseValue, resolved.Parameters.Single(p => p.Id == pulse.Id).BaseValue);
        var compiled = compiler.Compile(authored);
        Assert.Equal(source, compiled.Source);
        Assert.Equal(390, compiled.Controls.Single(c => c.GraphId == patch.ChildId && c.Address == patch.Frequency).InitialValue);
        Assert.DoesNotContain(compiled.Controls, c => c.GraphId == patch.ChildId && c.Address == pulseAddress);
        Assert.Equal(new ParameterRange(290, 490),
            new GraphModulationRangeEstimator(BuiltInFaustEmitters.Create()).Estimate(authored, patch.ChildId)[patch.Frequency]!.Value);

        var waveform = oscillator.Parameters.Single(p => p.Definition.Key == "waveform");
        session.Execute(new SetParameterValueCommand(patch.ChildId, new(oscillator.Id, waveform.Id), (int)OscillatorWaveform.Square));
        Assert.Equal(0.3, compiler.Compile(session.Snapshot).Controls.Single(c => c.GraphId == patch.ChildId && c.Address == pulseAddress).InitialValue, 10);
        Assert.Equal(pulse.BaseValue, session.Snapshot.GetGraph(patch.ChildId).GetNode(oscillator.Id).Parameters.Single(p => p.Id == pulse.Id).BaseValue);
    }

    [Fact]
    public void UnconnectedBoundaryProducesZeroAndUnusedCompositeDoesNotAffectProgram()
    {
        using var patch = new NestedFaustPatch();
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var before = compiler.Compile(patch.Session.Snapshot);
        NestedFaustPatch.Add(patch.Session, patch.Session.GraphId, CompositeNode.NodeType);
        Assert.Equal(before.Source, compiler.Compile(patch.Session.Snapshot).Source);

        var outer = patch.Session.Snapshot.Nodes.Single(n => n.Composite?.Graph.Id == patch.OuterId);
        foreach (var edge in patch.Session.Snapshot.Connections.Where(e => e.Destination.NodeId == outer.Id))
            patch.Session.Execute(new RemoveConnectionCommand(patch.Session.GraphId, edge.Id));
        var silentInput = compiler.Compile(patch.Session.Snapshot);
        Assert.Contains(" = 0.0;", silentInput.Source);
        Assert.Equal(new ParameterRange(440, 440),
            new GraphModulationRangeEstimator(BuiltInFaustEmitters.Create())
                .Estimate(patch.Session.Snapshot, patch.ChildId)[patch.Frequency]!.Value);
    }

    [Fact]
    public void RejectsMissingMirroredSocket()
    {
        using var patch = new NestedFaustPatch();
        var root = patch.Session.Snapshot;
        var outer = root.Nodes.Single(n => n.Composite is not null);
        var invalid = root with { Nodes = root.Nodes.Select(n => n.Id == outer.Id
            ? n with { Inputs = [] } : n).ToArray() };
        Assert.Throws<FaustCompilationException>(() => FaustGraphCompiler.CreateBuiltIn().Compile(invalid));
    }
}

/// <summary>Two nested drones share no mutable state. The parent sums two oscillators,
/// passes the result through both racks, and scales the child output on its way to master.</summary>
internal sealed class NestedFaustPatch : IDisposable
{
    public PatchSession Session { get; }
    public Guid OuterId { get; }
    public Guid ChildId { get; }
    public ParameterAddress Frequency { get; }

    public NestedFaustPatch()
    {
        var registry = BuiltInNodes.CreateRegistry();
        registry.Register(CompositeNode.NodeType, id => new CompositeNode(id, "Drone",
            [new ExposedPort(Guid.NewGuid(), "Pitch", SignalType.Modulation, FanIn: FanInPolicy.Sum)],
            [new ExposedPort(Guid.NewGuid(), "Audio", SignalType.Audio)]));
        Session = new PatchSession(registry);
        var rootId = Session.GraphId;
        var first = Add(Session, rootId, OscillatorGenerator.NodeType);
        var second = Add(Session, rootId, OscillatorGenerator.NodeType);
        var outer = Add(Session, rootId, CompositeNode.NodeType);
        var master = Add(Session, rootId, MasterOutput.NodeType);
        var outerGraph = outer.Composite!;
        OuterId = outerGraph.Graph.Id;
        var inner = Add(Session, OuterId, CompositeNode.NodeType);
        var child = inner.Composite!;
        ChildId = child.Graph.Id;
        var carrier = Add(Session, ChildId, OscillatorGenerator.NodeType);
        Frequency = new ParameterAddress(carrier.Id, carrier.Parameters[0].Id);

        Connect(rootId, first.Id, first.Outputs[0].Id, outer.Id, outer.Inputs[0].Id, 20);
        Connect(rootId, second.Id, second.Outputs[0].Id, outer.Id, outer.Inputs[0].Id, -30);
        Connect(OuterId, outerGraph.InputRackId, outer.Inputs[0].Id, inner.Id, inner.Inputs[0].Id, 2);
        Connect(ChildId, child.InputRackId, inner.Inputs[0].Id, carrier.Id,
            carrier.Parameters[0].ModulationPortId!.Value);
        Connect(ChildId, carrier.Id, carrier.Outputs[0].Id, child.OutputRackId, inner.Outputs[0].Id);
        Connect(OuterId, inner.Id, inner.Outputs[0].Id, outerGraph.OutputRackId, outer.Outputs[0].Id, 0.5);
        Connect(rootId, outer.Id, outer.Outputs[0].Id, master.Id, master.Inputs[0].Id);
    }

    private void Connect(Guid graph, Guid source, Guid output, Guid destination, Guid input, double amount = 1) =>
        Session.Execute(new ConnectNodesCommand(graph, new PortAddress(source, output), new PortAddress(destination, input), amount));

    internal static GraphNodeSnapshot Add(PatchSession session, Guid graph, string type) =>
        session.Execute(new AddNodeCommand(graph, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;

    public void Dispose() => Session.Dispose();
}

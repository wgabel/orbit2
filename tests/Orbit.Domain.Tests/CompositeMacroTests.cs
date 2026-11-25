using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Parameters;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class CompositeMacroTests
{
    [Fact]
    public void PreviewAndApplyIndependentCurvesWithoutChangingTopology()
    {
        var graph = new CompositeNode(Guid.NewGuid(), "Test", [], []).InternalGraph;
        var first = new OscillatorGenerator(Guid.NewGuid());
        var second = new OscillatorGenerator(Guid.NewGuid());
        graph.AddNode(first);
        graph.AddNode(second);
        var frequency = new ParameterAddress(first.Id, first.Parameters.Single(p => p.Definition.Key == "frequency").Id);
        var amplitude = new ParameterAddress(second.Id, second.Parameters.Single(p => p.Definition.Key == "amplitude").Id);
        var bindings = new List<CompositeMacroBinding> { new(frequency, 100, 500), new(amplitude, 1, 0, 2) };
        var mapping = new CompositeMacroMapping(bindings);
        bindings.Clear();
        var preview = graph.PreviewCompositeMacro(mapping, 0.5);
        Assert.Equal(440, graph.GetParameter(frequency).BaseValue);
        Assert.Equal(300, preview[0].CurrentValue);
        Assert.Equal(0.75, preview[1].CurrentValue);
        Assert.Equal(preview.ToArray(), graph.ApplyCompositeMacro(mapping, 0.5).ToArray());
        Assert.Equal(300, graph.GetParameter(frequency).BaseValue);
        Assert.Equal(0.75, graph.GetParameter(amplitude).BaseValue);
        Assert.Empty(graph.ApplyCompositeMacro(mapping, 0.5));
        Assert.Empty(graph.Connections);
    }

    [Fact]
    public void InvalidTargetsAndRangesNeverPartiallyApply()
    {
        var graph = new CompositeNode(Guid.NewGuid(), "Test", [], []).InternalGraph;
        var oscillator = new OscillatorGenerator(Guid.NewGuid());
        graph.AddNode(oscillator);
        var frequency = new ParameterAddress(oscillator.Id, oscillator.Parameters.Single(p => p.Definition.Key == "frequency").Id);
        var amplitude = new ParameterAddress(oscillator.Id, oscillator.Parameters.Single(p => p.Definition.Key == "amplitude").Id);
        var waveform = new ParameterAddress(oscillator.Id, oscillator.Parameters.Single(p => p.Definition.Key == "waveform").Id);
        var good = new CompositeMacroBinding(frequency, 100, 500);
        Assert.Throws<GraphValidationException>(() => new CompositeMacroMapping([good, good]));
        Assert.Throws<GraphValidationException>(() => new CompositeMacroMapping([good with { Exponent = double.NaN }]));
        foreach (var invalid in new CompositeMacroBinding[] {
            new(amplitude, 0, 2), new(waveform, 0, 1), new(new(Guid.NewGuid(), Guid.NewGuid()), 0, 1) })
        {
            var mapping = new CompositeMacroMapping([good, invalid]);
            Assert.Throws<GraphValidationException>(() => graph.ApplyCompositeMacro(mapping, 0.5));
            Assert.Equal(440, graph.GetParameter(frequency).BaseValue);
        }
        Assert.Throws<GraphValidationException>(() => graph.ApplyCompositeMacro(new([good]), double.NaN));
        Assert.Throws<GraphValidationException>(() => new SignalGraph().ApplyCompositeMacro(new([good]), 0.5));
    }

    [Fact]
    public void SessionMacroChangesTwoChildrenInOneRevisionAndUndo()
    {
        var registry = BuiltInNodes.CreateRegistry();
        registry.Register(CompositeNode.NodeType, id => new CompositeNode(id, "Test", [], []));
        using var session = new PatchSession(registry);
        var composite = session.Execute(new AddNodeCommand(session.GraphId, CompositeNode.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var child = composite.Composite!.Graph.Id;
        var first = session.Execute(new AddNodeCommand(child, OscillatorGenerator.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var second = session.Execute(new AddNodeCommand(child, OscillatorGenerator.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var a = new ParameterAddress(first.Id, first.Parameters.Single(p => p.Definition.Key == "frequency").Id);
        var b = new ParameterAddress(second.Id, second.Parameters.Single(p => p.Definition.Key == "frequency").Id);
        var before = session.Snapshot;
        var command = new ApplyCompositeMacroCommand(child, new([new(a, 100, 200), new(b, 200, 400)]), 1);
        var result = session.Execute(command);
        Assert.Equal(before.Revision + 1, session.Snapshot.Revision);
        Assert.Equal(2, result.ChangeSet!.Events.OfType<ParameterChanged>().Count());
        Assert.Equal(200, Value(a));
        Assert.Equal(400, Value(b));
        Assert.False(session.Execute(command).Changed);
        session.Undo();
        Assert.Equal(440, Value(a));
        Assert.Equal(440, Value(b));
        session.Redo();
        Assert.Equal(200, Value(a));
        Assert.Equal(400, Value(b));

        double Value(ParameterAddress address) => session.Snapshot.GetGraph(child).GetNode(address.NodeId)
            .Parameters.Single(p => p.Id == address.ParameterId).BaseValue;
    }
}

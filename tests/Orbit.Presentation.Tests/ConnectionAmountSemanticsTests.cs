using Orbit.Application.Graphs;
using Orbit.Application.Models;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class ConnectionAmountSemanticsTests
{
    [Fact]
    public void DirectModulationUsesTargetParameterWhileAudioIsDimensionless()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings());
        var graph = session.Snapshot;
        var modulation = ConnectionAmountSemantics.Resolve(graph, graph.Connections[0]);
        Assert.True(modulation.CanEdit);
        Assert.True(modulation.IsModulation);
        Assert.Equal(ParameterUnit.Hertz, modulation.TargetParameter!.Definition.Unit);
        var audio = ConnectionAmountSemantics.Resolve(graph, graph.Connections[1]);
        Assert.True(audio.CanEdit);
        Assert.False(audio.IsModulation);
        Assert.Null(audio.TargetParameter);
    }

    [Theory]
    [InlineData(SignalType.Trigger)]
    [InlineData(SignalType.Gate)]
    [InlineData(SignalType.Clock)]
    public void EventDestinationsAreNotEditable(SignalType type)
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings());
        var graph = session.Snapshot;
        var edge = graph.Connections[1];
        var node = graph.GetNode(edge.Destination.NodeId);
        var eventNode = node with
        {
            Inputs = node.Inputs.Select(port => port.Id == edge.Destination.PortId
                ? port with { SignalType = type } : port).ToArray()
        };
        // Projection-only fixture: substitute the destination type to exercise
        // all event categories without mutating or validating a real graph.
        var fixture = graph with { Nodes = graph.Nodes.Select(n => n.Id == node.Id ? eventNode : n).ToArray() };
        var semantics = ConnectionAmountSemantics.Resolve(fixture, edge);
        Assert.Equal(type, semantics.DestinationType);
        Assert.True(semantics.IsEvent);
        Assert.False(semantics.CanEdit);
    }
}

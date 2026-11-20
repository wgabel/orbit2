using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Graphs;

namespace Orbit.Application.Graphs.Commands;

/// <summary>
/// Translates typed commands into existing validated domain operations.
/// Contains no notification logic, graph ownership, UI or audio processing.
/// Each command is one domain mutation; compound node removal is atomic.
/// </summary>
internal sealed class GraphCommandHandler(NodeRegistry registry)
{
    public IReadOnlyList<IGraphEvent> Execute(SignalGraph graph, IGraphCommand command, long revision) =>
        command switch
        {
            AddNodeCommand add => AddNode(graph, add, revision),
            RemoveNodeCommand remove => RemoveNode(graph, remove, revision),
            ConnectNodesCommand connect => Connect(graph, connect),
            RemoveConnectionCommand disconnect => RemoveConnection(graph, disconnect),
            SetParameterValueCommand set => SetParameterValue(graph, set),
            _ => throw new NotSupportedException($"Unknown graph command: {command.GetType().Name}.")
        };

    private IReadOnlyList<IGraphEvent> AddNode(SignalGraph graph, AddNodeCommand command, long revision)
    {
        var node = registry.Create(command.NodeType);
        var snapshot = GraphNodeSnapshot.Capture(node, revision);
        graph.AddNode(node);
        return [new NodeAdded(graph.Id, snapshot)];
    }

    private static IReadOnlyList<IGraphEvent> RemoveNode(SignalGraph graph, RemoveNodeCommand command, long revision)
    {
        var node = GraphNodeSnapshot.Capture(graph.GetNode(command.NodeId), revision);
        var events = graph.Connections
            .Where(connection => connection.Source.NodeId == node.Id || connection.Destination.NodeId == node.Id)
            .Select(connection => (IGraphEvent)new ConnectionRemoved(graph.Id, connection))
            .ToList();
        events.Add(new NodeRemoved(graph.Id, node));

        // Domain operation removes the node and every incident connection together.
        graph.RemoveNode(node.Id);
        return events;
    }

    private static IReadOnlyList<IGraphEvent> Connect(SignalGraph graph, ConnectNodesCommand command)
    {
        var connection = graph.Connect(command.Source, command.Destination, command.Amount);
        return [new ConnectionCreated(graph.Id, connection)];
    }

    private static IReadOnlyList<IGraphEvent> RemoveConnection(SignalGraph graph, RemoveConnectionCommand command)
    {
        var connection = graph.GetConnection(command.ConnectionId);
        graph.RemoveConnection(connection.Id);
        return [new ConnectionRemoved(graph.Id, connection)];
    }

    private static IReadOnlyList<IGraphEvent> SetParameterValue(SignalGraph graph, SetParameterValueCommand command)
    {
        var previous = graph.GetParameter(command.Address).BaseValue;
        if (previous == command.Value)
            return [];

        graph.SetParameterValue(command.Address, command.Value);
        return [new ParameterChanged(graph.Id, command.Address, previous, command.Value)];
    }
}

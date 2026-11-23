using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Presets;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Ports;

namespace Orbit.Application.Graphs.Commands;

/// <summary>
/// Translates typed commands into existing validated domain operations.
/// Contains no notification logic, graph ownership, UI or audio processing.
/// Each command is one domain mutation; node removal and boundary edits are atomic.
/// </summary>
internal sealed class GraphCommandHandler(NodeRegistry registry)
{
    private readonly Lazy<NodePresetValidator> presetValidator = new(() => new NodePresetValidator(registry));

    public IReadOnlyList<IGraphEvent> Execute(SignalGraph graph, IGraphCommand command, long revision) =>
        command switch
        {
            AddNodeCommand add => AddNode(graph, add, revision),
            DuplicateNodeCommand duplicate => DuplicateNode(graph, duplicate, revision),
            RemoveNodeCommand remove => RemoveNode(graph, remove, revision),
            ConnectNodesCommand connect => Connect(graph, connect),
            RemoveConnectionCommand disconnect => RemoveConnection(graph, disconnect),
            SetConnectionAmountCommand amount => SetConnectionAmount(graph, amount),
            SetParameterValueCommand set => SetParameterValue(graph, set),
            SetPitchPatternStepsCommand set => SetPitchPatternSteps(graph, set),
            ApplyNodePresetCommand apply => ApplyNodePreset(graph, apply),
            SetNodeMutedCommand mute => SetNodeMuted(graph, mute),
            SetNodeBypassedCommand bypass => SetNodeBypassed(graph, bypass),
            AddBoundaryPortCommand add => BoundaryFacts(graph.Id, graph.AddBoundaryPort(add.Direction,
                new ExposedPort(Guid.NewGuid(), add.Name, add.SignalType, add.ChannelCount, add.FanIn))),
            RenameBoundaryPortCommand rename => BoundaryFacts(graph.Id,
                graph.RenameBoundaryPort(rename.Direction, rename.PortId, rename.Name)),
            MoveBoundaryPortCommand move => BoundaryFacts(graph.Id,
                graph.MoveBoundaryPort(move.Direction, move.PortId, move.Index)),
            RemoveBoundaryPortCommand remove => BoundaryFacts(graph.Id,
                graph.RemoveBoundaryPort(remove.Direction, remove.PortId)),
            _ => throw new NotSupportedException($"Unknown graph command: {command.GetType().Name}.")
        };

    private static IReadOnlyList<IGraphEvent> BoundaryFacts(Guid childGraphId, BoundaryPortEditResult? result)
    {
        if (result is null) return [];
        var facts = new List<IGraphEvent>();
        facts.AddRange(result.ParentConnections.Select(edge => new ConnectionRemoved(result.ParentGraphId, edge)));
        facts.AddRange(result.ChildConnections.Select(edge => new ConnectionRemoved(childGraphId, edge)));
        facts.Add(new BoundaryPortChanged(result.ParentGraphId, result.CompositeNodeId, result.Direction,
            result.Before, result.After, result.OldIndex, result.NewIndex));
        var rackDirection = result.Direction == PortDirection.Input ? PortDirection.Output : PortDirection.Input;
        facts.Add(new BoundaryPortChanged(childGraphId, result.RackNodeId, rackDirection,
            result.Before, result.After, result.OldIndex, result.NewIndex));
        return facts.AsReadOnly();
    }

    private IReadOnlyList<IGraphEvent> AddNode(SignalGraph graph, AddNodeCommand command, long revision)
    {
        var node = registry.Create(command.NodeType);
        var snapshot = GraphNodeSnapshot.Capture(node, revision);
        graph.AddNode(node);
        return [new NodeAdded(graph.Id, snapshot)];
    }

    private IReadOnlyList<IGraphEvent> DuplicateNode(SignalGraph graph, DuplicateNodeCommand command, long revision)
    {
        var source = GraphNodeSnapshot.Capture(graph.GetNode(command.NodeId), revision);
        var duplicate = NodeDuplicator.Duplicate(source, command.DuplicateId, registry);
        graph.AddNode(duplicate);
        return [new NodeAdded(graph.Id, GraphNodeSnapshot.Capture(duplicate, revision))];
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

    private IReadOnlyList<IGraphEvent> ApplyNodePreset(SignalGraph graph, ApplyNodePresetCommand command)
    {
        presetValidator.Value.Validate(command.Preset);
        return graph.SetNodeParameterValues(command.NodeId, command.Preset.NodeType, command.Preset.Parameters)
            .Select(change => (IGraphEvent)new ParameterChanged(graph.Id, change.Address,
                change.PreviousValue, change.CurrentValue)).ToArray();
    }

    private static IReadOnlyList<IGraphEvent> SetPitchPatternSteps(SignalGraph graph, SetPitchPatternStepsCommand command)
    {
        var node = graph.GetNode(command.NodeId);
        if (node is not Orbit.Domain.Nodes.Generators.NoteSequencerGenerator || command.Steps.Count != Orbit.Domain.Nodes.Generators.NoteSequencerGenerator.Capacity ||
            command.Steps.Any(code => code is < 0 or > 128)) throw new GraphValidationException("Invalid pitch pattern.");
        var values = node.Parameters.ToDictionary(p => p.Definition.Key, p => p.BaseValue);
        for (var i = 0; i < command.Steps.Count; i++) values[Orbit.Domain.Nodes.Generators.NoteSequencerGenerator.StepKey(i)] = command.Steps[i];
        return graph.SetNodeParameterValues(command.NodeId, node.TypeKey, values)
            .Select(change => (IGraphEvent)new ParameterChanged(graph.Id, change.Address, change.PreviousValue, change.CurrentValue)).ToArray();
    }

    private static IReadOnlyList<IGraphEvent> SetConnectionAmount(SignalGraph graph, SetConnectionAmountCommand command)
    {
        var previous = graph.GetConnection(command.ConnectionId);
        var current = graph.SetConnectionAmount(command.ConnectionId, command.Amount);
        return previous == current ? [] : [new ConnectionAmountChanged(graph.Id, previous, current)];
    }

    private static IReadOnlyList<IGraphEvent> SetNodeMuted(SignalGraph graph, SetNodeMutedCommand command)
    {
        var previous = graph.GetNode(command.NodeId).IsMuted;
        graph.SetNodeMuted(command.NodeId, command.Muted);
        return previous == command.Muted ? [] : [new NodeMutedChanged(graph.Id, command.NodeId, previous, command.Muted)];
    }

    private static IReadOnlyList<IGraphEvent> SetNodeBypassed(SignalGraph graph, SetNodeBypassedCommand command)
    {
        var previous = graph.GetNode(command.NodeId).IsBypassed;
        graph.SetNodeBypassed(command.NodeId, command.Bypassed);
        return previous == command.Bypassed ? [] : [new NodeBypassedChanged(graph.Id, command.NodeId, previous, command.Bypassed)];
    }
}

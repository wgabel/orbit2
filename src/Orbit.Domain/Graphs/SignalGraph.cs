using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Graphs;

/// <summary>
/// Owns topology and domain edits. Nodes know only their own sockets; this graph
/// owns all relationships. A failed edit leaves the graph unchanged.
/// </summary>
public sealed class SignalGraph
{
    private readonly List<ISignalNode> nodes = [];
    private readonly List<SignalConnection> connections = [];
    private readonly SignalGraphValidator validator = new();

    public SignalGraph() : this(Guid.NewGuid())
    {
    }

    public SignalGraph(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Graph ID cannot be empty.", nameof(id));

        Id = id;
        Nodes = nodes.AsReadOnly();
        Connections = connections.AsReadOnly();
    }

    internal SignalGraph(CompositeNode owner, Guid id) : this(id)
    {
        Owner = owner;
    }

    internal CompositeNode? Owner { get; }

    public Guid Id { get; }
    public IReadOnlyList<ISignalNode> Nodes { get; }
    public IReadOnlyList<SignalConnection> Connections { get; }

    /// <summary>Find an owned graph relative to this root. Detached children are not reachable.</summary>
    public SignalGraph GetGraph(Guid graphId) =>
        FindGraph(graphId) ?? throw new GraphValidationException($"Graph {graphId} does not exist in this patch.");

    private SignalGraph? FindGraph(Guid graphId)
    {
        if (Id == graphId) return this;
        foreach (var composite in nodes.OfType<ICompositeNode>())
            if (composite.InternalGraph.FindGraph(graphId) is { } found)
                return found;
        return null;
    }

    public ISignalNode GetNode(Guid nodeId) =>
        nodes.Find(node => node.Id == nodeId)
        ?? throw new GraphValidationException($"Node {nodeId} does not exist in this graph.");

    public SignalConnection GetConnection(Guid connectionId) =>
        connections.Find(candidate => candidate.Id == connectionId)
        ?? throw new GraphValidationException($"Connection {connectionId} does not exist.");

    public Parameter GetParameter(ParameterAddress address) =>
        GetNode(address.NodeId).Parameters
            .SingleOrDefault(candidate => candidate.Id == address.ParameterId)
        ?? throw new GraphValidationException($"Parameter {address.ParameterId} does not exist.");

    public void AddNode(ISignalNode node)
    {
        validator.ValidateNode(this, node);
        nodes.Add(node);
        if (node is SignalNode ownedNode)
            ownedNode.OwningGraph = this;
    }

    public void RemoveNode(Guid nodeId)
    {
        var node = GetNode(nodeId);
        if (node is InputRack or OutputRack)
            throw new GraphValidationException("Boundary racks cannot be removed independently of their composite.");

        // Remove every incident edge in the same domain operation.
        connections.RemoveAll(connection =>
            connection.Source.NodeId == nodeId || connection.Destination.NodeId == nodeId);
        nodes.Remove(node);
        if (node is SignalNode ownedNode)
            ownedNode.OwningGraph = null;
    }

    public SignalConnection Connect(PortAddress source, PortAddress destination, double amount = 1)
    {
        var connection = new SignalConnection(Guid.NewGuid(), source, destination, amount);
        AddConnection(connection);
        return connection;
    }

    public void AddConnection(SignalConnection connection)
    {
        validator.ValidateConnection(this, connection);
        connections.Add(connection);
    }

    public void RemoveConnection(Guid connectionId)
    {
        var connection = GetConnection(connectionId);

        connections.Remove(connection);
    }

    /// <summary>Replace only the signed coefficient. Endpoints, order and edge identity stay unchanged.</summary>
    public SignalConnection SetConnectionAmount(Guid connectionId, double amount)
    {
        var previous = GetConnection(connectionId);
        SignalGraphValidator.ValidateConnectionAmount(amount);
        if (previous.Amount == amount) return previous;
        var updated = previous with { Amount = amount };
        connections[connections.IndexOf(previous)] = updated;
        return updated;
    }

    public void SetParameterValue(ParameterAddress address, double value)
    {
        GetParameter(address).SetBaseValue(value);
    }

    public void SetNodeMuted(Guid nodeId, bool muted)
    {
        if (GetNode(nodeId) is not SignalNode { SupportsMute: true } node)
            throw new GraphValidationException("This node does not support mute.");
        node.IsMuted = muted;
    }

    public void SetNodeBypassed(Guid nodeId, bool bypassed)
    {
        if (GetNode(nodeId) is not SignalNode { BypassInputKey: not null } node)
            throw new GraphValidationException("This node does not support bypass.");
        node.IsBypassed = bypassed;
    }

    // These operations are addressed to the child graph; the editor resolves
    // its attached owner and parent. Only that boundary may be changed.
    public BoundaryPortEditResult AddBoundaryPort(PortDirection direction, ExposedPort port) =>
        new CompositeBoundaryEditor(this, direction).Add(port);

    public BoundaryPortEditResult? RenameBoundaryPort(PortDirection direction, Guid portId, string name) =>
        new CompositeBoundaryEditor(this, direction).Rename(portId, name);

    public BoundaryPortEditResult? MoveBoundaryPort(PortDirection direction, Guid portId, int index) =>
        new CompositeBoundaryEditor(this, direction).Move(portId, index);

    public BoundaryPortEditResult RemoveBoundaryPort(PortDirection direction, Guid portId) =>
        new CompositeBoundaryEditor(this, direction).Remove(portId);
}

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

    internal SignalGraph(CompositeNode owner) : this(Guid.NewGuid())
    {
        Owner = owner;
    }

    internal CompositeNode? Owner { get; }

    public Guid Id { get; }
    public IReadOnlyList<ISignalNode> Nodes { get; }
    public IReadOnlyList<SignalConnection> Connections { get; }

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

    public void SetParameterValue(ParameterAddress address, double value)
    {
        GetParameter(address).SetBaseValue(value);
    }
}

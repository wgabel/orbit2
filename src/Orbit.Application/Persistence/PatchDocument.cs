using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;

namespace Orbit.Application.Persistence;

public sealed record PatchDocument(int SchemaVersion, Guid PatchId, SavedGraph RootGraph,
    double BeatsPerMinute, Guid ActiveGraphId, SavedCanvas[] CanvasLayouts)
{
    public const int CurrentVersion = 1;
}

public sealed record SavedGraph(Guid Id, SavedNode[] Nodes, SignalConnection[] Connections);
public sealed record SavedNode(Guid Id, string TypeKey, SavedParameter[] Parameters,
    SavedPort[] Ports, SavedComposite? Composite)
{
    public bool IsMuted { get; init; }
}
public sealed record SavedParameter(string Key, Guid Id, double BaseValue);
public sealed record SavedPort(string Key, Guid Id);
public sealed record SavedComposite(string Name, Guid InputRackId, Guid OutputRackId,
    ExposedPort[] Inputs, ExposedPort[] Outputs, SavedGraph Graph);
public sealed record SavedCanvas(Guid GraphId, double PanX, double PanY, double Zoom, SavedNodePosition[] Positions);
public sealed record SavedNodePosition(Guid NodeId, double X, double Y);

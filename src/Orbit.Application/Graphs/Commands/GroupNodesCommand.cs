namespace Orbit.Application.Graphs.Commands;

/// <summary>Replace selected graph-local nodes with one editable composite.</summary>
public sealed record GroupNodesCommand(Guid GraphId, IReadOnlyList<Guid> NodeIds, string Name,
    long ExpectedRevision) : IGraphCommand;

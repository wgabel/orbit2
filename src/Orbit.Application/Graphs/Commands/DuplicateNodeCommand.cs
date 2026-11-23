namespace Orbit.Application.Graphs.Commands;

/// <summary>Copy a graph-local node with independent identities and no external wiring.</summary>
public sealed record DuplicateNodeCommand(Guid GraphId, Guid SourceNodeId, Guid NewNodeId) : IGraphCommand;

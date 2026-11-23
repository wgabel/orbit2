namespace Orbit.Application.Graphs.Commands;

/// <summary>Clone one node into the same graph with fresh identities and no parent-graph cables.</summary>
public sealed record DuplicateNodeCommand(Guid GraphId, Guid NodeId, Guid DuplicateId) : IGraphCommand;

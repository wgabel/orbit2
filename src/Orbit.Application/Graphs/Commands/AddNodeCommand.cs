namespace Orbit.Application.Graphs.Commands;

/// <summary>Create a node through the registered node factory.</summary>
public sealed record AddNodeCommand(Guid GraphId, string NodeType) : IGraphCommand;

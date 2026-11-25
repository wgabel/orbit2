using Orbit.Domain.Nodes.Composites;

namespace Orbit.Application.Graphs.Commands;

/// <summary>Apply a normalized mapping to the addressed composite child graph in one edit.</summary>
public sealed record ApplyCompositeMacroCommand(Guid GraphId, CompositeMacroMapping Mapping, double Value) : IGraphCommand;

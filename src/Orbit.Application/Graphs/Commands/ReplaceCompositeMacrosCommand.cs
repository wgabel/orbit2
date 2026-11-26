using Orbit.Domain.Nodes.Composites;

namespace Orbit.Application.Graphs.Commands;

/// <summary>Replace the complete macro configuration of one composite in its parent graph.</summary>
public sealed record ReplaceCompositeMacrosCommand(Guid GraphId, Guid NodeId,
    IReadOnlyList<CompositeMacroDefinition> Macros) : IGraphCommand
{
    /// <summary>Optional optimistic revision guard for reviewed editor drafts.</summary>
    public long? ExpectedRevision { get; init; }
}

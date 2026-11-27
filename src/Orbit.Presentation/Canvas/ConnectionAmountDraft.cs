using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Graphs;

namespace Orbit.Presentation.Canvas;

/// <summary>A single local slider gesture. Preview never owns a patch session or
/// dispatch callback; only successful completion can produce a commit request.</summary>
public sealed class ConnectionAmountDraft
{
    private readonly long workspaceGeneration;
    private readonly Guid graphId;
    private readonly SignalConnection original;
    private readonly double initialPosition;
    private bool finished;

    private ConnectionAmountDraft(long workspaceGeneration, Guid graphId, SignalConnection original,
        ConnectionAmountSliderRange range)
    {
        this.workspaceGeneration = workspaceGeneration;
        this.graphId = graphId;
        this.original = original;
        Range = range;
        // A native range snaps its thumb to the step grid even if the exact
        // authored coefficient is between steps. Returning to that thumb
        // position must not overwrite the authored value.
        var position = range.ToPosition(original.Amount);
        var step = ConnectionAmountSliderRange.PositionStep;
        var minimum = ConnectionAmountSliderRange.PositionMinimum;
        initialPosition = minimum + Math.Round((position - minimum) / step,
            MidpointRounding.AwayFromZero) * step;
        PreviewAmount = original.Amount;
    }

    public ConnectionAmountSliderRange Range { get; }
    public double PreviewAmount { get; private set; }
    public bool IsFinished => finished;

    public static ConnectionAmountDraft? Begin(long workspaceGeneration, GraphSnapshot graph,
        Guid connectionId, ConnectionAmountSliderRange? retainedRange = null)
    {
        var edge = graph.Connections.SingleOrDefault(candidate => candidate.Id == connectionId);
        if (edge is null) return null;
        var semantics = ConnectionAmountSemantics.Resolve(graph, edge);
        if (!semantics.CanEdit) return null;
        var range = retainedRange is not null && retainedRange.Contains(edge.Amount)
            ? retainedRange : ConnectionAmountSliderRange.Resolve(semantics, edge.Amount)!;
        return new(workspaceGeneration, graph.Id, edge, range);
    }

    /// <summary>Consume the native normalized rail value without graph mutations.</summary>
    public bool Preview(double position)
    {
        if (finished || !double.IsFinite(position) ||
            position < ConnectionAmountSliderRange.PositionMinimum ||
            position > ConnectionAmountSliderRange.PositionMaximum)
            return false;
        PreviewAmount = Math.Abs(position - initialPosition) < 1e-10
            ? original.Amount : Range.FromPosition(position);
        return true;
    }

    /// <summary>Call on external authoritative changes, not on unrelated view
    /// rerenders. Invalidate once and ignore subsequent stale native events.</summary>
    public bool Observe(long currentWorkspaceGeneration, GraphSnapshot graph)
    {
        if (finished) return false;
        if (ConnectionAmountCommit.Matches(currentWorkspaceGeneration, graph,
            workspaceGeneration, graphId, original)) return true;
        Cancel();
        return false;
    }

    public void Cancel()
    {
        finished = true;
        PreviewAmount = original.Amount;
    }

    public ConnectionAmountCommit? Complete()
    {
        if (finished) return null;
        finished = true;
        return PreviewAmount == original.Amount ? null :
            new(workspaceGeneration, graphId, original, PreviewAmount);
    }
}

/// <summary>Guarded request for the workspace's normal command path. Validate
/// against the current active graph and workspace generation immediately before
/// dispatch, rather than relying on a selected edge or a stale render.</summary>
public sealed record ConnectionAmountCommit(long WorkspaceGeneration, Guid GraphId,
    SignalConnection Original, double Amount)
{
    public SetConnectionAmountCommand? CreateCommand(long currentWorkspaceGeneration, GraphSnapshot graph)
        => double.IsFinite(Amount) && Amount != Original.Amount &&
           Matches(currentWorkspaceGeneration, graph, WorkspaceGeneration, GraphId, Original)
            ? new(GraphId, Original.Id, Amount) : null;

    internal static bool Matches(long currentWorkspaceGeneration, GraphSnapshot graph,
        long workspaceGeneration, Guid graphId, SignalConnection original)
    {
        if (currentWorkspaceGeneration != workspaceGeneration || graph.Id != graphId)
            return false;
        var edge = graph.Connections.SingleOrDefault(candidate => candidate.Id == original.Id);
        return edge == original && edge is not null &&
            ConnectionAmountSemantics.Resolve(graph, edge).CanEdit;
    }
}

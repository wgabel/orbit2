namespace Orbit.UI.Components.Canvas;

/// <summary>Browser UI-only measurements; nullable metrics indicate unavailable or not yet observed.</summary>
public sealed record EditorPerformanceSnapshot
{
    public string State { get; init; } = "unavailable";
    public double? WindowSeconds { get; init; }
    public int? FrameIntervals { get; init; }
    public double? Fps { get; init; }
    public double? MedianGapMs { get; init; }
    public double? P95GapMs { get; init; }
    public double? MaxGapMs { get; init; }
    public EditorLongEventSummary? LongTasks { get; init; }
    public EditorLongEventSummary? LongAnimationFrames { get; init; }
    public int? HistorySamples { get; init; }
    public double? HistoryLowestFps { get; init; }
    public double? HistoryWorstP95Ms { get; init; }
}

public sealed record EditorLongEventSummary
{
    public int Count { get; init; }
    public double TotalDurationMs { get; init; }
    public double? MaxDurationMs { get; init; }
    public double Over50Ms { get; init; }
}

namespace Orbit.Application.Models;

/// <summary>One passive, context-global diagnostic sample. Null means unavailable;
/// no metric describes measured DSP load or physical key-to-speaker latency.</summary>
public sealed record AudioPerformanceSnapshot(
    long SessionEpoch,
    string PlaybackState,
    string ContextState,
    double? SampleRateHz,
    double? BaseLatencyMs,
    double? OutputLatencyMs,
    double? EstimatedPipelineLatencyMs,
    long? UnderrunEvents,
    double? UnderrunDurationMs,
    double? PlaybackDurationMs,
    double? MissingAudioPercent)
{
    public BrowserRuntimeDiagnostics? Runtime { get; init; }
}

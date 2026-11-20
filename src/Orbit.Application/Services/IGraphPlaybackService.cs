using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Models;

namespace Orbit.Application.Services;

/// <summary>Presentation-facing graph playback contract. Start from a committed
/// snapshot; the task completes after playback ends or fails.</summary>
public interface IGraphPlaybackService
{
    Task InitializeAsync();
    Task PlayAsync(GraphSnapshot graph);
    Task StopAsync(Guid graphId);
    Task<PlaybackState> GetStateAsync(Guid graphId);
    /// <summary>Apply base edits in place or replace the running graph when routing changes.</summary>
    Task<GraphPlaybackUpdate> UpdateAsync(GraphSnapshot graph);
    /// <summary>Read one passive display snapshot; null when stopped, swapping or unavailable.</summary>
    Task<GraphParameterTelemetry?> GetTelemetryAsync(Guid graphId);
}

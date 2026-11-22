using Orbit.Application.Models;

namespace Orbit.Application.Interfaces;

/// <summary>Optional diagnostics port, independent of graph editing and playback commands.</summary>
public interface IAudioPerformanceSource
{
    /// <summary>Read existing context metrics only. Reset starts a local baseline;
    /// it must not reset the platform counters. Null indicates collection paused.</summary>
    Task<AudioPerformanceSnapshot?> GetPerformanceSnapshotAsync(bool reset, CancellationToken cancellationToken);
}

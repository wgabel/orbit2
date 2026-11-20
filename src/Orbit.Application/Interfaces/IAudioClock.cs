using Orbit.Domain.Timing;

namespace Orbit.Application.Interfaces;

/// <summary>
/// Read-only access to the existing shared audio clock. A null snapshot means
/// the context has not been started; querying it must not create audio.
/// </summary>
public interface IAudioClock
{
    Task<AudioClockSnapshot?> GetSnapshotAsync();
}

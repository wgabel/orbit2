namespace Orbit.Domain.Timing;

/// <summary>
/// A read-only observation of the shared audio context. Rendering uses the
/// engine's actual sample rate and frame clock; this is for application queries.
/// </summary>
public sealed record AudioClockSnapshot(long CurrentSample, int SampleRate, bool IsRunning)
{
    public double CurrentTime => (double)CurrentSample / SampleRate;
}

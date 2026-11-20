namespace Orbit.Application.Models;

/// <summary>
/// Framework-neutral description of a playback. The sound ID identifies an
/// active playback; the processor ID and parameters identify its DSP program.
/// Durations are expressed in milliseconds and volume is linear (0 to 1).
/// </summary>
public sealed record PlaybackRequest(
    string SoundId,
    string Processor,
    int DurationMilliseconds,
    double Volume,
    IReadOnlyDictionary<string, double> Parameters);

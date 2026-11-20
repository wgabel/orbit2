namespace Orbit.Application.Models;

/// <summary>
/// Framework-neutral description of a playback. The sound ID identifies an
/// active playback; the processor ID and parameters identify its DSP program.
/// Playback sustains until explicitly stopped. Volume is linear (0 to 1).
/// </summary>
public sealed record PlaybackRequest(
    string SoundId,
    string Processor,
    double Volume,
    IReadOnlyDictionary<string, double> Parameters);

namespace Orbit.Application.Models;

/// <summary>
/// Framework-neutral description of a playback. The sound ID identifies an
/// active playback; the processor ID and parameters identify its DSP program.
/// Playback sustains until explicitly stopped. Volume is linear (0 to 1).
/// Optional source selects runtime compilation instead of a registered precompiled processor.
/// </summary>
public sealed record PlaybackRequest(
    string SoundId,
    string Processor,
    double Volume,
    IReadOnlyDictionary<string, double> Parameters,
    string? Source = null,
    bool RequiresTransport = false,
    int OutputChannels = 1,
    Guid Generation = default,
    int TransportInputChannels = 0,
    IReadOnlyList<RhythmicInput>? RhythmicInputs = null,
    int TapSelectorCount = 0);

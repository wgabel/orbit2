namespace Orbit.Domain.Timing;

/// <summary>Immutable musical position sampled from the existing audio clock.</summary>
public sealed record TransportSnapshot(double BeatsPerMinute, double BeatPosition, bool IsPlaying, AudioClockSnapshot? Clock)
{
    public static TransportSnapshot Initial { get; } = new(MusicalTransport.DefaultTempo, 0, false, null);
    public long ResetGeneration { get; init; }

    public double GetFrequency(MusicalDivision division) =>
        MusicalTime.GetFrequency(BeatsPerMinute, MusicalTime.GetBeats(division));
}

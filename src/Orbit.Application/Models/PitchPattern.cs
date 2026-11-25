using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes.Generators;

namespace Orbit.Application.Models;

/// <summary>Immutable, bounded worklet configuration. Zero means Hold; note codes are MIDI + 1.</summary>
public sealed record PitchPattern(bool Loop, int Count, bool Free, int Ticks, double Seconds, IReadOnlyList<int> Steps,
    bool Notes = false, double GateFraction = 0.9, double Velocity = 0.8, IReadOnlyList<double>? GateFractions = null, IReadOnlyList<double>? VelocityOverrides = null,
    IReadOnlyList<double>? TimingOffsets = null)
{
    public static PitchPattern FromNode(GraphNodeSnapshot node)
    {
        var values = node.Parameters.ToDictionary(p => p.Definition.Key, p => p.BaseValue);
        return new(values[NoteSequencerGenerator.LoopKey] == 1, (int)values[NoteSequencerGenerator.CountKey],
            values[NoteSequencerGenerator.TimingKey] == 1,
            (int)(values[NoteSequencerGenerator.DivisionKey] * values[NoteSequencerGenerator.MultiplierKey]),
            values[NoteSequencerGenerator.SecondsKey], Array.AsReadOnly(Enumerable.Range(0, NoteSequencerGenerator.Capacity)
                .Select(i => (int)values[NoteSequencerGenerator.StepKey(i)]).ToArray()),
            values[NoteSequencerGenerator.ArticulationKey] == 1,
            values[NoteSequencerGenerator.GateLengthKey], values[NoteSequencerGenerator.VelocityKey],
            Array.AsReadOnly(Enumerable.Range(0, NoteSequencerGenerator.Capacity)
                .Select(i => values[NoteSequencerGenerator.StepGateLengthKey(i)]).ToArray()),
            Array.AsReadOnly(Enumerable.Range(0, NoteSequencerGenerator.Capacity)
                .Select(i => values[NoteSequencerGenerator.StepVelocityKey(i)]).ToArray()),
            Array.AsReadOnly(Enumerable.Range(0, NoteSequencerGenerator.Capacity)
                .Select(i => values[NoteSequencerGenerator.StepTimingOffsetKey(i)]).ToArray()));
    }

    public bool Matches(PitchPattern? other) => other is not null && Loop == other.Loop && Count == other.Count && Free == other.Free &&
        Ticks == other.Ticks && Seconds == other.Seconds && Notes == other.Notes &&
        GateFraction == other.GateFraction && Velocity == other.Velocity && Steps.SequenceEqual(other.Steps) &&
        (GateFractions ?? DefaultGateFractions).SequenceEqual(other.GateFractions ?? DefaultGateFractions) &&
        (VelocityOverrides ?? DefaultVelocityOverrides).SequenceEqual(other.VelocityOverrides ?? DefaultVelocityOverrides) &&
        (TimingOffsets ?? DefaultTimingOffsets).SequenceEqual(other.TimingOffsets ?? DefaultTimingOffsets);

    private static readonly IReadOnlyList<double> DefaultVelocityOverrides = Array.AsReadOnly(
        Enumerable.Repeat(NoteSequencerGenerator.InheritVelocity, NoteSequencerGenerator.Capacity).ToArray());
    private static readonly IReadOnlyList<double> DefaultTimingOffsets = Array.AsReadOnly(new double[NoteSequencerGenerator.Capacity]);
    private static readonly IReadOnlyList<double> DefaultGateFractions = Array.AsReadOnly(new double[NoteSequencerGenerator.Capacity]);
}

public sealed record PitchPatternUpdate(int Index, PitchPattern Pattern);

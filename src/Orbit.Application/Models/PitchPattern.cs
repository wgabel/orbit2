using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes.Generators;

namespace Orbit.Application.Models;

/// <summary>Immutable, bounded worklet configuration. Zero means Hold; note codes are MIDI + 1.</summary>
public sealed record PitchPattern(bool Loop, int Count, bool Free, int Ticks, double Seconds, IReadOnlyList<int> Steps)
{
    public static PitchPattern FromNode(GraphNodeSnapshot node)
    {
        var values = node.Parameters.ToDictionary(p => p.Definition.Key, p => p.BaseValue);
        return new(values[NoteSequencerGenerator.LoopKey] == 1, (int)values[NoteSequencerGenerator.CountKey],
            values[NoteSequencerGenerator.TimingKey] == 1,
            (int)(values[NoteSequencerGenerator.DivisionKey] * values[NoteSequencerGenerator.MultiplierKey]),
            values[NoteSequencerGenerator.SecondsKey], Array.AsReadOnly(Enumerable.Range(0, NoteSequencerGenerator.Capacity)
                .Select(i => (int)values[NoteSequencerGenerator.StepKey(i)]).ToArray()));
    }

    public bool Matches(PitchPattern? other) => other is not null && Loop == other.Loop && Count == other.Count && Free == other.Free &&
        Ticks == other.Ticks && Seconds == other.Seconds && Steps.SequenceEqual(other.Steps);
}

public sealed record PitchPatternUpdate(int Index, PitchPattern Pattern);

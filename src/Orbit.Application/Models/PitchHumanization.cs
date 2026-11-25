using Orbit.Domain.Nodes.Generators;

namespace Orbit.Application.Models;

public enum HumanizationTiming
{
    Centered,
    Ahead,
    Behind
}

/// <summary>Produces explicit, bounded slot edits; no runtime randomness or hidden playback state.</summary>
public sealed record PitchHumanization(HumanizationTiming Timing, int TimingPercent, int VelocityPercent, int Seed)
{
    public IReadOnlyDictionary<string, double> CreateChanges(PitchPattern pattern, int firstSlot, int slotCount)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (!Enum.IsDefined(Timing) || TimingPercent is < 0 or > NoteSequencerGenerator.MaximumTimingOffsetPercent ||
            VelocityPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(TimingPercent), "Invalid humanization profile.");
        if (pattern.Count is < 1 or > NoteSequencerGenerator.Capacity || firstSlot < 0 ||
            slotCount < 1 || firstSlot >= pattern.Count || slotCount > pattern.Count - firstSlot)
            throw new ArgumentOutOfRangeException(nameof(firstSlot), "Select a range inside the active pattern.");
        if (!pattern.Notes)
            throw new ArgumentException("Humanization requires Notes playback.", nameof(pattern));

        var changes = new Dictionary<string, double>();
        for (var slot = firstSlot; slot < firstSlot + slotCount; slot++)
        {
            if (pattern.Steps[slot] is < 1 or > 128) continue; // no new onsets
            if (TimingPercent != 0)
            {
                var delta = Timing switch
                {
                    HumanizationTiming.Ahead => -Magnitude(slot, 0, TimingPercent),
                    HumanizationTiming.Behind => Magnitude(slot, 0, TimingPercent),
                    _ => Signed(slot, 0, TimingPercent)
                };
                var previous = pattern.TimingOffsets?[slot] ?? 0;
                var bound = NoteSequencerGenerator.MaximumTimingOffsetPercent;
                var value = Math.Clamp((int)Math.Round(previous * 100) + delta, -bound, bound) / 100d;
                if (value != previous) changes.Add(NoteSequencerGenerator.StepTimingOffsetKey(slot), value);
            }
            if (VelocityPercent != 0)
            {
                var previous = pattern.VelocityOverrides?[slot] ?? NoteSequencerGenerator.InheritVelocity;
                // Explicitly silent notes must remain silent; inheritance is not silence.
                var effective = previous == NoteSequencerGenerator.InheritVelocity ? pattern.Velocity : previous;
                var delta = Signed(slot, 1, VelocityPercent);
                if (effective == 0 || delta == 0) continue;
                var value = Math.Clamp((int)Math.Round(effective * 100) + delta, 0, 100) / 100d;
                if (value != effective) changes.Add(NoteSequencerGenerator.StepVelocityKey(slot), value);
            }
        }
        return changes;
    }

    private int Magnitude(int slot, int stream, int limit) => 1 + (int)(Sample(slot, stream) % (uint)limit);
    private int Signed(int slot, int stream, int limit) => (int)(Sample(slot, stream) % (uint)(2 * limit + 1)) - limit;

    // Fixed unsigned integer mixing, keyed by absolute slot and independent stream.
    // Unlike Random, the sequence is independent of runtime version and selection order.
    private uint Sample(int slot, int stream)
    {
        unchecked
        {
            var value = (uint)Seed + 0x9e3779b9u * (uint)(slot + 1) + 0x85ebca6bu * (uint)(stream + 1);
            value = (value ^ (value >> 16)) * 0x7feb352du;
            value = (value ^ (value >> 15)) * 0x846ca68bu;
            return value ^ (value >> 16);
        }
    }
}

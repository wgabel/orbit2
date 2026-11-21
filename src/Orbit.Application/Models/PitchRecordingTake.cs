using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Timing;

namespace Orbit.Application.Models;

/// <summary>A transient monophonic take. Only an explicit commit becomes graph history.</summary>
public sealed class PitchRecordingTake
{
    private readonly int[] steps;
    private readonly long[] lastSamples;
    public PitchPattern Baseline { get; }
    public int Reference { get; }
    public int Spacing { get; }
    public long ResetGeneration { get; }
    private readonly int sampleRate;
    private readonly long startSample;
    public int RecordedSlots { get; private set; }

    public PitchRecordingTake(PitchPattern baseline, int reference, int spacing, bool replace, TransportSnapshot start)
    {
        if (!baseline.Loop || baseline.Count is < 1 or > NoteSequencerGenerator.Capacity ||
            spacing < 1 || baseline.Count % spacing != 0 || baseline.Steps.Count != NoteSequencerGenerator.Capacity)
            throw new ArgumentException("Recording requires a loop and quantization that divides its step count.");
        if (!start.IsPlaying || start.Clock is not { IsRunning: true })
            throw new ArgumentException("Recording requires a running audio clock.");
        Baseline = baseline with { Steps = Array.AsReadOnly(baseline.Steps.ToArray()) };
        Reference = reference;
        Spacing = spacing;
        ResetGeneration = start.ResetGeneration;
        sampleRate = start.Clock.SampleRate;
        startSample = start.Clock.CurrentSample;
        steps = Baseline.Steps.ToArray();
        if (replace) Array.Clear(steps, 0, baseline.Count); // retain hidden slots
        lastSamples = Enumerable.Repeat(-1L, steps.Length).ToArray();
    }

    public bool MatchesSource(PitchPattern pattern, int reference) => reference == Reference && Baseline.Matches(pattern);

    public bool IsCurrent(TransportSnapshot position) => position.IsPlaying && position.ResetGeneration == ResetGeneration &&
        position.Clock is { IsRunning: true } clock && clock.SampleRate == sampleRate && clock.CurrentSample >= startSample;

    public static int QuantizeSlot(PitchPattern pattern, int spacing, double position)
    {
        if (pattern.Count is < 1 or > NoteSequencerGenerator.Capacity || spacing < 1 || pattern.Count % spacing != 0 ||
            !double.IsFinite(position) || position < 0) throw new ArgumentOutOfRangeException(nameof(position));
        var duration = pattern.Free ? pattern.Seconds : pattern.Ticks / 4d;
        if (!double.IsFinite(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(pattern));
        // Work within the loop even on a long transport. Ties choose the later boundary.
        var loop = duration * pattern.Count;
        var nearest = (int)Math.Floor(position % loop / (duration * spacing) + .5);
        return nearest * spacing % pattern.Count;
    }

    public bool TryRecord(int note, TransportSnapshot position)
    {
        if (!IsCurrent(position) || !NoteSequencerGenerator.IsPlayable(note, Reference)) return false;
        var time = Baseline.Free ? position.ElapsedSeconds : position.BeatPosition;
        if (!double.IsFinite(time) || time < 0) return false;
        var slot = QuantizeSlot(Baseline, Spacing, time);
        var sample = position.Clock!.CurrentSample;
        // A delayed acknowledgement must not overwrite a newer onset at the same slot.
        if (sample < lastSamples[slot]) return false;
        if (lastSamples[slot] < 0) RecordedSlots++;
        lastSamples[slot] = sample;
        steps[slot] = note + 1;
        return true;
    }

    public int Step(int slot) => steps[slot];
    public IReadOnlyList<int> CaptureSteps() => Array.AsReadOnly(steps.ToArray());
}

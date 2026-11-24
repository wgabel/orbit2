using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Orbit.Domain.Timing;

namespace Orbit.Domain.Nodes.Generators;

/// <summary>Live or transport-locked continuous pitch. Fixed capacity keeps edits nonstructural.</summary>
public sealed class NoteSequencerGenerator : SignalNode, IGenerator
{
    public const string NodeType = "generator.note-sequencer";
    public const string ReferenceKey = "reference-note";
    public const string InitialPitchKey = "initial-pitch";
    public const double MaximumOffset = 60;
    public const int Capacity = 64;
    public const string LoopKey = "pattern-loop";
    public const string CountKey = "pattern-count";
    public const string TimingKey = "pattern-timing";
    public const string DivisionKey = "pattern-division";
    public const string MultiplierKey = "pattern-multiplier";
    public const string SecondsKey = "pattern-seconds";
    public const string GlideKey = "pattern-glide";
    public const string ArticulationKey = "pattern-articulation";
    public const string GateLengthKey = "pattern-gate-length";
    public const string VelocityKey = "pattern-velocity";
    public const int RestCode = 129;
    public static string StepKey(int index) => "pattern-step-" + (index + 1).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
    public static string StepGateLengthKey(int index) => "pattern-step-gate-length-" + (index + 1).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
    public static bool IsStepGateLengthParameter(string key) => key.StartsWith("pattern-step-gate-length-", StringComparison.Ordinal);
    public static bool IsPatternParameter(string key) => key.StartsWith("pattern-", StringComparison.Ordinal);

    private static ParameterDefinition Choice(string key, string name, int value, IReadOnlyList<ParameterChoice> choices) =>
        new(key, name, value, choices.Min(c => c.Value), choices.Max(c => c.Value), ParameterUnit.Ratio, ParameterScale.Linear, false, Smoothing: ParameterSmoothing.None) { Choices = choices };
    private static IReadOnlyList<ParameterChoice> Integers(int first, int count) => Array.AsReadOnly(Enumerable.Range(first, count).Select(n => new ParameterChoice(n, n.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray());
    private static IReadOnlyList<ParameterChoice> Notes { get; } = Array.AsReadOnly(new[] { new ParameterChoice(0, "Tie / Hold") }.Concat(Enumerable.Range(0, 128).Select(n => new ParameterChoice(n + 1, MusicalPitch.NoteName(n)))).Append(new ParameterChoice(RestCode, "Rest")).ToArray());
    public static IReadOnlyList<ParameterDefinition> PatternDefinitions { get; } = Array.AsReadOnly(new[] {
        Choice(LoopKey, "Pattern mode", 0, [new(0, "Live"), new(1, "Loop")]),
        Choice(CountKey, "Step count", 16, Integers(1, Capacity)),
        Choice(TimingKey, "Timing", 0, [new(0, "Synchronized"), new(1, "Free seconds")]),
        Choice(DivisionKey, "Step division", 1, [new(1, "1/16"), new(2, "1/8"), new(4, "1 beat"), new(8, "2 beats"), new(16, "1 bar")]),
        Choice(MultiplierKey, "Step multiplier", 1, Integers(1, 64)),
        new ParameterDefinition(SecondsKey, "Step seconds", 1, 0.01, 600, ParameterUnit.Seconds, ParameterScale.Linear, false, Smoothing: ParameterSmoothing.None),
        new ParameterDefinition(GlideKey, "Glide seconds", 0, 0, 600, ParameterUnit.Seconds, ParameterScale.Linear, false, Smoothing: ParameterSmoothing.None),
        Choice(ArticulationKey, "Playback", 0, [new(0, "Pitch only (drone)"), new(1, "Notes (gate + trigger)")]),
        new ParameterDefinition(GateLengthKey, "Note length", 0.9, 0.05, 1, ParameterUnit.Normalized, ParameterScale.Linear, false, Smoothing: ParameterSmoothing.None),
        new ParameterDefinition(VelocityKey, "Velocity", 0.8, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, false, Smoothing: ParameterSmoothing.None)
    }.Concat(Enumerable.Range(0, Capacity).Select(i => Choice(StepKey(i), $"Step {i + 1}", 0, Notes))).Concat(Enumerable.Range(0, Capacity).Select(i =>
        new ParameterDefinition(StepGateLengthKey(i), $"Step {i + 1} note length", 0, 0, 1, ParameterUnit.Normalized,
            ParameterScale.Linear, false, Smoothing: ParameterSmoothing.None))).ToArray());

    public NoteSequencerGenerator(Guid id) : base(id, NodeType, "Note Sequencer",
        new[] { new Parameter(Guid.NewGuid(), new ParameterDefinition(ReferenceKey, "Reference note", MusicalPitch.ReferenceMidiNote, 0, 127,
             ParameterUnit.Ratio, ParameterScale.Linear, false, Smoothing: ParameterSmoothing.None)
             { Choices = Array.AsReadOnly(Enumerable.Range(0, 128).Select(n => new ParameterChoice(n, MusicalPitch.NoteName(n))).ToArray()) }),
         new Parameter(Guid.NewGuid(), new ParameterDefinition(InitialPitchKey, "Initial pitch", 0, -MaximumOffset, MaximumOffset,
             ParameterUnit.Semitones, ParameterScale.Linear, false, Smoothing: ParameterSmoothing.Pitch)) }.Concat(PatternDefinitions.Select(d => new Parameter(Guid.NewGuid(), d))),
        [], [new PortDefinition(Guid.NewGuid(), "pitch-out", "Pitch", PortDirection.Output, SignalType.Modulation),
             new PortDefinition(Guid.NewGuid(), "gate-out", "Gate", PortDirection.Output, SignalType.Gate),
             new PortDefinition(Guid.NewGuid(), "trigger-out", "Trigger", PortDirection.Output, SignalType.Trigger),
             new PortDefinition(Guid.NewGuid(), "velocity-out", "Velocity", PortDirection.Output, SignalType.Modulation)]) { }

    public static bool IsPlayable(int note, int reference) => note is >= 0 and <= 127 && Math.Abs(note - reference) <= MaximumOffset;
}

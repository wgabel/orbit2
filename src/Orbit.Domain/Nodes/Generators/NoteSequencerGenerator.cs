using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Orbit.Domain.Timing;

namespace Orbit.Domain.Nodes.Generators;

/// <summary>First increment: live, latched semitone output. Pattern data and articulation are later increments.</summary>
public sealed class NoteSequencerGenerator : SignalNode, IGenerator
{
    public const string NodeType = "generator.note-sequencer";
    public const string ReferenceKey = "reference-note";
    public const string InitialPitchKey = "initial-pitch";
    public const double MaximumOffset = 60;

    public NoteSequencerGenerator(Guid id) : base(id, NodeType, "Note Sequencer",
        [new Parameter(Guid.NewGuid(), new ParameterDefinition(ReferenceKey, "Reference note", MusicalPitch.ReferenceMidiNote, 0, 127,
             ParameterUnit.Ratio, ParameterScale.Linear, false)
             { Choices = Array.AsReadOnly(Enumerable.Range(0, 128).Select(n => new ParameterChoice(n, MusicalPitch.NoteName(n))).ToArray()) }),
         new Parameter(Guid.NewGuid(), new ParameterDefinition(InitialPitchKey, "Initial pitch", 0, -MaximumOffset, MaximumOffset,
             ParameterUnit.Semitones, ParameterScale.Linear, false))],
        [], [new PortDefinition(Guid.NewGuid(), "pitch-out", "Pitch", PortDirection.Output, SignalType.Modulation)]) { }

    public static bool IsPlayable(int note, int reference) => note is >= 0 and <= 127 && Math.Abs(note - reference) <= MaximumOffset;
}

using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Orbit.Domain.Timing;

namespace Orbit.Domain.Nodes.Generators;

/// <summary>Transient monophonic keyboard actions; four sample-coherent control outputs.</summary>
public sealed class NoteSourceGenerator(Guid id) : SignalNode(id, NodeType, "Note Source",
    [new Parameter(Guid.NewGuid(), new ParameterDefinition(NoteSequencerGenerator.ReferenceKey, "Reference note", MusicalPitch.ReferenceMidiNote, 0, 127,
        ParameterUnit.Ratio, ParameterScale.Linear, false)
        { Choices = Array.AsReadOnly(Enumerable.Range(0, 128).Select(n => new ParameterChoice(n, MusicalPitch.NoteName(n))).ToArray()) }),
     new Parameter(Guid.NewGuid(), new ParameterDefinition(VelocityKey, "Keyboard velocity", .8, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, false))],
    [],
    [new PortDefinition(Guid.NewGuid(), "pitch-out", "Pitch", PortDirection.Output, SignalType.Modulation),
     new PortDefinition(Guid.NewGuid(), "gate-out", "Gate", PortDirection.Output, SignalType.Gate),
     new PortDefinition(Guid.NewGuid(), "trigger-out", "Trigger", PortDirection.Output, SignalType.Trigger),
     new PortDefinition(Guid.NewGuid(), "velocity-out", "Velocity", PortDirection.Output, SignalType.Modulation)]), IGenerator
{
    public const string NodeType = "generator.note-source";
    public const string VelocityKey = "keyboard-velocity";
}

namespace Orbit.Application.Models;

public enum PerformanceNoteKind { NoteOn, NoteOff, Reset }

/// <summary>Input contract shared by physical and screen keys; no graph edits or persisted held state.</summary>
public readonly record struct PerformanceNoteAction(PerformanceNoteKind Kind, int Note = 0, double Velocity = 1)
{
    public const int ResetCode = 129;
    public bool IsValid => Kind == PerformanceNoteKind.Reset ||
        Kind is PerformanceNoteKind.NoteOn or PerformanceNoteKind.NoteOff && Note is >= 0 and <= 127 &&
        double.IsFinite(Velocity) && Velocity is >= 0 and <= 1;
    public int Code => Kind == PerformanceNoteKind.Reset ? ResetCode : Note + 1;
    // Note lanes use a separate wire protocol: 1..128 + 512*velocity7, 130..257 off,
    // reset 129. A zero velocity note-on remains a valid strike (silent excitation).
    public int NoteCode => Kind switch
    {
        PerformanceNoteKind.Reset => ResetCode,
        PerformanceNoteKind.NoteOff => 130 + Note,
        _ => Note + 1 + 512 * (int)Math.Round(Velocity * 127)
    };
}

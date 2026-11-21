namespace Orbit.Application.Models;

public enum PerformanceNoteKind { NoteOn, NoteOff, Reset }

/// <summary>Input contract shared by physical and screen keys; no graph edits or persisted held state.</summary>
public readonly record struct PerformanceNoteAction(PerformanceNoteKind Kind, int Note = 0)
{
    public const int ResetCode = 129;
    public bool IsValid => Kind == PerformanceNoteKind.Reset ||
        Kind is PerformanceNoteKind.NoteOn or PerformanceNoteKind.NoteOff && Note is >= 0 and <= 127;
    public int Code => Kind == PerformanceNoteKind.Reset ? ResetCode : Note + 1;
}

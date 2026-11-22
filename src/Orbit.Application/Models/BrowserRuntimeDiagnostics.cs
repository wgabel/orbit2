namespace Orbit.Application.Models;

/// <summary>Context-global host counts; not musical voice count or DSP compute time.</summary>
public sealed record BrowserRuntimeDiagnostics(
    long Attempts, long Accepted, long Failed, long Superseded, long Cancelled, int Pending, long ProcessorErrors,
    int CurrentVoices, int RetiringVoices, int OutputChannels, int OccupiedAuxSlots, int CaptureLeases,
    RuntimeOperationDiagnostics? LastAttempt, RuntimeOperationDiagnostics? LastAccepted);

public sealed record RuntimeOperationDiagnostics(
    long Id, string Kind, long? Revision, Guid? Generation, string Result,
    double? WorkerRoundTripMs, double? WorkletCreateMs, double? ParameterBindingMs,
    double? AcceptanceMs, double? FadeDurationMs, double? CompletionMs);

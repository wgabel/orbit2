using Orbit.Application.Models;

namespace Orbit.Audio.Compilation;

/// <summary>Compiled source and host control values for one committed graph revision.
/// The source is independent of editable base values; the host applies those before playback.</summary>
public sealed record FaustGraphProgram(
    Guid GraphId, long Revision, string Source, IReadOnlyList<FaustParameterControl> Controls)
{
    public int ReachableLeafNodes { get; init; }
    public int BoundaryRoutes { get; init; }
    public int ReachableEdges { get; init; }
    public bool RequiresTransport { get; init; }
    public int TransportInputChannels { get; init; }
    public IReadOnlyList<RhythmicInput> RhythmicInputs { get; init; } = [];
    public SignalTapManifest SignalTaps { get; init; } = SignalTapManifest.Empty;
    public IReadOnlyList<FaustAuditionControl> AuditionControls { get; init; } = [];
    public IReadOnlyList<FaustEnvelopeMeter> EnvelopeMeters { get; init; } = [];
    public IReadOnlyList<FaustPitchMeter> PitchMeters { get; init; } = [];

    public Dictionary<string, double> InitialControlValues() => Controls
        .Select(control => (control.Name, control.InitialValue))
        .Concat(AuditionControls.Select(control => (control.Name, control.InitialValue)))
        .ToDictionary(control => control.Name, control => control.InitialValue);
}

public sealed record FaustEnvelopeMeter(Guid GraphId, Guid NodeId, string Name);
public sealed record FaustPitchMeter(Guid GraphId, Guid NodeId, string Name)
{
    public string StepName => Name.Replace("pitch_", "step_", StringComparison.Ordinal);
}

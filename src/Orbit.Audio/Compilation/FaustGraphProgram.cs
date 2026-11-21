using Orbit.Application.Models;

namespace Orbit.Audio.Compilation;

/// <summary>Compiled source and host control values for one committed graph revision.
/// The source is independent of editable base values; the host applies those before playback.</summary>
public sealed record FaustGraphProgram(
    Guid GraphId, long Revision, string Source, IReadOnlyList<FaustParameterControl> Controls)
{
    public bool RequiresTransport { get; init; }
    public int TransportInputChannels { get; init; }
    public IReadOnlyList<RhythmicInput> RhythmicInputs { get; init; } = [];
    public SignalTapManifest SignalTaps { get; init; } = SignalTapManifest.Empty;
    public IReadOnlyList<FaustAuditionControl> AuditionControls { get; init; } = [];

    public Dictionary<string, double> InitialControlValues() => Controls
        .Select(control => (control.Name, control.InitialValue))
        .Concat(AuditionControls.Select(control => (control.Name, control.InitialValue)))
        .ToDictionary(control => control.Name, control => control.InitialValue);
}

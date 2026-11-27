using Orbit.Application.Models;
using Orbit.Domain.Parameters;

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
    public IReadOnlyList<FaustMacroControl> MacroControls { get; init; } = [];
    public IReadOnlyList<FaustMacroTarget> MacroTargets { get; init; } = [];
    /// <summary>True only when this accepted program contains connected, consumed audio-rate macro controls.</summary>
    public bool HasActiveMacroModulation => MacroControls.Count > 0;
    public IReadOnlyList<FaustEnvelopeMeter> EnvelopeMeters { get; init; } = [];
    public IReadOnlyList<FaustPitchMeter> PitchMeters { get; init; } = [];

    public Dictionary<string, double> InitialControlValues() => Controls
        .Select(control => (control.Name, control.InitialValue))
        .Concat(MacroControls.Select(control => (control.Name, control.InitialValue)))
        .Concat(AuditionControls.Select(control => (control.Name, control.InitialValue)))
        .ToDictionary(control => control.Name, control => control.InitialValue);
}

public sealed record FaustMacroAddress(Guid GraphId, Guid CompositeId, Guid MacroId);
public sealed record FaustMacroControl(FaustMacroAddress Address, string Name, double InitialValue, string MeterName);
public sealed record FaustMacroTarget(Guid GraphId, ParameterAddress Address, string MeterName);

public sealed record FaustEnvelopeMeter(Guid GraphId, Guid NodeId, string Name);
public sealed record FaustPitchMeter(Guid GraphId, Guid NodeId, string Name)
{
    public string StepName => Name.Replace("pitch_", "step_", StringComparison.Ordinal);
}
8a0063b057812921487f929313d31a3d3e5d38dd

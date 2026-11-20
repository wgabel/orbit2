namespace Orbit.Audio.Compilation;

/// <summary>Compiled source and host control values for one committed graph revision.
/// The source is independent of editable base values; the host applies those before playback.</summary>
public sealed record FaustGraphProgram(
    Guid GraphId, long Revision, string Source, IReadOnlyList<FaustParameterControl> Controls)
{
    public bool RequiresTransport { get; init; }
}

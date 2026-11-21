namespace Orbit.Audio.Compilation;

/// <summary>Host-owned gain switch, shared by every output of the same editable node.</summary>
public sealed record FaustMuteControl(Guid GraphId, Guid NodeId, string Name, double InitialValue);

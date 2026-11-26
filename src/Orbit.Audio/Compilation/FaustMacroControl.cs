namespace Orbit.Audio.Compilation;

/// <summary>Address of an authored macro, distinct from a leaf parameter.</summary>
public readonly record struct MacroControlAddress(Guid GraphId, Guid CompositeId, Guid MacroId);

/// <summary>Writable normalized base and separate passive normalized effective meter.</summary>
public sealed record FaustMacroControl(MacroControlAddress Address, string Name, double InitialValue, string MeterName);

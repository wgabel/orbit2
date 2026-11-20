namespace Orbit.Audio.Compilation;

/// <summary>A graph cannot be represented by the currently supported Faust backend.</summary>
public sealed class FaustCompilationException(string message) : Exception(message) { }

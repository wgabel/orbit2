namespace Orbit.Audio.Compilation;

/// <summary>Backend strategy for a node type with one mono output.
/// Shared routing and parameter handling belong to the compilation context.</summary>
public interface IFaustNodeEmitter
{
    string TypeKey { get; }
    string Emit(FaustNodeContext context);
}

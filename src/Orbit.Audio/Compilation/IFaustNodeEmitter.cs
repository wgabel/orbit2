using Orbit.Audio.Analysis;
using Orbit.Domain.Parameters;

namespace Orbit.Audio.Compilation;

/// <summary>Backend strategy for a node type with one mono output.
/// Shared routing and parameter handling belong to the compilation context.</summary>
public interface IFaustNodeEmitter
{
    string TypeKey { get; }
    string Emit(FaustNodeContext context);
    /// <summary>Conservative output bounds; null when the node has no known bound.</summary>
    ParameterRange? EstimateOutputRange(SignalRangeContext context) => null;
    ParameterRange? EstimateOutputRange(SignalRangeContext context, string portKey) => EstimateOutputRange(context);
}

/// <summary>The emitted expression is a tuple in the node's declared output order.</summary>
public interface IFaustMultiOutputEmitter : IFaustNodeEmitter;

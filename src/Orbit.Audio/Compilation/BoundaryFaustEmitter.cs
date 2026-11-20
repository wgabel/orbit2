using Orbit.Audio.Analysis;
using Orbit.Domain.Parameters;

namespace Orbit.Audio.Compilation;

/// <summary>Internal binding between mirrored sockets; no oscillator, extra
/// sample delay or editable parameter is introduced at a graph boundary.</summary>
internal sealed class BoundaryFaustEmitter : IFaustNodeEmitter
{
    public const string NodeType = "backend.boundary-route";
    public string TypeKey => NodeType;
    public string Emit(FaustNodeContext context) => context.Input("signal-in");
    public ParameterRange? EstimateOutputRange(SignalRangeContext context) => context.Input("signal-in");
}

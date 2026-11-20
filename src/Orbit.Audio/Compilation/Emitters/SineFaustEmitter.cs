using Orbit.Domain.Nodes.Generators;
using Orbit.Audio.Analysis;
using Orbit.Domain.Parameters;

namespace Orbit.Audio.Compilation.Emitters;

/// <summary>An audio-rate oscillator; the same expression serves an LFO or a voice.</summary>
public sealed class SineFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => SineGenerator.NodeType;

    public ParameterRange? EstimateOutputRange(SignalRangeContext context) =>
        SignalRangeContext.Multiply(new ParameterRange(-1, 1), context.Parameter("amplitude"));

    public string Emit(FaustNodeContext context)
    {
        var frequency = context.Parameter("frequency", "0.49 * ma.SR");
        var amplitude = context.Parameter("amplitude");
        return $"os.osc({frequency}) * {amplitude}";
    }
}

using Orbit.Audio.Analysis;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Parameters;

namespace Orbit.Audio.Compilation.Emitters;

public sealed class NoiseFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => NoiseGenerator.NodeType;

    public ParameterRange? EstimateOutputRange(SignalRangeContext context) =>
        SignalRangeContext.Multiply(new ParameterRange(-1, 1), context.Parameter("amplitude"));

    public string Emit(FaustNodeContext context)
    {
        var color = (NoiseColor)context.ConstantParameter("color");
        var amplitude = context.Parameter("amplitude", settlingMilliseconds: FaustControlSmoothing.Amplitude);
        return $"({FaustNoiseSpectra.Emit(color, context.Node.Id)}) * {amplitude}";
    }
}

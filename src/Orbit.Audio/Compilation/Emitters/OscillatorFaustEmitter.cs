using Orbit.Domain.Nodes.Generators;
using Orbit.Audio.Analysis;
using Orbit.Domain.Parameters;

namespace Orbit.Audio.Compilation.Emitters;

/// <summary>Selects a single waveform implementation at compile time. Frequency and phase
/// remain audio-rate parameters, and every waveform shares the same normalized output bounds.</summary>
public sealed class OscillatorFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => OscillatorGenerator.NodeType;

    public ParameterRange? EstimateOutputRange(SignalRangeContext context) =>
        SignalRangeContext.Multiply(new ParameterRange(-1, 1), context.Parameter("amplitude"));

    public string Emit(FaustNodeContext context)
    {
        var timing = FaustOscillatorTiming.Resolve(context);
        var amplitude = context.Parameter("amplitude");
        var phase = context.Parameter("phase");
        var waveform = (OscillatorWaveform)context.ConstantParameter("waveform");
        return $"({FaustOscillatorWaveforms.Emit(waveform, timing, phase)}) * {amplitude}";
    }
}

using Orbit.Domain.Nodes.Generators;

namespace Orbit.Audio.Compilation.Emitters;

/// <summary>An audio-rate oscillator; the same expression serves an LFO or a voice.</summary>
public sealed class SineFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => SineGenerator.NodeType;

    public string Emit(FaustNodeContext context)
    {
        var frequency = context.Parameter("frequency");
        var amplitude = context.Parameter("amplitude");
        return $"os.osc(min(0.49 * ma.SR, {frequency})) * {amplitude}";
    }
}

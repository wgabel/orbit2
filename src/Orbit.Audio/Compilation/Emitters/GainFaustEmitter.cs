using Orbit.Domain.Nodes.Processors;

namespace Orbit.Audio.Compilation.Emitters;

/// <summary>A gain stage multiplies its incoming audio by its modulated gain.</summary>
public sealed class GainFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => GainProcessor.NodeType;

    public string Emit(FaustNodeContext context) =>
        $"{context.Input("audio-in")} * {context.Parameter("gain")}";
}

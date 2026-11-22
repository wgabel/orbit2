using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Timing;

namespace Orbit.Audio.Compilation.Emitters;

public sealed class ResonatorFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => ResonatorProcessor.NodeType;

    public string Emit(FaustNodeContext context)
    {
        var pitch = context.Parameter(ResonatorProcessor.PitchKey, initializeAtTarget: true);
        var ratio = $"pow(2.0, {pitch} / {FaustNumber.Format(MusicalPitch.SemitonesPerOctave)})";
        return FaustDampedMode.Emit(context.Input("audio-in"),
            context.Parameter("frequency", "0.49 * ma.SR", ratio, initializeAtTarget: true),
            context.Parameter("decay", initializeAtTarget: true),
            context.Parameter("excitation", initializeAtTarget: true),
            context.Parameter("output-gain", initializeAtTarget: true),
            context.Parameter("mix", initializeAtTarget: true));
    }
}

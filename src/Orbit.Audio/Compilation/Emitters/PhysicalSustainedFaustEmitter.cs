using Orbit.Domain.Nodes.Generators;

namespace Orbit.Audio.Compilation.Emitters;

public sealed class PhysicalSustainedFaustEmitter(PhysicalSustainedKind kind) : IFaustNodeEmitter
{
    public string TypeKey => PhysicalSustainedGenerator.TypeFor(kind);
    public string Emit(FaustNodeContext c) => FaustSustainedInstruments.Emit(kind,
        c.Parameter("pitch", initializeAtTarget: true), c.Input("gate-in"),
        c.Parameter("expression", initializeAtTarget: true), c.Parameter("character", initializeAtTarget: true),
        c.Parameter("attack", initializeAtTarget: true), c.Parameter("release", initializeAtTarget: true),
        c.Parameter("vibrato-depth", initializeAtTarget: true), c.Parameter("vibrato-rate", initializeAtTarget: true),
        c.Parameter("output-gain", initializeAtTarget: true));
}

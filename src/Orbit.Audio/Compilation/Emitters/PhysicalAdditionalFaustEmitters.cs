using Orbit.Domain.Nodes.Generators;

namespace Orbit.Audio.Compilation.Emitters;

public sealed class PhysicalViolinFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => PhysicalViolinGenerator.NodeType;
    public string Emit(FaustNodeContext c) => FaustAdditionalPhysicalInstruments.Violin(
        c.Parameter("pitch", initializeAtTarget: true), c.Input("gate-in"), c.Parameter("expression", initializeAtTarget: true),
        c.Parameter("bow-pressure", initializeAtTarget: true), c.Parameter("bow-position", initializeAtTarget: true),
        c.Parameter("attack", initializeAtTarget: true), c.Parameter("release", initializeAtTarget: true),
        c.Parameter("vibrato-depth", initializeAtTarget: true), c.Parameter("vibrato-rate", initializeAtTarget: true),
        c.Parameter("output-gain", initializeAtTarget: true));
}
public sealed class PhysicalBrassFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => PhysicalBrassGenerator.NodeType;
    public string Emit(FaustNodeContext c) => FaustAdditionalPhysicalInstruments.Brass(
        c.Parameter("pitch", initializeAtTarget: true), c.Input("gate-in"), c.Parameter("expression", initializeAtTarget: true),
        c.Parameter("lip-tension", initializeAtTarget: true), c.Parameter("mute-opening", initializeAtTarget: true),
        c.Parameter("attack", initializeAtTarget: true), c.Parameter("release", initializeAtTarget: true),
        c.Parameter("vibrato-depth", initializeAtTarget: true), c.Parameter("vibrato-rate", initializeAtTarget: true),
        c.Parameter("output-gain", initializeAtTarget: true));
}
public sealed class PhysicalElectricGuitarFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => PhysicalElectricGuitarGenerator.NodeType;
    public string Emit(FaustNodeContext c) => FaustAdditionalPhysicalInstruments.ElectricGuitar(
        c.Parameter("pitch", initializeAtTarget: true), c.Input("trigger-in"), c.Parameter("strength", initializeAtTarget: true),
        c.Parameter("pluck-position", initializeAtTarget: true), c.Parameter("mute", initializeAtTarget: true),
        c.Parameter("output-gain", initializeAtTarget: true));
}

using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Processors;

namespace Orbit.Audio.Compilation.Emitters;

public sealed class PhysicalPluckFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => PhysicalPluckGenerator.NodeType;
    public string Emit(FaustNodeContext c) => FaustPhysicalInstruments.Pluck(
        c.Parameter("pitch", initializeAtTarget: true), c.Input("trigger-in"), c.Parameter("strength", initializeAtTarget: true),
        c.Parameter("decay", initializeAtTarget: true), c.Input("excitation-in"), c.Parameter("output-gain", initializeAtTarget: true));
}

public sealed class DrawbarOrganFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => DrawbarOrganGenerator.NodeType;
    public string Emit(FaustNodeContext c) => FaustPhysicalInstruments.Organ(
        c.Parameter("pitch", initializeAtTarget: true), c.Input("gate-in"), c.Input("trigger-in"),
        c.Parameter("swell", initializeAtTarget: true), c.Parameter("key-click", initializeAtTarget: true),
        c.Parameter("percussion", initializeAtTarget: true), DrawbarOrganGenerator.Drawbars.Select(k => c.Parameter("drawbar-" + k, initializeAtTarget: true)).ToArray());
}

public sealed class PhysicalMarimbaFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => PhysicalMarimbaGenerator.NodeType;
    public string Emit(FaustNodeContext c) => FaustPhysicalInstruments.Marimba(
        c.Parameter("pitch", initializeAtTarget: true), c.Input("trigger-in"), c.Parameter("strength", initializeAtTarget: true),
        c.Parameter("hardness", initializeAtTarget: true), c.Parameter("position", initializeAtTarget: true),
        c.Parameter("decay", initializeAtTarget: true), c.Parameter("output-gain", initializeAtTarget: true));
}

public sealed class RotarySpeakerFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => RotarySpeakerProcessor.NodeType;
    public string Emit(FaustNodeContext c) => FaustPhysicalInstruments.Rotary(c.Input("audio-in"),
        c.Parameter("speed"), c.Parameter("am-depth", initializeAtTarget: true), c.Parameter("doppler-depth", initializeAtTarget: true),
        c.Parameter("mix", initializeAtTarget: true));
}

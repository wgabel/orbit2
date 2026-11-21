using Orbit.Audio.Compilation.Emitters;
using Orbit.Domain.Nodes.Generators;

namespace Orbit.Audio.Compilation;

/// <summary>One registry of DSP strategies and their optional signal-bound rules.</summary>
public static class BuiltInFaustEmitters
{
    public static IReadOnlyList<IFaustNodeEmitter> Create() => [new OscillatorFaustEmitter(), new PitchControlFaustEmitter(), new NoteSequencerFaustEmitter(), new EnvelopeFaustEmitter(), new RhythmicAdapterFaustEmitter(false), new RhythmicAdapterFaustEmitter(true), new RhythmicSourceFaustEmitter(ManualTriggerGenerator.NodeType), new RhythmicSourceFaustEmitter(GateGenerator.NodeType), new RhythmicSourceFaustEmitter(ClockGenerator.NodeType), new NoiseFaustEmitter(), new GainFaustEmitter(), new MixerFaustEmitter(), new FilterFaustEmitter(), new ParametricEqFaustEmitter(), new DelayFaustEmitter(), new ReverbFaustEmitter(), new ChorusFaustEmitter()];
}

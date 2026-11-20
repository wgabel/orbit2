using Orbit.Audio.Compilation.Emitters;

namespace Orbit.Audio.Compilation;

/// <summary>One registry of DSP strategies and their optional signal-bound rules.</summary>
public static class BuiltInFaustEmitters
{
    public static IReadOnlyList<IFaustNodeEmitter> Create() => [new OscillatorFaustEmitter(), new NoiseFaustEmitter(), new GainFaustEmitter(), new MixerFaustEmitter(), new FilterFaustEmitter(), new ParametricEqFaustEmitter()];
}

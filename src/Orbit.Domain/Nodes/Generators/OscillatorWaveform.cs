namespace Orbit.Domain.Nodes.Generators;

/// <summary>Stable choice values for a periodic oscillator, independent of DSP implementation.</summary>
public enum OscillatorWaveform
{
    Sine = 0,
    Triangle = 1,
    Square = 2,
    Sawtooth = 3
}

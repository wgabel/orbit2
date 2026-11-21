using Orbit.Application.Models;
using Orbit.Audio.Compilation;

namespace Orbit.Audio.Analysis;

/// <summary>Exact stationary transfer magnitude of the emitted TPT SVF.
/// The bilinear transform maps s/g to j*tan(pi*f/fs)/tan(pi*fc/fs).
/// Multiply through by (g*cos(pi*f/fs))² to keep Nyquist well-conditioned.</summary>
public static class StateVariableResponse
{
    public static double EffectiveCenter(ProcessorResponseParameters parameters, int sampleRate) =>
        Math.Min(parameters.Frequency, FaustStateVariableFilter.MaximumCutoffRatio * sampleRate);

    public static double Magnitude(ProcessorResponseParameters parameters, int sampleRate, double frequency)
    {
        if (sampleRate <= 2 * ProcessorResponseCapabilities.MinimumFrequency || !double.IsFinite(frequency) ||
            frequency < 0 || frequency > sampleRate / 2.0)
            throw new ArgumentOutOfRangeException(nameof(frequency));
        if (!double.IsFinite(parameters.Frequency) || parameters.Frequency <= 0 ||
            !double.IsFinite(parameters.Q) || parameters.Q <= 0 || !double.IsFinite(parameters.GainDb))
            throw new ArgumentOutOfRangeException(nameof(parameters));

        var g = Math.Tan(Math.PI * EffectiveCenter(parameters, sampleRate) / sampleRate);
        var angle = Math.PI * frequency / sampleRate;
        var p = Math.Sin(angle);
        var q = g * Math.Cos(angle);
        var real = q * q - p * p;
        var band = p * q;
        var peakGain = parameters.Kind == ProcessorResponseKind.Peaking ? Math.Pow(10, parameters.GainDb / 40) : 1;
        var k = 1 / (parameters.Q * peakGain);
        var imaginary = k * band;
        var denominator = Math.Sqrt(real * real + imaginary * imaginary);
        var numerator = parameters.Kind switch
        {
            ProcessorResponseKind.LowPass => q * q,
            ProcessorResponseKind.HighPass => p * p,
            ProcessorResponseKind.BandPass => band,
            ProcessorResponseKind.Notch => Math.Abs(real),
            // x + k*(A²-1)*band gives a numerator with damping k*A².
            ProcessorResponseKind.Peaking => Math.Sqrt(real * real + Math.Pow(imaginary * peakGain * peakGain, 2)),
            _ => throw new ArgumentOutOfRangeException(nameof(parameters))
        };
        return numerator / denominator;
    }

    public static double Decibels(ProcessorResponseParameters parameters, int sampleRate, double frequency) =>
        20 * Math.Log10(Magnitude(parameters, sampleRate, frequency));
}

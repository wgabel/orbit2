using Orbit.Domain.Nodes.Processors;

namespace Orbit.Application.Models;

/// <summary>Calculated transfer gain (not captured audio or absolute dBFS).</summary>
public enum ProcessorResponseKind { LowPass, HighPass, BandPass, Notch, Peaking }

public readonly record struct ProcessorResponseParameters(ProcessorResponseKind Kind, double Frequency, double Q, double GainDb = 0);
public readonly record struct ProcessorResponsePoint(double Frequency, double Decibels);
public sealed record ProcessorResponseSettings(ProcessorResponseParameters Configured, ProcessorResponseParameters? Effective);

public static class ProcessorResponseCapabilities
{
    public const int ReferenceSampleRate = 48000;
    public const double MinimumFrequency = 20;

    public static bool Supports(string typeKey) => typeKey is FilterProcessor.NodeType or ParametricEqProcessor.NodeType;
}

using System.Globalization;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Timing;

namespace Orbit.Presentation.Canvas;

/// <summary>Nominal cycle timing, not a prediction of downstream silence.</summary>
public sealed record CycleTiming(double Hertz, double PositiveFraction)
{
    public double Seconds => 1 / Hertz;
    public double PositiveSeconds => Seconds * PositiveFraction;
    public double NegativeSeconds => Seconds * (1 - PositiveFraction);

    public static CycleTiming? ForNode(GraphNodeSnapshot node, double bpm)
    {
        if (node.TypeKey != OscillatorGenerator.NodeType) return null;
        double Value(string key) => node.Parameters.Single(p => p.Definition.Key == key).BaseValue;
        var hz = Value("timing") == (int)OscillatorTimingMode.Free ? Value("frequency") :
            MusicalTime.GetFrequency(bpm, MusicalTime.GetBeats((MusicalDivision)Value("division")));
        var positive = Value("waveform") == (int)OscillatorWaveform.Square ? Value(OscillatorGenerator.PulseWidthKey) : 0.5;
        return double.IsFinite(hz) && hz > 0 ? new(hz, positive) : null;
    }

    public static CycleTiming? ForParameter(GraphNodeSnapshot node, ParameterSnapshot parameter, double bpm) =>
        parameter.Definition.Key is "frequency" or "division" && node.IsParameterActive(parameter) ? ForNode(node, bpm) : null;

    public static string FormatDuration(double seconds) => seconds >= 1 ? Format(seconds) + " s" :
        seconds >= 0.001 ? Format(seconds * 1000) + " ms" : Format(seconds * 1_000_000) + " µs";

    private static string Format(double value) => value.ToString("G4", CultureInfo.InvariantCulture);
}

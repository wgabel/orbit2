using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Models;
using Orbit.Domain.Nodes.Generators;

namespace Orbit.Presentation.Canvas;

public sealed record EnvelopePreviewPoint(double X, double Level);
public sealed record EnvelopePreviewSegment(EnvelopeStage Stage, double Start, double End, double? Milliseconds,
    double From, double To, double Curve)
{
    public double LevelAt(double progress) => Stage == EnvelopeStage.Attack
        ? From + (To - From) * Math.Pow(progress, Curve)
        : To + (From - To) * Math.Pow(1 - progress, Curve);
}

/// <summary>Configured envelope from rest. Timed segments are proportional; the indefinite hold is schematic.</summary>
public sealed record EnvelopePreview(EnvelopeMode Mode, IReadOnlyList<EnvelopePreviewSegment> Segments)
{
    public double TimedMilliseconds => Segments.Sum(segment => segment.Milliseconds ?? 0);
    public bool HasHold => Segments.Any(segment => segment.Stage == EnvelopeStage.Hold);

    public IEnumerable<EnvelopePreviewPoint> Points => Segments.SelectMany(segment => Enumerable.Range(0, 33)
        .Select(i => new EnvelopePreviewPoint(segment.Start + (segment.End - segment.Start) * i / 32,
            segment.LevelAt(i / 32.0))));

    public double? PlayheadPosition(EnvelopePlayhead? playhead)
    {
        var segment = Segments.FirstOrDefault(s => s.Stage == playhead?.Stage);
        return segment is null || playhead is null ? null : segment.Start + (segment.End - segment.Start) *
            (segment.Stage == EnvelopeStage.Hold ? 0.5 : Math.Clamp(playhead.Progress, 0, 1));
    }

    public static EnvelopePreview Create(GraphNodeSnapshot node)
    {
        double Value(string key) => node.Parameters.Single(parameter => parameter.Definition.Key == key).BaseValue;
        var mode = (EnvelopeMode)Value(EnvelopeGenerator.ModeKey);
        var attack = Value(EnvelopeGenerator.AttackKey);
        var decay = Value(EnvelopeGenerator.DecayKey);
        var release = Value(EnvelopeGenerator.ReleaseKey);
        var sustain = mode == EnvelopeMode.Adsr ? Value(EnvelopeGenerator.SustainKey) : 1;
        var rest = Value(EnvelopeGenerator.RestKey);
        var specs = new List<(EnvelopeStage Stage, double? Ms, double From, double To, double Curve)>
        {
            (EnvelopeStage.Attack, attack, 0, 1, Value(EnvelopeGenerator.AttackCurveKey))
        };
        if (mode != EnvelopeMode.Ar) specs.Add((EnvelopeStage.Decay, decay, 1,
            mode == EnvelopeMode.Adsr ? sustain : 0, Value(EnvelopeGenerator.DecayCurveKey)));
        if (mode is EnvelopeMode.Ar or EnvelopeMode.Adsr)
        {
            specs.Add((EnvelopeStage.Hold, null, sustain, sustain, 1));
            specs.Add((EnvelopeStage.Release, release, sustain, 0, Value(EnvelopeGenerator.ReleaseCurveKey)));
        }
        if (mode == EnvelopeMode.Loop && rest > 0) specs.Add((EnvelopeStage.Rest, rest, 0, 0, 1));
        var timed = specs.Sum(spec => spec.Ms ?? 0);
        var holdWidth = timed / 3;
        var width = timed + (specs.Any(spec => spec.Ms is null) ? holdWidth : 0);
        var cursor = 0.0;
        var segments = specs.Select(spec =>
        {
            var start = cursor / width;
            cursor += spec.Ms ?? holdWidth;
            return new EnvelopePreviewSegment(spec.Stage, start, cursor / width, spec.Ms, spec.From, spec.To, spec.Curve);
        }).ToArray();
        return new(mode, segments);
    }
}

using System.Globalization;
using Microsoft.AspNetCore.Components;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Models;
using Orbit.Application.Services;
using Orbit.Domain.Parameters;

namespace Orbit.UI.Components.Analysis;

/// <summary>Shared compact/expanded calculated plot. No signal capture or animation loop.</summary>
public partial class ProcessorResponseView
{
    [Inject] private IProcessorResponseAnalyzer Analyzer { get; set; } = default!;
    [Parameter, EditorRequired] public GraphNodeSnapshot Node { get; set; } = default!;
    [Parameter] public bool Compact { get; set; }
    [Parameter] public int? SampleRate { get; set; }
    [Parameter] public IReadOnlyDictionary<ParameterAddress, ParameterRange?> ModulationRanges { get; set; } = new Dictionary<ParameterAddress, ParameterRange?>();
    [Parameter] public IReadOnlyDictionary<ParameterAddress, double> EffectiveValues { get; set; } = new Dictionary<ParameterAddress, double>();

    private const double MinimumDb = -60;
    private const double MaximumDb = 24;
    private static readonly int[] DbTicks = [24, 0, -30, -60];
    private (ProcessorResponseParameters Parameters, int Rate)? configuredKey;
    private (ProcessorResponseParameters Parameters, int Rate)? effectiveKey;
    private string configuredPath = "";
    private string? effectivePath;
    private int Rate => SampleRate ?? ProcessorResponseCapabilities.ReferenceSampleRate;
    private double Nyquist => Rate / 2.0;
    private IEnumerable<double> FrequencyTicks => new[] { 20.0, 200.0, 2000.0, Nyquist }.Where(f => f <= Nyquist).Distinct();
    private string AuditionNotice => Node.IsMuted ? "Muted · output silent" : Node.IsBypassed ? "Bypassed · dry path (0 dB)" : "Wet path · calculated, not measured";

    protected override void OnParametersSet()
    {
        if (Analyzer.Resolve(Node, ModulationRanges, EffectiveValues) is not { } response) return;
        var key = (response.Configured, Rate);
        if (configuredKey != key)
        {
            configuredKey = key;
            configuredPath = Path(Analyzer.Calculate(response.Configured, Rate));
        }
        if (response.Effective is { } effective)
        {
            var liveKey = (effective, Rate);
            if (effectiveKey != liveKey)
            {
                effectiveKey = liveKey;
                effectivePath = Path(Analyzer.Calculate(effective, Rate));
            }
        }
        else
        {
            effectiveKey = null;
            effectivePath = null;
        }
    }

    private string Path(IReadOnlyList<ProcessorResponsePoint> points) => string.Join(" ", points.Select(point =>
        $"{Number(X(point.Frequency))},{Number(Y(point.Decibels))}"));

    private double X(double frequency) => 100 * Math.Log(frequency / ProcessorResponseCapabilities.MinimumFrequency) /
        Math.Log(Nyquist / ProcessorResponseCapabilities.MinimumFrequency);
    private static double Y(double db) => 100 * (MaximumDb - Math.Clamp(db, MinimumDb, MaximumDb)) / (MaximumDb - MinimumDb);
    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string FrequencyLabel(double frequency) => frequency >= 1000
        ? $"{(frequency / 1000).ToString("0.#", CultureInfo.InvariantCulture)}k" : Number(frequency);

}

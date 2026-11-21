using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Models;
using Orbit.Application.Services;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;

namespace Orbit.Audio.Analysis;

/// <summary>Registry of response projections for built-in DSP implementations.</summary>
public sealed class ProcessorResponseAnalyzer : IProcessorResponseAnalyzer
{
    private static readonly IReadOnlyDictionary<string, Func<Func<string, double>, ProcessorResponseParameters>> Projections =
        new Dictionary<string, Func<Func<string, double>, ProcessorResponseParameters>>
        {
            [FilterProcessor.NodeType] = value => new((FilterMode)value("mode") switch
            {
                FilterMode.LowPass => ProcessorResponseKind.LowPass,
                FilterMode.HighPass => ProcessorResponseKind.HighPass,
                FilterMode.BandPass => ProcessorResponseKind.BandPass,
                FilterMode.Notch => ProcessorResponseKind.Notch,
                _ => throw new ArgumentOutOfRangeException("mode")
            }, value("cutoff"), value("resonance")),
            [ParametricEqProcessor.NodeType] = value => new(ProcessorResponseKind.Peaking, value("frequency"), value("q"), value("gain"))
        };

    public ProcessorResponseSettings? Resolve(GraphNodeSnapshot node,
        IReadOnlyDictionary<ParameterAddress, ParameterRange?> modulationRanges,
        IReadOnlyDictionary<ParameterAddress, double> effectiveValues)
    {
        if (!Projections.TryGetValue(node.TypeKey, out var project)) return null;
        var parameters = node.Parameters.ToDictionary(parameter => parameter.Definition.Key);
        var configured = project(key => parameters[key].BaseValue);
        var anyLive = false;
        var complete = true;
        var effective = project(key =>
        {
            var parameter = parameters[key];
            var address = new ParameterAddress(node.Id, parameter.Id);
            if (!modulationRanges.ContainsKey(address)) return parameter.BaseValue;
            if (!effectiveValues.TryGetValue(address, out var value) || !double.IsFinite(value))
            {
                complete = false;
                return parameter.BaseValue;
            }
            anyLive = true;
            return Math.Clamp(value, parameter.Definition.Minimum, parameter.Definition.Maximum);
        });
        return new(configured, anyLive && complete ? effective : null);
    }

    public IReadOnlyList<ProcessorResponsePoint> Calculate(ProcessorResponseParameters parameters, int sampleRate)
    {
        const int intervals = 160;
        const double minimum = ProcessorResponseCapabilities.MinimumFrequency;
        var maximum = sampleRate / 2.0;
        if (maximum <= minimum) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        // Include the exact prewarped center (critical for deep notches and high-Q bells).
        var frequencies = Enumerable.Range(0, intervals + 1)
            .Select(index => index == intervals ? maximum : minimum * Math.Pow(maximum / minimum, (double)index / intervals))
            .Append(Math.Clamp(StateVariableResponse.EffectiveCenter(parameters, sampleRate), minimum, maximum))
            .Distinct().Order();
        return frequencies.Select(frequency => new ProcessorResponsePoint(frequency,
            Math.Max(-120, StateVariableResponse.Decibels(parameters, sampleRate, frequency)))).ToArray();
    }
}

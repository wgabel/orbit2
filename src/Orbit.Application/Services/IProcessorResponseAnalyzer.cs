using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Models;
using Orbit.Domain.Parameters;

namespace Orbit.Application.Services;

/// <summary>Pure processor response calculation; no capture or playback dependency.</summary>
public interface IProcessorResponseAnalyzer
{
    ProcessorResponseSettings? Resolve(GraphNodeSnapshot node,
        IReadOnlyDictionary<ParameterAddress, ParameterRange?> modulationRanges,
        IReadOnlyDictionary<ParameterAddress, double> effectiveValues);

    IReadOnlyList<ProcessorResponsePoint> Calculate(ProcessorResponseParameters parameters, int sampleRate);
}

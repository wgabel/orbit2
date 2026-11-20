using Orbit.Domain.Parameters;

namespace Orbit.Application.Models;

/// <summary>Transient effective values from the active DSP revision, never editable model state.</summary>
public sealed record GraphParameterTelemetry(Guid GraphId, long Revision,
    IReadOnlyDictionary<ParameterAddress, double> Values)
{
    /// <summary>Child values grouped by owning graph; Values remains the root canvas view.</summary>
    public IReadOnlyDictionary<Guid, IReadOnlyDictionary<ParameterAddress, double>> ChildValues { get; init; }
        = new Dictionary<Guid, IReadOnlyDictionary<ParameterAddress, double>>();

    public IReadOnlyDictionary<ParameterAddress, double> GetValues(Guid graphId) =>
        graphId == GraphId ? Values : ChildValues.TryGetValue(graphId, out var values)
            ? values : new Dictionary<ParameterAddress, double>();
}

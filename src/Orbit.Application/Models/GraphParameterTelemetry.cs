using Orbit.Domain.Parameters;

namespace Orbit.Application.Models;

/// <summary>Transient effective values from the active DSP revision, never editable model state.</summary>
public sealed record GraphParameterTelemetry(Guid GraphId, long Revision,
    IReadOnlyDictionary<ParameterAddress, double> Values)
{
    /// <summary>Child values grouped by owning graph; Values remains the root canvas view.</summary>
    public IReadOnlyDictionary<Guid, IReadOnlyDictionary<ParameterAddress, double>> ChildValues { get; init; }
        = new Dictionary<Guid, IReadOnlyDictionary<ParameterAddress, double>>();

    public IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, EnvelopePlayhead>> EnvelopePlayheads { get; init; }
        = new Dictionary<Guid, IReadOnlyDictionary<Guid, EnvelopePlayhead>>();
    public IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, double>> PitchOffsets { get; init; }
        = new Dictionary<Guid, IReadOnlyDictionary<Guid, double>>();
    public IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, int>> PitchSteps { get; init; }
        = new Dictionary<Guid, IReadOnlyDictionary<Guid, int>>();

    /// <summary>Current normalized macro values keyed by their owning graph/composite/macro.</summary>
    public IReadOnlyDictionary<GraphMacroAddress, double> MacroValues { get; init; }
        = new Dictionary<GraphMacroAddress, double>();

    public IReadOnlyDictionary<ParameterAddress, double> GetValues(Guid graphId) =>
        graphId == GraphId ? Values : ChildValues.TryGetValue(graphId, out var values)
            ? values : new Dictionary<ParameterAddress, double>();
}

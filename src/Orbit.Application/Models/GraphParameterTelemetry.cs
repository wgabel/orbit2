using Orbit.Domain.Parameters;

namespace Orbit.Application.Models;

/// <summary>Transient effective values from the active DSP revision, never editable model state.</summary>
public sealed record GraphParameterTelemetry(Guid GraphId, long Revision,
    IReadOnlyDictionary<ParameterAddress, double> Values);

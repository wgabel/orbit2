using Orbit.Domain.Parameters;

namespace Orbit.UI.Components.Canvas;

/// <summary>Validated base-value edit from a node's shared parameter slider.</summary>
public sealed record ParameterEditRequest(ParameterAddress Address, double Value);

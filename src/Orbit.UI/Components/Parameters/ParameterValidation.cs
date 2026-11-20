namespace Orbit.UI.Components.Parameters;

/// <summary>Reports a field's validation state to its owning editor.</summary>
public sealed record ParameterValidation(string Key, string? Error);

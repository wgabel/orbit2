namespace Orbit.UI.Components.Parameters;

/// <summary>A live value edit, optionally belonging to one continuous input gesture.</summary>
public sealed record ParameterValueEdit(double Value, Guid? GestureId = null);

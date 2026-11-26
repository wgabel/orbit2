namespace Orbit.UI.Components.Canvas;

/// <summary>Validated normalized value edit from a composite's shared slider.</summary>
public sealed record MacroEditRequest(Guid NodeId, Guid MacroId, double Value, Guid? GestureId = null);

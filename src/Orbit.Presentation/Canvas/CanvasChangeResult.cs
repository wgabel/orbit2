namespace Orbit.Presentation.Canvas;

/// <summary>Layout edit result, with observer errors reported separately from the edit.</summary>
public sealed record CanvasChangeResult(CanvasStateChange? Change, IReadOnlyList<Exception> SubscriberErrors)
{
    public bool Changed => Change is not null;
}

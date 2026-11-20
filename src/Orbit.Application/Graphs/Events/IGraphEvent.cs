namespace Orbit.Application.Graphs.Events;

/// <summary>A fact about a successfully committed graph edit, not a request.</summary>
public interface IGraphEvent
{
    Guid GraphId { get; }
}

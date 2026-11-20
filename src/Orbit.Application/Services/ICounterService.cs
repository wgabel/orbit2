namespace Orbit.Application.Services;

/// <summary>
/// Application contract consumed by the presentation layer. The UI can read
/// the counter and request an increment without knowing its implementation.
/// Program.cs in Orbit.UI registers CounterService for this interface.
/// </summary>
public interface ICounterService
{
    /// <summary>Current application state, exposed as a read-only value.</summary>
    int Count { get; }

    /// <summary>Requests the application operation that increments the count.</summary>
    void Increment();
}

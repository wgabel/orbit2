namespace Orbit.Application.Services;

/// <summary>
/// Framework-independent application service implementing ICounterService.
/// Owns the counter state and its mutation rules; it does not know about the
/// Razor component, rendering lifecycle or browser.
/// </summary>
public sealed class CounterService : ICounterService
{
    // In-memory state for this running application; only this service mutates it.
    public int Count { get; private set; }

    public void Increment()
    {
        // Application operation requested by the UI through ICounterService.
        Count++;
    }
}

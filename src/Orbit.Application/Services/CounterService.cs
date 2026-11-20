namespace Orbit.Application.Services;

public sealed class CounterService : ICounterService
{
    public int Count { get; private set; }

    public void Increment()
    {
        Count++;
    }
}

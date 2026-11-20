namespace Orbit.Application.Services;

public interface ICounterService
{
    int Count { get; }
    void Increment();
}

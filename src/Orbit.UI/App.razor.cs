using Microsoft.AspNetCore.Components;
using Orbit.Application.Services;

namespace Orbit.UI;

public partial class App
{
    [Inject]
    private ICounterService CounterService { get; set; } = default!;

    private int Count => CounterService.Count;

    private void IncrementCounter()
    {
        CounterService.Increment();
    }
}

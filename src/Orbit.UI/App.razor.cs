using Microsoft.AspNetCore.Components;
using Orbit.Application.Services;

namespace Orbit.UI;

/// <summary>
/// Presentation logic for App.razor. The Razor view and this code-behind are
/// partial definitions of the same component class. This boundary translates
/// UI events into application operations without owning application state.
/// </summary>
public partial class App
{
    // Blazor resolves this contract using the registration in Program.cs.
    [Inject]
    private ICounterService CounterService { get; set; } = default!;

    // View-facing projection of application state; App.razor renders this value.
    private int Count => CounterService.Count;

    // Called by App.razor's @onclick binding. Blazor re-renders after the event.
    private void IncrementCounter()
    {
        CounterService.Increment();
    }
}

using Orbit.Application.Models;
using Orbit.Domain.Timing;

namespace Orbit.Application.Services;

/// <summary>Presentation-facing use cases for the first audible signal graph.</summary>
public interface IModulationDemoService
{
    Task InitializeAsync();
    Task PlayAsync(ModulationDemoSettings settings);
    Task StopAsync();
    Task<AudioClockSnapshot?> GetClockSnapshotAsync();
}

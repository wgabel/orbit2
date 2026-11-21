using Orbit.Application.Models;

namespace Orbit.Application.Interfaces;

/// <summary>Optional runtime capability; browsers own bounded capture buffers and share them by channel.</summary>
public interface ISignalCapture
{
    Task<SignalObservationFrame?> ReadSignalAsync(string soundId, Guid generation, Guid subscriptionId, int channel);
    Task ReleaseSignalAsync(string soundId, Guid subscriptionId);
}

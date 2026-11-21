using Orbit.Application.Models;

namespace Orbit.Application.Interfaces;

/// <summary>Optional runtime capability; browsers own bounded capture buffers and share them by channel.</summary>
public interface ISignalCapture
{
    bool SupportsDynamicTaps => false;
    Task<SignalObservationFrame?> ReadSignalAsync(string soundId, Guid generation, Guid subscriptionId, int channel);
    async Task<SignalObservationResult> ReadObservationAsync(string soundId, Guid generation, Guid subscriptionId, int selector)
    {
        var frame = await ReadSignalAsync(soundId, generation, subscriptionId, selector);
        return new(frame is null ? SignalObservationAvailability.Pending : SignalObservationAvailability.Available, frame);
    }
    Task ReleaseSignalAsync(string soundId, Guid subscriptionId);
}

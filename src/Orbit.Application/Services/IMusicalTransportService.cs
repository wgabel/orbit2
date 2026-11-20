using Orbit.Domain.Timing;

namespace Orbit.Application.Services;

/// <summary>Patch-wide transport operations, independent of graph edits and canvas navigation.</summary>
public interface IMusicalTransportService
{
    /// <summary>Observe audio playback and anchor/resume or freeze musical time.</summary>
    Task<TransportSnapshot> SynchronizeAsync(bool audioPlaying);
    Task<TransportSnapshot> SetTempoAsync(double beatsPerMinute);
    Task<TransportSnapshot> ResetAsync();
}

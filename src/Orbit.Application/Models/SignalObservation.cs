using Orbit.Domain.Ports;

namespace Orbit.Application.Models;

public readonly record struct SignalObservationAddress(
    Guid PatchId, Guid GraphId, Guid NodeId, Guid PortId, PortDirection Direction, int Channel = 0);

public enum SignalObservationAvailability { Inactive, Unavailable, Pending, Available, Capacity, Unsupported }

/// <summary>A bounded diagnostic window, not a sample-clock callback. Time is the audio context time at read.</summary>
public sealed record SignalObservationFrame(
    Guid Generation, double SampleRate, double Time, float[] Samples);

public sealed record SignalObservationResult(SignalObservationAvailability Availability,
    SignalObservationFrame? Frame = null);

/// <summary>Ephemeral routing descriptor for a browser-side view; never persisted in a graph.</summary>
public sealed record SignalObservationBinding(SignalObservationAvailability Availability,
    string? SoundId = null, Guid Generation = default, int Channel = 0);

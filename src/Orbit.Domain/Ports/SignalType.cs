namespace Orbit.Domain.Ports;

/// <summary>
/// Describes a port's intended use, not its sample buffer implementation.
/// Trigger, gate and clock are reserved for later DSP support.
/// </summary>
public enum SignalType
{
    Audio,
    Modulation,
    Trigger,
    Gate,
    Clock
}

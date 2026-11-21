namespace Orbit.Domain.Ports;

/// <summary>
/// Describes a port's intended use, not its sample buffer implementation.
/// Trigger/Clock are one-sample pulses; Gate is a held binary level.
/// </summary>
public enum SignalType
{
    Audio,
    Modulation,
    Trigger,
    Gate,
    Clock
}

public static class SignalTypeExtensions
{
    public static bool IsEvent(this SignalType type) => type is SignalType.Trigger or SignalType.Gate or SignalType.Clock;
}

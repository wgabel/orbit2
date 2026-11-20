namespace Orbit.Domain.Ports;

/// <summary>Whether an input accepts one connection or sums several signals.</summary>
public enum FanInPolicy
{
    Single,
    Sum
}

using Orbit.Domain.Timing;

namespace Orbit.Application.Interfaces;

/// <summary>Delivers committed transport anchors to the audio runtime, not per-sample data.</summary>
public interface ITransportSignalSink
{
    Task SetTransportAsync(TransportSnapshot snapshot);
}

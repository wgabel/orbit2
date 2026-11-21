using Orbit.Application.Models;

namespace Orbit.Application.Services;

public interface ISignalAnalysisService
{
    ISignalObservationSubscription Subscribe(SignalObservationAddress address);
}

public interface ISignalObservationSubscription : IAsyncDisposable
{
    Task<SignalObservationResult> ReadAsync();
}

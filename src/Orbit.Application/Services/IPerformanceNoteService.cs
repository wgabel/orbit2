using Orbit.Application.Models;

namespace Orbit.Application.Services;

public interface IPerformanceNoteService
{
    Task<bool> SendAsync(Guid patchId, Guid graphId, Guid nodeId, PerformanceNoteAction action);
}

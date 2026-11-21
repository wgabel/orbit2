using Orbit.Application.Models;

namespace Orbit.Application.Services;

/// <summary>Transient graph-qualified actions, never graph commands or persisted state.</summary>
public interface IRhythmicActionService
{
    Task<bool> SendAsync(Guid patchId, Guid graphId, Guid nodeId, RhythmicAction action);
}

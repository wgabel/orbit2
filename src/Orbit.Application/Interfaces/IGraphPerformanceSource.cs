using Orbit.Application.Models;

namespace Orbit.Application.Interfaces;

/// <summary>Read-only application-side graph/compilation diagnostics.</summary>
public interface IGraphPerformanceSource
{
    void SetPerformanceCollection(bool enabled);
    GraphPerformanceSnapshot? GetGraphPerformanceSnapshot(Guid patchId);
}

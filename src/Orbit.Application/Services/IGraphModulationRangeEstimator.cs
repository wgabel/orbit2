using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Parameters;
using Orbit.Application.Models;

namespace Orbit.Application.Services;

/// <summary>Predicts connected modulation ranges from an immutable graph.
/// An entry with a null range means modulation is connected but its bounds are unknown.</summary>
public interface IGraphModulationRangeEstimator
{
    IReadOnlyDictionary<ParameterAddress, ParameterRange?> Estimate(GraphSnapshot graph);
    /// <summary>Evaluate a child canvas in the context of its complete root patch.</summary>
    IReadOnlyDictionary<ParameterAddress, ParameterRange?> Estimate(GraphSnapshot root, Guid graphId);

    /// <summary>Predict normalized macro ranges in the graph that owns each composite.</summary>
    IReadOnlyDictionary<GraphMacroAddress, ParameterRange?> EstimateMacroRanges(GraphSnapshot root, Guid graphId);
}

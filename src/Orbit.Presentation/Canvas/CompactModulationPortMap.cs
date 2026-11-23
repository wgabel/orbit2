using Orbit.Application.Graphs.Snapshots;

namespace Orbit.Presentation.Canvas;

/// <summary>Projects stable modulation-parameter identities onto persistent input sockets.</summary>
public static class CompactModulationPortMap
{
    public static ParameterSnapshot? ForInput(GraphNodeSnapshot node, Guid inputPortId) =>
        node.Parameters.SingleOrDefault(parameter =>
            parameter.Definition.CanModulate && parameter.ModulationPortId == inputPortId);

    /// <summary>Future definitions without a valid input socket remain editable in compact view.</summary>
    public static IEnumerable<ParameterSnapshot> WithoutInput(GraphNodeSnapshot node) =>
        node.Parameters.Where(parameter => parameter.Definition.CanModulate &&
            (parameter.ModulationPortId is not Guid portId ||
             node.Inputs.All(port => port.Id != portId)));
}

using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Orbit.Domain.Graphs;
using Orbit.Domain.Ports;
using Orbit.Presentation.Canvas;

namespace Orbit.UI.Components.Canvas;

public partial class GraphCanvas
{
    private bool assistedWire;
    private bool touchWireMoved;
    private CanvasPoint touchWireStart;
    private TouchWireGeometry? touchWireGeometry;
    private WireTarget[] wireTargets = [];
    private IReadOnlySet<PortAddress>? wireTargetAddresses;
    private WireAim? wireAim;
    private long touchWireRevision;
    private long touchWireLayoutRevision;

    private async Task PrepareTouchWireAsync(PortPointerStart start)
    {
        if (module is null || TouchWireStale) return;
        var snapshot = Snapshot;
        var source = snapshot.Graph.Nodes.FirstOrDefault(node => node.Id == start.Address.NodeId)?
            .Outputs.FirstOrDefault(port => port.Id == start.Address.PortId);
        if (source is null) return;
        var geometry = await module.InvokeAsync<TouchWireGeometry>("measureTouchWire", viewportElement);
        if (disposed || !assistedWire || gesturePointer != start.Pointer.PointerId ||
            pendingSource != start.Address || snapshot.GraphRevision != Snapshot.GraphRevision ||
            snapshot.LayoutRevision != Snapshot.LayoutRevision) return;
        if (!geometry.Visible.IsValid) return;

        var measured = geometry.Ports.ToDictionary(port => new PortAddress(port.NodeId, port.PortId));
        wireTargets = snapshot.Graph.Nodes.SelectMany(node => node.Inputs
                .Where(input => node.IsInputActive(input.Id))
                .Select(input => (Address: new PortAddress(node.Id, input.Id), Input: input)))
            .Where(candidate => measured.ContainsKey(candidate.Address) &&
                SignalGraphValidator.RoutingRejection(start.Address, source, candidate.Address,
                    candidate.Input, snapshot.Graph.Connections) is null)
            .Select(candidate =>
            {
                var port = measured[candidate.Address];
                return new WireTarget(candidate.Address, new CanvasPoint(port.X, port.Y));
            }).ToArray();
        touchWireGeometry = geometry;
        wireTargetAddresses = wireTargets.Select(target => target.Address).ToHashSet();
        UpdateTouchWire(lastClientPoint);
    }

    private bool UpdateTouchWire(CanvasPoint finger)
    {
        touchWireMoved |= TouchWireAssist.HasMoved(touchWireStart, finger);
        if (!touchWireMoved || touchWireGeometry is not { } geometry) return false;
        var previous = wireAim?.Target;
        wireAim = TouchWireAssist.Resolve(finger, geometry.Visible, wireTargets, previous);
        var point = wireAim.Point.Subtract(new CanvasPoint(geometry.Left, geometry.Top));
        draftEnd = Snapshot.Viewport.ScreenToWorld(point);
        return wireAim.Target is not null && wireAim.Target != previous;
    }

    private bool TouchWireStale => assistedWire &&
        (touchWireRevision != Snapshot.GraphRevision || touchWireLayoutRevision != Snapshot.LayoutRevision);

    private string WireTargetStyle(CanvasPoint point)
    {
        var geometry = touchWireGeometry!;
        return FormattableString.Invariant($"left:{point.X - geometry.Left}px;top:{point.Y - geometry.Top}px;");
    }

    private string WireTargetLabel(PortAddress address)
    {
        var node = Snapshot.Graph.GetNode(address.NodeId);
        return $"{Snapshot.DisplayName(node.Id)} → {node.Inputs.Single(port => port.Id == address.PortId).Name}";
    }

    private void ClearTouchWire()
    {
        assistedWire = touchWireMoved = false;
        wireAim = null;
        wireTargets = [];
        wireTargetAddresses = null;
        touchWireGeometry = null;
    }
}

// Client coordinates are CSS pixels. Only this DOM measurement is browser-specific.
public sealed record TouchWirePort(Guid NodeId, Guid PortId, double X, double Y);
public sealed record TouchWireGeometry(double Left, double Top, CanvasBounds Visible, TouchWirePort[] Ports);

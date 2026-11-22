using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Orbit.Domain.Graphs;
using Orbit.Domain.Ports;
using Orbit.Presentation.Canvas;

namespace Orbit.UI.Components.Canvas;

public partial class GraphCanvas
{
    private bool assistedWire;
    private bool offsetWireForTouch;
    private bool touchWireMoved;
    private CanvasPoint touchWireStart;
    private TouchWireGeometry? touchWireGeometry;
    private WireTarget[] wireTargets = [];
    private readonly Dictionary<PortAddress, bool> wireCompatibility = [];
    private PortDefinition? touchWireSource;
    private IReadOnlySet<PortAddress>? wireTargetAddresses;
    private WireAim? wireAim;
    private long touchWireRevision;
    private long touchWireLayoutRevision;
    private long touchWireGeneration;
    private bool touchWireReleasePending;
    private DotNetObjectReference<GraphCanvas>? touchWireReference;
    private IJSObjectReference? touchWireWatcher;

    private async Task PrepareTouchWireAsync(PortPointerStart start)
    {
        if (module is null || TouchWireStale) return;
        var generation = touchWireGeneration;
        var snapshot = Snapshot;
        var source = snapshot.Graph.Nodes.FirstOrDefault(node => node.Id == start.Address.NodeId)?
            .Outputs.FirstOrDefault(port => port.Id == start.Address.PortId);
        if (source is null) return;
        touchWireSource = source;
        var geometry = await module.InvokeAsync<TouchWireGeometry>("measureTouchWire", viewportElement);
        if (!IsCurrentTouchWire(generation) || touchWireReleasePending) return;
        ApplyTouchWireGeometry(geometry, lastClientPoint);
        touchWireReference ??= DotNetObjectReference.Create(this);
        var watcher = await module.InvokeAsync<IJSObjectReference>("watchTouchWireGeometry",
            viewportElement, touchWireReference, generation);
        if (!IsCurrentTouchWire(generation) || touchWireReleasePending)
            await DisposeTouchWireWatcherAsync(watcher);
        else
            touchWireWatcher = watcher;
    }

    private bool IsCurrentTouchWire(long generation) =>
        !disposed && assistedWire && gesturePointer is not null &&
        generation == touchWireGeneration && !TouchWireStale;

    [JSInvokable]
    public Task TouchWireGeometryChanged(long generation, TouchWireGeometry geometry) => InvokeAsync(() =>
    {
        if (!IsCurrentTouchWire(generation) || touchWireReleasePending) return;
        ApplyTouchWireGeometry(geometry, lastClientPoint);
        StateHasChanged();
    });

    private void ApplyTouchWireGeometry(TouchWireGeometry geometry, CanvasPoint finger)
    {
        if (!geometry.Visible.IsValid)
        {
            touchWireGeometry = null;
            wireAim = null;
            wireTargets = [];
            wireTargetAddresses = null;
            draftEnd = null;
            return;
        }
        wireTargets = geometry.Ports.Select(port => new WireTarget(
                new PortAddress(port.NodeId, port.PortId), new CanvasPoint(port.X, port.Y)))
            .Where(target => IsCompatibleWireTarget(target.Address)).ToArray();
        touchWireGeometry = geometry;
        wireTargetAddresses = wireTargets.Select(target => target.Address).ToHashSet();
        UpdateTouchWire(finger);
    }

    private bool IsCompatibleWireTarget(PortAddress target)
    {
        if (wireCompatibility.TryGetValue(target, out var compatible)) return compatible;
        // Cache validation only for measured inputs, including newly exposed
        // ones. Do not repeatedly traverse the graph or validate offscreen ports.
        var node = Snapshot.Graph.Nodes.FirstOrDefault(node => node.Id == target.NodeId);
        var input = node?.Inputs.FirstOrDefault(port => port.Id == target.PortId);
        compatible = node is not null && input is not null && node.IsInputActive(input.Id) &&
            pendingSource is PortAddress source && touchWireSource is { } output &&
            SignalGraphValidator.RoutingRejection(source, output, target, input, Snapshot.Graph.Connections) is null;
        wireCompatibility[target] = compatible;
        return compatible;
    }

    private async Task<bool> RefreshTouchWireForReleaseAsync(CanvasPoint finger)
    {
        var generation = touchWireGeneration;
        if (!IsCurrentTouchWire(generation)) return false;
        touchWireReleasePending = true;
        if (module is not null && touchWireSource is not null)
        {
            var geometry = await module.InvokeAsync<TouchWireGeometry>("measureTouchWire", viewportElement);
            if (!IsCurrentTouchWire(generation)) return false;
            ApplyTouchWireGeometry(geometry, finger);
        }
        else
            UpdateTouchWire(finger);
        return true;
    }

    private bool UpdateTouchWire(CanvasPoint finger)
    {
        touchWireMoved |= TouchWireAssist.HasMoved(touchWireStart, finger);
        if (!touchWireMoved || touchWireGeometry is not { } geometry) return false;
        var previousAddress = wireAim?.Target?.Address;
        // The same address may have moved since the previous measurement.
        var previous = wireTargets.FirstOrDefault(target => target.Address == previousAddress);
        wireAim = TouchWireAssist.Resolve(finger, geometry.Visible, wireTargets, previous, offsetWireForTouch);
        var point = wireAim.Point.Subtract(new CanvasPoint(geometry.Left, geometry.Top));
        draftEnd = Snapshot.Viewport.ScreenToWorld(point);
        return wireAim.Target is not null && wireAim.Target.Address != previousAddress;
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
        touchWireGeneration++;
        touchWireReleasePending = false;
        assistedWire = touchWireMoved = offsetWireForTouch = false;
        wireAim = null;
        wireTargets = [];
        wireCompatibility.Clear();
        touchWireSource = null;
        wireTargetAddresses = null;
        touchWireGeometry = null;
        if (touchWireWatcher is { } watcher)
        {
            touchWireWatcher = null;
            _ = DisposeTouchWireWatcherAsync(watcher);
        }
    }

    private static async Task DisposeTouchWireWatcherAsync(IJSObjectReference watcher)
    {
        try
        {
            await watcher.InvokeVoidAsync("dispose");
            await watcher.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
    }
}

// Client coordinates are CSS pixels. Only this DOM measurement is browser-specific.
public sealed record TouchWirePort(Guid NodeId, Guid PortId, double X, double Y);
public sealed record TouchWireGeometry(double Left, double Top, CanvasBounds Visible, TouchWirePort[] Ports);

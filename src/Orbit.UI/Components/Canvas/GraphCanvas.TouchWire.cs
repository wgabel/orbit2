using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Diagnostics;
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
    private readonly WireEdgePan edgePan = new();
    private bool wirePanRunning;
    private bool wirePanStopped;
    private bool wireHasPanned;
    private bool wireGeometryDirty;
    private bool wireGeometryRefreshing;
    private DotNetObjectReference<GraphCanvas>? touchWireReference;
    private IJSObjectReference? touchWireWatcher;

    private async Task PrepareWireGestureAsync(PortPointerStart start)
    {
        if (module is null || TouchWireStale) return;
        var generation = touchWireGeneration;
        var snapshot = Snapshot;
        var source = snapshot.Graph.Nodes.FirstOrDefault(node => node.Id == start.Address.NodeId)?
            .Outputs.FirstOrDefault(port => port.Id == start.Address.PortId);
        if (source is null) return;
        touchWireSource = source;
        var geometry = await module.InvokeAsync<TouchWireGeometry>("measureTouchWire", viewportElement);
        if (!IsCurrentTouchWire(generation) || wirePanStopped) return;
        ApplyTouchWireGeometry(geometry, lastClientPoint);
        MaybeStartWirePan();
        touchWireReference ??= DotNetObjectReference.Create(this);
        var watcher = await module.InvokeAsync<IJSObjectReference>("watchTouchWireGeometry",
            viewportElement, touchWireReference, generation);
        if (!IsCurrentTouchWire(generation) || wirePanStopped)
            await DisposeTouchWireWatcherAsync(watcher);
        else
            touchWireWatcher = watcher;
    }

    private bool IsCurrentTouchWire(long generation) =>
        !disposed && drawingWire && gesturePointer is not null &&
        generation == touchWireGeneration && !TouchWireStale;

    [JSInvokable]
    public Task TouchWireGeometryChanged(long generation, TouchWireGeometry geometry) => InvokeAsync(async () =>
    {
        if (!IsCurrentTouchWire(generation) || wirePanStopped || wireGeometryDirty || wireGeometryRefreshing) return;
        // A queued browser notification may predate an edge-pan transform.
        // After panning, read the current DOM instead of applying such a snapshot.
        if (wireHasPanned && module is not null)
        {
            var revision = Snapshot.LayoutRevision;
            geometry = await module.InvokeAsync<TouchWireGeometry>("measureTouchWire", viewportElement);
            if (!IsCurrentTouchWire(generation) || wirePanStopped || wireGeometryDirty || wireGeometryRefreshing ||
                revision != Snapshot.LayoutRevision) return;
        }
        ApplyTouchWireGeometry(geometry, lastClientPoint);
        MaybeStartWirePan();
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
        if (assistedWire) UpdateTouchWire(finger);
        else if (touchWireMoved) UpdateMouseWire(finger, Snapshot.Viewport);
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
        wirePanStopped = true;
        lastClientPoint = finger;
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

    private bool TouchWireStale => drawingWire &&
        (touchWireRevision != Snapshot.GraphRevision || touchWireLayoutRevision != Snapshot.LayoutRevision);
    private void UpdateMouseWire(CanvasPoint pointer, CanvasViewport viewport)
    {
        if (touchWireGeometry is { } geometry)
            draftEnd = viewport.ScreenToWorld(pointer.Subtract(new CanvasPoint(geometry.Left, geometry.Top)));
    }

    private void MaybeStartWirePan()
    {
        if (wirePanStopped || !drawingWire || !touchWireMoved || touchWireGeometry is not { } geometry) return;
        if (WireEdgePan.Velocity(geometry.Visible, lastClientPoint) == CanvasPoint.Zero)
        {
            edgePan.Reset();
            return;
        }
        if (wirePanRunning) return;
        wirePanRunning = true;
        _ = RunWirePanAsync(touchWireGeneration);
    }

    private async Task RunWirePanAsync(long generation)
    {
        var clock = Stopwatch.StartNew();
        var last = clock.Elapsed.TotalSeconds;
        try
        {
            while (IsCurrentTouchWire(generation) && !wirePanStopped && touchWireGeometry is { } geometry)
            {
                var now = clock.Elapsed.TotalSeconds;
                var elapsed = Math.Clamp(now - last, 0, 0.064);
                last = now;
                if (WireEdgePan.Velocity(geometry.Visible, lastClientPoint) == CanvasPoint.Zero) { edgePan.Reset(); break; }
                // Both lifted touch and unshifted mouse/pen use the same snap target.
                // A precise desktop drop on the wider button also holds the viewport.
                var snapped = wireAim?.Target is not null;
                if (!snapped && !offsetWireForTouch && module is not null && !wireGeometryDirty && !wireGeometryRefreshing)
                {
                    var hit = await module.InvokeAsync<PortAddress?>("findInputPortAt", viewportElement, lastClientPoint.X, lastClientPoint.Y);
                    if (!IsCurrentTouchWire(generation) || wirePanStopped) break;
                    snapped = hit is PortAddress address && wireTargetAddresses?.Contains(address) == true;
                }
                var velocity = edgePan.Update(geometry.Visible, lastClientPoint, Environment.TickCount64, snapped);
                // Wait for the rendered transform and real DOM hit-test before
                // moving again. No stale candidate can survive a pan frame.
                if (velocity != CanvasPoint.Zero && elapsed > 0 && !wireGeometryDirty && !wireGeometryRefreshing)
                {
                    var next = Snapshot.Viewport.PanBy(velocity.Scale(elapsed));
                    var delta = next.Pan.Subtract(Snapshot.Viewport.Pan);
                    if (next != Snapshot.Viewport)
                    {
                        wireHasPanned = wireGeometryDirty = true;
                        var prior = wireAim?.Target?.Address;
                        if (assistedWire)
                        {
                            wireTargets = wireTargets.Select(target => target with { Point = target.Point.Add(delta) })
                                .Where(target => TouchWireAssist.Contains(geometry.Visible, target.Point)).ToArray();
                            wireTargetAddresses = wireTargets.Select(target => target.Address).ToHashSet();
                            wireAim = TouchWireAssist.Resolve(lastClientPoint, geometry.Visible, wireTargets,
                                wireTargets.FirstOrDefault(target => target.Address == prior), offsetWireForTouch);
                            draftEnd = next.ScreenToWorld(wireAim.Point.Subtract(new CanvasPoint(geometry.Left, geometry.Top)));
                        }
                        else UpdateMouseWire(lastClientPoint, next);
                        touchWireLayoutRevision = Snapshot.LayoutRevision + 1;
                        State.SetViewport(next);
                    }
                }
                await Task.Delay(33);
            }
        }
        catch (JSException) { if (generation == touchWireGeneration) { pendingSource = null; EndGesture(); } }
        finally { if (generation == touchWireGeneration) wirePanRunning = false; }
    }

    private async Task RefreshPannedWireAfterRenderAsync()
    {
        if (!wireGeometryDirty || wireGeometryRefreshing || !drawingWire || wirePanStopped || module is null) return;
        var generation = touchWireGeneration;
        var revision = Snapshot.LayoutRevision;
        wireGeometryRefreshing = true;
        try
        {
            var geometry = await module.InvokeAsync<TouchWireGeometry>("measureTouchWire", viewportElement);
            // A release or takeover owns the gesture now; never overwrite its
            // fresh release measurement with an older render's geometry.
            if (!IsCurrentTouchWire(generation) || wirePanStopped || revision != Snapshot.LayoutRevision) return;
            var previous = wireAim?.Target?.Address;
            ApplyTouchWireGeometry(geometry, lastClientPoint);
            wireGeometryDirty = false;
            if (offsetWireForTouch && wireAim?.Target is { } target && target.Address != previous)
                await module.InvokeVoidAsync("touchWireFeedback");
            await InvokeAsync(StateHasChanged);
        }
        finally { if (generation == touchWireGeneration) wireGeometryRefreshing = false; }
    }

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
        edgePan.Reset();
        wirePanRunning = wirePanStopped = wireHasPanned = wireGeometryDirty = wireGeometryRefreshing = false;
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

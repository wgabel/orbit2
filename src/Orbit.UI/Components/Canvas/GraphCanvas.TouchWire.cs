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
    private bool touchWireMoved;
    private CanvasPoint touchWireStart;
    private TouchWireGeometry? touchWireGeometry;
    private WireTarget[] wireTargets = [];
    private IReadOnlySet<PortAddress>? wireTargetAddresses;
    private HashSet<PortAddress> compatibleWireAddresses = [];
    private WireAim? wireAim;
    private long touchWireRevision;
    private long touchWireLayoutRevision;
    private readonly WireEdgePan edgePan = new();
    private int wireGestureGeneration;
    private bool wirePanRunning;
    private bool wirePanStopped;
    private bool wireHasPanned;
    private bool wireGeometryDirty;
    private bool wireGeometryRefreshing;

    private async Task PrepareWireGestureAsync(PortPointerStart start)
    {
        if (module is null || TouchWireStale) return;
        var snapshot = Snapshot;
        var source = snapshot.Graph.Nodes.FirstOrDefault(node => node.Id == start.Address.NodeId)?
            .Outputs.FirstOrDefault(port => port.Id == start.Address.PortId);
        if (source is null) return;
        compatibleWireAddresses = snapshot.Graph.Nodes.SelectMany(node => node.Inputs
                .Where(input => node.IsInputActive(input.Id))
                .Select(input => (Address: new PortAddress(node.Id, input.Id), Input: input)))
            .Where(candidate => SignalGraphValidator.RoutingRejection(start.Address, source, candidate.Address,
                    candidate.Input, snapshot.Graph.Connections) is null)
            .Select(candidate => candidate.Address).ToHashSet();
        if (await RefreshWireGeometryAsync()) MaybeStartWirePan();
    }

    private async Task<bool> RefreshWireGeometryAsync()
    {
        if (module is null || !drawingWire || TouchWireStale) return false;
        var generation = wireGestureGeneration;
        var revision = Snapshot.LayoutRevision;
        var geometry = await module.InvokeAsync<TouchWireGeometry>("measureTouchWire", viewportElement);
        if (disposed || generation != wireGestureGeneration || !drawingWire || TouchWireStale ||
            revision != Snapshot.LayoutRevision || !geometry.Visible.IsValid) return false;

        var previous = wireAim?.Target?.Address;
        wireTargets = geometry.Ports.Select(port => new WireTarget(new PortAddress(port.NodeId, port.PortId), new CanvasPoint(port.X, port.Y)))
            .Where(target => compatibleWireAddresses.Contains(target.Address)).ToArray();
        touchWireGeometry = geometry;
        wireTargetAddresses = wireTargets.Select(target => target.Address).ToHashSet();
        wireGeometryDirty = false;
        if (assistedWire)
        {
            var prior = wireTargets.FirstOrDefault(target => target.Address == previous);
            if (wireAim is { } aim) wireAim = aim with { Target = prior };
            if (UpdateTouchWire(lastClientPoint) && module is not null) await module.InvokeVoidAsync("touchWireFeedback");
        }
        else if (touchWireMoved) UpdateMouseWire(lastClientPoint, Snapshot.Viewport);
        return true;
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
        _ = RunWirePanAsync(wireGestureGeneration);
    }

    private async Task RunWirePanAsync(int generation)
    {
        var clock = Stopwatch.StartNew();
        var last = clock.Elapsed.TotalSeconds;
        try
        {
            while (!disposed && generation == wireGestureGeneration && drawingWire && !wirePanStopped && !TouchWireStale && touchWireGeometry is { } geometry)
            {
                var now = clock.Elapsed.TotalSeconds;
                var elapsed = Math.Clamp(now - last, 0, 0.064);
                last = now;
                if (WireEdgePan.Velocity(geometry.Visible, lastClientPoint) == CanvasPoint.Zero) { edgePan.Reset(); break; }
                var snapped = assistedWire && wireAim?.Target is not null;
                if (!assistedWire && module is not null)
                {
                    var hit = await module.InvokeAsync<PortAddress?>("findInputPortAt", viewportElement, lastClientPoint.X, lastClientPoint.Y);
                    if (disposed || generation != wireGestureGeneration || !drawingWire || wirePanStopped || TouchWireStale) break;
                    snapped = hit is PortAddress address && compatibleWireAddresses.Contains(address);
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
                        wireHasPanned = true;
                        var prior = wireAim?.Target?.Address;
                        if (assistedWire)
                        {
                            wireGeometryDirty = true;
                            wireTargets = wireTargets.Select(target => target with { Point = target.Point.Add(delta) })
                                .Where(target => TouchWireAssist.Contains(geometry.Visible, target.Point)).ToArray();
                            wireTargetAddresses = wireTargets.Select(target => target.Address).ToHashSet();
                            wireAim = TouchWireAssist.Resolve(lastClientPoint, geometry.Visible, wireTargets,
                                wireTargets.FirstOrDefault(target => target.Address == prior));
                            draftEnd = next.ScreenToWorld(wireAim.Point.Subtract(new CanvasPoint(geometry.Left, geometry.Top)));
                        }
                        else UpdateMouseWire(lastClientPoint, next);
                        // Layout revision advances synchronously for this owned
                        // viewport change; all other layout/graph changes cancel.
                        touchWireLayoutRevision = Snapshot.LayoutRevision + 1;
                        State.SetViewport(next);
                    }
                }
                await Task.Delay(33);
            }
        }
        catch (JSException) { if (generation == wireGestureGeneration) { pendingSource = null; EndGesture(); } }
        finally { if (generation == wireGestureGeneration) wirePanRunning = false; }
    }

    private async Task RefreshPannedWireAfterRenderAsync()
    {
        if (!wireGeometryDirty || wireGeometryRefreshing || !drawingWire || wirePanStopped) return;
        var generation = wireGestureGeneration;
        wireGeometryRefreshing = true;
        try
        {
            if (await RefreshWireGeometryAsync()) await InvokeAsync(StateHasChanged);
            else if (generation == wireGestureGeneration && drawingWire && !wirePanStopped)
            {
                pendingSource = null;
                EndGesture();
                await InvokeAsync(StateHasChanged);
            }
        }
        finally { if (generation == wireGestureGeneration) wireGeometryRefreshing = false; }
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
        wireGestureGeneration++;
        edgePan.Reset();
        wirePanRunning = wirePanStopped = wireHasPanned = wireGeometryDirty = wireGeometryRefreshing = false;
        compatibleWireAddresses = [];
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

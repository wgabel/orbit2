using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Orbit.Presentation.Canvas;

namespace Orbit.UI.Components.Canvas;

public partial class GraphCanvas
{
    private readonly CanvasFitTaps fitTaps = new();
    private CanvasPoint tapStart;
    private long tapStarted;
    private bool tapEligible;
    private bool gestureIsTouch;
    private CanvasPoint? draggedNodeStart;
    private long? pinchPointer;
    private CanvasPoint pinchPoint;
    private CanvasPoint? pinchOrigin;
    private CanvasPoint pinchStartFirst;
    private CanvasPoint pinchStartSecond;
    private CanvasViewport? pinchStartViewport;
    private int fitGeneration;
    private int fitSequence;
    private bool universalNavigating;
    [Parameter] public EventCallback OnCancelParameterGesture { get; set; }

    public sealed record NavigationContact(long Id, double X, double Y);
    public sealed record NavigationPair(NavigationContact First, NavigationContact Second, double Left, double Top, bool Slider);

    // Capture-phase DOM routing sees both contacts even if a native control or
    // plot stopped its normal bubbling event. The canvas still owns geometry.
    [JSInvokable]
    public async Task BeginUniversalPinchAsync(NavigationPair pair)
    {
        if (disposed || universalNavigating) return;
        var restoreNode = draggedNode;
        var restorePosition = draggedNodeStart;
        pendingSource = null;
        EndGesture();
        if (restoreNode is Guid id && restorePosition is CanvasPoint position && Snapshot.NodePositions.ContainsKey(id))
            State.MoveNode(id, position);
        fitTaps.Reset();
        fitGeneration++;
        universalNavigating = true;
        gestureIsTouch = true;
        gesturePointer = pair.First.Id;
        pinchPointer = pair.Second.Id;
        lastClientPoint = new CanvasPoint(pair.First.X, pair.First.Y);
        pinchPoint = new CanvasPoint(pair.Second.X, pair.Second.Y);
        pinchOrigin = new CanvasPoint(pair.Left, pair.Top);
        pinchStartViewport = Snapshot.Viewport;
        pinchStartFirst = lastClientPoint.Subtract(pinchOrigin.Value);
        pinchStartSecond = pinchPoint.Subtract(pinchOrigin.Value);
        if (pair.Slider) await OnCancelParameterGesture.InvokeAsync();
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public Task MoveUniversalPinchAsync(NavigationContact contact)
    {
        if (disposed || !universalNavigating) return Task.CompletedTask;
        var args = new PointerEventArgs { PointerId = contact.Id, ClientX = contact.X, ClientY = contact.Y, PointerType = "touch" };
        if (!MovePinch(args) && gesturePointer == contact.Id)
        {
            var point = new CanvasPoint(contact.X, contact.Y);
            State.PanBy(point.Subtract(lastClientPoint));
            lastClientPoint = point;
        }
        return InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public Task EndUniversalPinchAsync(long id, bool canceled)
    {
        if (disposed || !universalNavigating) return Task.CompletedTask;
        if (canceled) EndGesture();
        else if (!ReleasePinch(new PointerEventArgs { PointerId = id }) && gesturePointer == id) EndGesture();
        return InvokeAsync(StateHasChanged);
    }

    private async Task BeginNodeSurfaceAsync(Guid nodeId, PointerEventArgs args)
    {
        fitTaps.Reset();
        if (module is null || args.Button != 0 ||
            !await module.InvokeAsync<bool>("isNodeNavigationSurface", viewportElement, args.ClientX, args.ClientY)) return;
        if (universalNavigating || await TakeOverNavigationAsync(args)) return;
        await BeginGesture(args, null, wire: false, tapNodeId: nodeId);
    }

    // A single input tap remains a port activation. A second finger landing
    // on an input during another canvas gesture must not create a wire.
    private async Task BeginInputNavigationAsync(PointerEventArgs args)
    {
        fitTaps.Reset();
        await TakeOverNavigationAsync(args);
    }

    // Promote an active single-pointer gesture to navigation before any new
    // selection, cable, annotation or node edit is started.
    private async Task<bool> TakeOverNavigationAsync(PointerEventArgs args)
    {
        if (universalNavigating) return true;
        if (gesturePointer is not long first || first == args.PointerId)
            return false;
        if (!gestureIsTouch || args.PointerType != "touch" || pinchPointer is not null)
            return true;

        var firstPoint = lastClientPoint;
        var restoreNode = draggedNode;
        var restorePosition = draggedNodeStart;
        pendingSource = null;
        EndGesture();
        if (restoreNode is Guid node && restorePosition is CanvasPoint position &&
            Snapshot.NodePositions.ContainsKey(node))
            State.MoveNode(node, position);
        fitTaps.Reset();
        gesturePointer = first;
        gestureIsTouch = true;
        lastClientPoint = firstPoint;
        pinchPointer = args.PointerId;
        pinchPoint = new CanvasPoint(args.ClientX, args.ClientY);
        if (module is not null)
        {
            var bounds = await module.InvokeAsync<ViewportMeasurement>("viewportBounds", viewportElement);
            if (pinchPointer == args.PointerId && gesturePointer == first)
            {
                pinchOrigin = new CanvasPoint(bounds.Left, bounds.Top);
                pinchStartViewport = Snapshot.Viewport;
                pinchStartFirst = lastClientPoint.Subtract(pinchOrigin.Value);
                pinchStartSecond = pinchPoint.Subtract(pinchOrigin.Value);
            }
            await module.InvokeVoidAsync("capturePointer", viewportElement, args.PointerId);
        }
        return true;
    }

    private bool MovePinch(PointerEventArgs args)
    {
        if (pinchPointer is not long second) return false;
        if (args.PointerId != gesturePointer && args.PointerId != second) return true;
        var point = new CanvasPoint(args.ClientX, args.ClientY);
        if (args.PointerId == second) pinchPoint = point;
        else lastClientPoint = point;
        // Browsers may deliver the two moves separately. Compute from the
        // gesture baseline, not from the already clamped previous frame, so
        // translating both fingers at a zoom limit never ratchets the zoom.
        if (pinchOrigin is CanvasPoint origin && pinchStartViewport is { } start)
            State.SetViewport(start.Pinch(pinchStartFirst, pinchStartSecond,
                lastClientPoint.Subtract(origin), pinchPoint.Subtract(origin)));
        return true;
    }

    private bool ReleasePinch(PointerEventArgs args)
    {
        if (pinchPointer is not long second) return false;
        if (args.PointerId != gesturePointer && args.PointerId != second) return true;
        // The remaining finger becomes a background pan; its last coordinate
        // is the new baseline, so lifting one finger cannot move the viewport.
        if (args.PointerId == gesturePointer)
        {
            gesturePointer = second;
            lastClientPoint = pinchPoint;
        }
        pinchPointer = null;
        pinchOrigin = null;
        pinchStartViewport = null;
        return true;
    }

    private void StartNavigationGesture(PointerEventArgs args, Guid? nodeId, bool backgroundTap)
    {
        fitGeneration++;
        gestureIsTouch = args.PointerType == "touch";
        draggedNodeStart = nodeId is Guid id && Snapshot.NodePositions.TryGetValue(id, out var position)
            ? position : null;
        tapEligible = backgroundTap;
        tapStart = new CanvasPoint(args.ClientX, args.ClientY);
        tapStarted = Environment.TickCount64;
        if (!backgroundTap) fitTaps.Reset();
    }

    private void CheckTapMovement(CanvasPoint point)
    {
        if (!tapEligible) return;
        var delta = point.Subtract(tapStart);
        if (delta.X * delta.X + delta.Y * delta.Y <= 8 * 8) return;
        tapEligible = false;
        fitTaps.Reset();
    }

    private bool CompleteFitTap(PointerEventArgs args)
    {
        var point = new CanvasPoint(args.ClientX, args.ClientY);
        CheckTapMovement(point);
        if (!tapEligible || Environment.TickCount64 - tapStarted > 250)
        {
            fitTaps.Reset();
            return false;
        }
        return fitTaps.Register(Environment.TickCount64, point, args.PointerType);
    }

    private void ClearNavigationGesture()
    {
        universalNavigating = false;
        tapEligible = gestureIsTouch = false;
        draggedNodeStart = null;
        pinchPointer = null;
        pinchOrigin = null;
        pinchStartViewport = null;
    }

    private async Task FitFromGestureAsync()
    {
        if (module is null || disposed) return;
        var generation = ++fitGeneration;
        var state = State;
        await MeasureAsync();
        if (disposed || generation != fitGeneration || !ReferenceEquals(state, State)) return;
        var target = MeasuredFit();
        if (target is null) return;
        var start = Snapshot.Viewport;
        var reducedMotion = await module.InvokeAsync<bool>("prefersReducedMotion");
        if (disposed || generation != fitGeneration || !ReferenceEquals(state, State)) return;
        if (!reducedMotion && start != target)
        {
            var clock = Stopwatch.StartNew();
            const double duration = 220;
            while (clock.Elapsed.TotalMilliseconds < duration)
            {
                var t = Math.Clamp(clock.Elapsed.TotalMilliseconds / duration, 0, 1);
                var eased = 1 - Math.Pow(1 - t, 3);
                state.SetViewport(new CanvasViewport(start.Pan.Add(target.Pan.Subtract(start.Pan).Scale(eased)),
                    start.Zoom + (target.Zoom - start.Zoom) * eased));
                await Task.Delay(16);
                if (disposed || generation != fitGeneration || !ReferenceEquals(state, State)) return;
            }
        }
        state.SetViewport(target);
        fitSequence++;
    }
}

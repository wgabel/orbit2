using System.Diagnostics;
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
    private int fitGeneration;
    private int fitSequence;

    private async Task BeginNodeSurfaceAsync(PointerEventArgs args)
    {
        if (args.PointerType != "touch" || module is null || args.Button != 0 ||
            !await module.InvokeAsync<bool>("isNodeNavigationSurface", viewportElement, args.ClientX, args.ClientY)) return;
        if (await TakeOverNavigationAsync(args)) return;
        await BeginGesture(args, null, wire: false);
    }

    // Promote an active single-pointer gesture to navigation before any new
    // selection, cable, annotation or node edit is started.
    private async Task<bool> TakeOverNavigationAsync(PointerEventArgs args)
    {
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
                pinchOrigin = new CanvasPoint(bounds.Left, bounds.Top);
            await module.InvokeVoidAsync("capturePointer", viewportElement, args.PointerId);
        }
        return true;
    }

    private bool MovePinch(PointerEventArgs args)
    {
        if (pinchPointer is not long second) return false;
        if (args.PointerId != gesturePointer && args.PointerId != second) return true;
        var previousFirst = lastClientPoint;
        var previousSecond = pinchPoint;
        var point = new CanvasPoint(args.ClientX, args.ClientY);
        if (args.PointerId == second) pinchPoint = point;
        else lastClientPoint = point;
        if (pinchOrigin is CanvasPoint origin)
            State.SetViewport(Snapshot.Viewport.Pinch(previousFirst.Subtract(origin),
                previousSecond.Subtract(origin), lastClientPoint.Subtract(origin), pinchPoint.Subtract(origin)));
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
        tapEligible = gestureIsTouch = false;
        draggedNodeStart = null;
        pinchPointer = null;
        pinchOrigin = null;
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

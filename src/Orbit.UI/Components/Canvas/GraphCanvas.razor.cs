using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Orbit.Application.Graphs.Commands;
using Orbit.Domain.Ports;
using Orbit.Domain.Parameters;
using Orbit.Domain.Timing;
using Orbit.Presentation.Canvas;

namespace Orbit.UI.Components.Canvas;

/// <summary>
/// Translates browser gestures into presentation-state operations and graph
/// edit requests. DOM measurements are delegated to the small interop adapter.
/// </summary>
public partial class GraphCanvas : IAsyncDisposable
{
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Parameter, EditorRequired] public ICanvasStateService State { get; set; } = default!;
    [Parameter, EditorRequired] public Func<SetConnectionAmountCommand, string?> DispatchConnectionAmount { get; set; } = default!;
    [Parameter] public Guid PatchId { get; set; }
    [Parameter] public EventCallback<PortConnectionRequest> OnConnect { get; set; }
    [Parameter] public EventCallback<ParameterEditRequest> OnParameterEdit { get; set; }
    [Parameter] public EventCallback<NodeAuditionRequest> OnAudition { get; set; }
    [Parameter] public EventCallback<NodePresetApplyRequest> OnPresetApply { get; set; }
    [Parameter] public EventCallback<Guid> OnManagePresets { get; set; }
    [Parameter] public long PresetCatalogRevision { get; set; }
    [Parameter] public EventCallback<Guid> OnParameterGestureEnded { get; set; }
    [Parameter] public EventCallback<Guid> OnOpenComposite { get; set; }
    [Parameter] public EventCallback<Guid> OnEditRack { get; set; }
    [Parameter] public bool AutoFitOnFirstRender { get; set; } = true;
    [Parameter] public bool CanShowScope { get; set; }
    [Parameter] public bool ScopeOpen { get; set; }
    [Parameter] public EventCallback OnToggleScope { get; set; }
    [Parameter] public double BeatsPerMinute { get; set; } = MusicalTransport.DefaultTempo;
    [Parameter] public IReadOnlyDictionary<ParameterAddress, ParameterRange?> ModulationRanges { get; set; } = new Dictionary<ParameterAddress, ParameterRange?>();
    [Parameter] public IReadOnlyDictionary<ParameterAddress, double> EffectiveValues { get; set; } = new Dictionary<ParameterAddress, double>();

    private ElementReference viewportElement;
    private Orbit.Application.Persistence.ConnectionLabelMode LabelMode
    {
        get => Snapshot.ConnectionLabels;
        set => State.SetConnectionLabels(value);
    }
    private bool ShowBadge(ConnectionVisual wire) => LabelMode switch
    {
        Orbit.Application.Persistence.ConnectionLabelMode.Off => false,
        Orbit.Application.Persistence.ConnectionLabelMode.Selected => wire.Selected,
        Orbit.Application.Persistence.ConnectionLabelMode.Modified => wire.Selected || wire.Amount != 1,
        _ => true
    };

    private IJSObjectReference? module;
    private ICanvasStateService? subscribedState;
    private IDisposable? subscription;
    private CanvasMeasurement? measurement;
    private readonly Dictionary<PortAddress, CanvasPoint> portOffsets = [];
    private long measuredRevision = -1;
    private long visualizationGeometryGeneration;
    private long measuredVisualizationGeneration = -1;
    private bool initialFit;
    private bool disposed;
    private long? gesturePointer;
    private Guid? draggedNode;
    private bool drawingWire;
    private CanvasPoint lastClientPoint;
    private PortAddress? pendingSource;
    private CanvasPoint? draftEnd;

    private CanvasStateSnapshot Snapshot => State.Snapshot;
    private string Hint => pendingSource is not null
        ? "Connect to an input · Esc to cancel"
        : "Drag header · drag background to pan · wheel to zoom";
    private string WorldStyle => FormattableString.Invariant(
        $"transform:translate({Snapshot.Viewport.Pan.X}px,{Snapshot.Viewport.Pan.Y}px) scale({Snapshot.Viewport.Zoom});");

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(State, subscribedState)) return;
        subscription?.Dispose();
        subscribedState = State;
        measurement = null;
        portOffsets.Clear();
        measuredRevision = -1;
        initialFit = !AutoFitOnFirstRender;
        pendingSource = null;
        EndGesture();
        subscription = State.Subscribe(change =>
        {
            if (change.Kind == CanvasChangeKind.VisualizationChanged) visualizationGeometryGeneration++;
            if (pendingSource is PortAddress source &&
                !Snapshot.Graph.Nodes.Any(node => node.Id == source.NodeId &&
                    node.Outputs.Any(port => port.Id == source.PortId)))
            {
                pendingSource = null;
                draftEnd = null;
                drawingWire = false;
            }
            _ = InvokeAsync(StateHasChanged);
        });
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed) return;
        if (module is null)
            module = await JavaScript.InvokeAsync<IJSObjectReference>("import", "./js/canvasDom.js");

        // Node offsets are invariant under pan and zoom. Re-measure after a
        // committed graph edit, or explicitly before fitting measured bounds.
        if (measuredRevision != Snapshot.GraphRevision || measuredVisualizationGeneration != visualizationGeometryGeneration)
        {
            await MeasureAsync();
            if (!initialFit && measurement is not null && measurement.Nodes.Length > 0)
            {
                initialFit = true;
                FitMeasured();
            }
            if (!disposed) StateHasChanged();
        }
    }

    private async Task MeasureAsync()
    {
        if (module is null || disposed) return;
        var revision = Snapshot.GraphRevision;
        var generation = visualizationGeometryGeneration;
        var result = await module.InvokeAsync<CanvasMeasurement>("measureCanvas", viewportElement, Snapshot.Viewport.Zoom);
        if (disposed || revision != Snapshot.GraphRevision || generation != visualizationGeometryGeneration) return;
        measurement = result;
        portOffsets.Clear();
        foreach (var anchor in result.Ports)
            portOffsets[new PortAddress(anchor.NodeId, anchor.PortId)] = new CanvasPoint(anchor.X, anchor.Y);
        measuredRevision = revision;
        measuredVisualizationGeneration = generation;
    }

    private CanvasPoint? PortPoint(PortAddress address) =>
        Snapshot.NodePositions.TryGetValue(address.NodeId, out var position) &&
        portOffsets.TryGetValue(address, out var offset) ? position.Add(offset) : null;

    private IReadOnlyList<ConnectionVisual> Wires
    {
        get
        {
            var result = new List<ConnectionVisual>();
            foreach (var edge in Snapshot.Graph.Connections)
            {
                if (PortPoint(edge.Source) is not CanvasPoint start || PortPoint(edge.Destination) is not CanvasPoint end)
                    continue;
                var destination = Snapshot.Graph.GetNode(edge.Destination.NodeId);
                var kind = destination.Inputs
                    .Single(port => port.Id == edge.Destination.PortId).SignalType;
                var curve = BezierConnection.Between(start, end);
                var center = curve.At(0.5);
                result.Add(new ConnectionVisual(edge.Id, PathBetween(start, end), kind,
                    Snapshot.SelectedConnectionIds.Contains(edge.Id),
                    !destination.IsInputActive(edge.Destination.PortId), edge.Amount,
                    ConnectionBadgeText.Format(Snapshot.Graph, edge),
                    ConnectionBadgeText.Describe(Snapshot.Graph, edge), center.X, center.Y));
            }
            return result;
        }
    }

    private string? DraftPath => pendingSource is PortAddress address &&
        PortPoint(address) is CanvasPoint start && draftEnd is CanvasPoint end
        ? PathBetween(start, end) : null;

    private static string PathBetween(CanvasPoint start, CanvasPoint end)
    {
        var curve = BezierConnection.Between(start, end);
        return FormattableString.Invariant($"M {start.X} {start.Y} C {curve.Control1.X} {curve.Control1.Y}, {curve.Control2.X} {curve.Control2.Y}, {end.X} {end.Y}");
    }

    private async Task BeginBackground(PointerEventArgs args)
    {
        if (args.Button is not (0 or 1)) return;
        if (args.Button == 0)
        {
            State.ClearSelection();
            pendingSource = null;
        }
        await BeginGesture(args, null, wire: false);
    }

    private async Task BeginNode(Guid nodeId, PointerEventArgs args)
    {
        if (args.Button is not (0 or 1)) return;
        if (args.Button == 0)
            State.SelectNode(nodeId, args.ShiftKey || args.CtrlKey || args.MetaKey);
        pendingSource = null;
        await BeginGesture(args, args.Button == 0 ? nodeId : null, wire: false);
    }

    private async Task BeginOutput(PortPointerStart start)
    {
        pendingSource = start.Address;
        draftEnd = PortPoint(start.Address);
        await BeginGesture(start.Pointer, null, wire: true);
    }

    private async Task BeginGesture(PointerEventArgs args, Guid? nodeId, bool wire)
    {
        gesturePointer = args.PointerId;
        draggedNode = nodeId;
        drawingWire = wire;
        lastClientPoint = new CanvasPoint(args.ClientX, args.ClientY);
        if (module is not null)
            await module.InvokeVoidAsync("capturePointer", viewportElement, args.PointerId);
    }

    private void PointerMoved(PointerEventArgs args)
    {
        if (gesturePointer != args.PointerId) return;
        var current = new CanvasPoint(args.ClientX, args.ClientY);
        var delta = current.Subtract(lastClientPoint);
        lastClientPoint = current;
        if (drawingWire)
        {
            // Delta conversion uses the same viewport transform as nodes.
            draftEnd = (draftEnd ?? CanvasPoint.Zero).Add(Snapshot.Viewport.ScreenDeltaToWorld(delta));
        }
        else if (draggedNode is Guid nodeId && Snapshot.NodePositions.ContainsKey(nodeId))
            State.MoveNodeByScreenDelta(nodeId, delta);
        else if (draggedNode is null)
            State.PanBy(delta);
    }

    private async Task PointerReleasedAsync(PointerEventArgs args)
    {
        if (gesturePointer != args.PointerId) return;
        var wasWire = drawingWire;
        EndGesture();
        if (!wasWire || pendingSource is null || module is null) return;
        var target = await module.InvokeAsync<PortAddress?>("findInputPortAt", viewportElement, args.ClientX, args.ClientY);
        if (target is PortAddress destination)
            await ActivatePortAsync(destination);
    }

    private void CancelGesture(PointerEventArgs args)
    {
        if (gesturePointer == args.PointerId) EndGesture();
    }

    private void EndGesture()
    {
        gesturePointer = null;
        draggedNode = null;
        drawingWire = false;
        draftEnd = null;
    }

    private async Task ActivatePortAsync(PortAddress address)
    {
        var node = Snapshot.Graph.Nodes.FirstOrDefault(candidate => candidate.Id == address.NodeId);
        if (node is null) return;
        if (node.Outputs.Any(port => port.Id == address.PortId))
        {
            pendingSource = address;
            return;
        }
        if (pendingSource is PortAddress source)
        {
            pendingSource = null;
            await OnConnect.InvokeAsync(new PortConnectionRequest(source, address));
        }
        else
            State.SelectNode(address.NodeId);
    }

    private void SelectConnection(Guid connectionId)
    {
        pendingSource = null;
        State.SelectConnection(connectionId);
    }

    private async Task CloseConnectionEditorAsync()
    {
        State.ClearSelection();
        await viewportElement.FocusAsync(preventScroll: true);
    }

    private void KeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
        {
            EndGesture();
            pendingSource = null;
            State.ClearSelection();
        }
    }

    private async Task OnWheelAsync(WheelEventArgs args)
    {
        if (module is null) return;
        var bounds = await module.InvokeAsync<ViewportMeasurement>("viewportBounds", viewportElement);
        var anchor = new CanvasPoint(args.ClientX - bounds.Left, args.ClientY - bounds.Top);
        var delta = args.DeltaY * (args.DeltaMode == 1 ? 16 : args.DeltaMode == 2 ? bounds.Height : 1);
        var factor = Math.Exp(-Math.Clamp(delta, -1000, 1000) * 0.0015);
        State.ZoomAt(Snapshot.Viewport.Zoom * factor, anchor);
    }

    public Task ZoomIn() => ZoomByAsync(1.25);
    public Task ZoomOut() => ZoomByAsync(0.8);

    private async Task ZoomByAsync(double factor)
    {
        if (module is null) return;
        var bounds = await module.InvokeAsync<ViewportMeasurement>("viewportBounds", viewportElement);
        State.ZoomAt(Snapshot.Viewport.Zoom * factor, new CanvasPoint(bounds.Width / 2, bounds.Height / 2));
    }

    public void ResetViewport() => State.ResetViewport();

    public async Task FitAsync()
    {
        await MeasureAsync();
        FitMeasured();
    }

    /// <summary>Measure at Add time, including resizes and scrolling not associated with graph edits.
    /// The reservation is consumed by the next synchronous node-add commit on this canvas.</summary>
    public async Task<IDisposable?> ReserveVisibleNodePlacementAsync()
    {
        if (module is null || disposed) return null;
        var state = State;
        var snapshot = state.Snapshot;
        var result = await module.InvokeAsync<CanvasMeasurement>("measureCanvas", viewportElement, snapshot.Viewport.Zoom);
        if (disposed || !ReferenceEquals(State, state) || snapshot != state.Snapshot) return null;

        var screen = result.VisibleScreenBounds.IsValid ? result.VisibleScreenBounds :
            new CanvasBounds(0, 0, result.Width, result.Height);
        if (!screen.IsValid) return null;

        // Keep a small screen-space inset even when zoomed in or on a narrow screen.
        var padding = Math.Min(24, Math.Min(screen.Width, screen.Height) / 8);
        var start = snapshot.Viewport.ScreenToWorld(new CanvasPoint(screen.Left + padding, screen.Top + padding));
        var end = snapshot.Viewport.ScreenToWorld(new CanvasPoint(screen.Right - padding, screen.Bottom - padding));
        var occupied = result.Nodes.Where(node => snapshot.NodePositions.ContainsKey(node.NodeId))
            .Select(node =>
            {
                var position = snapshot.NodePositions[node.NodeId];
                return new CanvasBounds(position.X, position.Y, position.X + node.Width, position.Y + node.Height);
            });
        return state.ReserveVisibleNodePlacement(new CanvasBounds(start.X, start.Y, end.X, end.Y), occupied);
    }

    private void FitMeasured()
    {
        if (measurement is null || measurement.Width <= 0 || measurement.Height <= 0) return;
        var rectangles = measurement.Nodes.Where(node => Snapshot.NodePositions.ContainsKey(node.NodeId))
            .Select(node => (Position: Snapshot.NodePositions[node.NodeId], node.Width, node.Height)).ToArray();
        if (rectangles.Length == 0)
        {
            State.ResetViewport();
            return;
        }
        var bounds = new CanvasBounds(rectangles.Min(node => node.Position.X),
            rectangles.Min(node => node.Position.Y),
            rectangles.Max(node => node.Position.X + node.Width),
            rectangles.Max(node => node.Position.Y + node.Height));
        if (bounds.IsValid)
            State.FitToBounds(bounds, measurement.Width, measurement.Height, 32);
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        subscription?.Dispose();
        if (module is not null)
            await module.DisposeAsync();
    }
}

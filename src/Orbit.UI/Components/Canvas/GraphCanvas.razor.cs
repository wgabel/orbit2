using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Orbit.Application.Graphs.Commands;
using Orbit.Domain.Ports;
using Orbit.Domain.Parameters;
using Orbit.Domain.Timing;
using Orbit.Presentation.Canvas;
using Orbit.Application.Persistence;

namespace Orbit.UI.Components.Canvas;

/// <summary>
/// Translates browser gestures into presentation-state operations and graph
/// edit requests. DOM measurements are delegated to the small interop adapter.
/// </summary>
public partial class GraphCanvas : IAsyncDisposable
{
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Parameter, EditorRequired] public ICanvasStateService State { get; set; } = default!;
    [Parameter, EditorRequired] public GraphElementCounts PatchCounts { get; set; }
    [Parameter] public OutlineFocusRequest? OutlineFocus { get; set; }
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
    [Parameter] public EventCallback<GroupNodesCommand> OnGroupNodes { get; set; }
    [Parameter] public EventCallback<UngroupCompositeCommand> OnUngroupComposite { get; set; }
    [Parameter] public EventCallback<Guid> OnEditRack { get; set; }
    [Parameter] public bool AutoFitOnFirstRender { get; set; } = true;
    [Parameter] public bool CanShowScope { get; set; }
    [Parameter] public bool ScopeOpen { get; set; }
    [Parameter] public bool MobileSelectionOpen { get; set; }
    [Parameter] public EventCallback OnMobileToolCompleted { get; set; }
    [Parameter] public EventCallback OnCloseMobileTools { get; set; }
    [Parameter] public EventCallback OnToggleScope { get; set; }
    [Parameter] public EventCallback<bool> OnAnnotationDraftChanged { get; set; }
    [Parameter] public EventCallback<string> OnAnnotationError { get; set; }
    [Parameter] public bool AnnotationActionsDisabled { get; set; }
    [Parameter] public double BeatsPerMinute { get; set; } = MusicalTransport.DefaultTempo;
    [Parameter] public int? SampleRate { get; set; }
    [Parameter] public IReadOnlyDictionary<ParameterAddress, ParameterRange?> ModulationRanges { get; set; } = new Dictionary<ParameterAddress, ParameterRange?>();
    [Parameter] public IReadOnlyDictionary<ParameterAddress, double> EffectiveValues { get; set; } = new Dictionary<ParameterAddress, double>();
    [Parameter] public IReadOnlyDictionary<Guid, Orbit.Application.Models.EnvelopePlayhead> EnvelopePlayheads { get; set; } = new Dictionary<Guid, Orbit.Application.Models.EnvelopePlayhead>();

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
    private bool previousSecondaryDetail;
    private IReadOnlyList<Guid> previousSelectedNodes = [];
    private bool initialFit;
    private Guid? handledOutlineFocus;
    private bool disposed;
    private long? gesturePointer;
    private Guid? draggedNode;
    private bool drawingWire;
    private CanvasPoint lastClientPoint;
    private PortAddress? pendingSource;
    private CanvasPoint? draftEnd;
    private SavedNote? notePreview;
    private bool resizingNote;
    private Guid? editingNote;
    private sealed record DescriptionDraft(Guid Id, bool IsConnection, string Heading, string Name, string Body);
    private DescriptionDraft? editingDescription;
    public bool HasAnnotationDraft => editingNote is not null || editingDescription is not null || editingGroup is not null;

    private async Task EditNodeDescriptionAsync(Guid nodeId)
    {
        if (HasAnnotationDraft || AnnotationActionsDisabled) return;
        State.SelectNode(nodeId);
        await EditSelectedDescriptionAsync();
    }

    public async Task EditSelectedDescriptionAsync()
    {
        if (HasAnnotationDraft || AnnotationActionsDisabled) return;
        if (Snapshot.SelectedNodeIds.Count == 1 && Snapshot.SelectedConnectionIds.Count == 0)
        {
            var id = Snapshot.SelectedNodeIds[0];
            var value = Snapshot.NodeDescriptions.GetValueOrDefault(id);
            editingDescription = new(id, false, Snapshot.Graph.GetNode(id).Name, value?.Alias ?? "", value?.Description ?? "");
        }
        else if (Snapshot.SelectedConnectionIds.Count == 1 && Snapshot.SelectedNodeIds.Count == 0)
        {
            var id = Snapshot.SelectedConnectionIds[0];
            editingDescription = new(id, true, "Connection description",
                Snapshot.ConnectionDescriptions.GetValueOrDefault(id)?.Label ?? "", "");
        }
        else return;
        pendingSource = null;
        await SetAnnotationDraftAsync(true);
    }

    private async Task ApplyDescriptionAsync((string Name, string Body) draft)
    {
        if (editingDescription is not { } target) return;
        try
        {
            if (target.IsConnection) State.SetConnectionDescription(new(target.Id, draft.Name));
            else State.SetNodeDescription(new(target.Id, draft.Name, draft.Body));
        }
        catch (Exception exception) { await OnAnnotationError.InvokeAsync(exception.Message); return; }
        await CloseDescriptionAsync();
    }

    private async Task CloseDescriptionAsync()
    {
        editingDescription = null;
        await SetAnnotationDraftAsync(false);
        await viewportElement.FocusAsync(preventScroll: true);
    }

    private async Task SetAnnotationDraftAsync(bool active)
    {
        await OnAnnotationDraftChanged.InvokeAsync(active);
        await SetExternalDraftWarningAsync(active);
    }

    private bool draftWarningActive;
    public async Task SetExternalDraftWarningAsync(bool active)
    {
        if (module is not null) await module.InvokeVoidAsync("setAnnotationDraftWarning", active);
        draftWarningActive = active;
    }

    private async Task BeginNoteEditAsync(Guid id)
    {
        if (HasAnnotationDraft || AnnotationActionsDisabled) return;
        State.SelectNote(id);
        pendingSource = null;
        editingNote = id;
        await SetAnnotationDraftAsync(true);
    }

    private async Task FinishNoteEditAsync(SavedNote? note)
    {
        try { if (note is not null) State.UpdateNote(note); }
        catch (Exception exception) { await OnAnnotationError.InvokeAsync(exception.Message); return; }
        editingNote = null;
        await SetAnnotationDraftAsync(false);
        await viewportElement.FocusAsync(preventScroll: true);
    }

    private Task UpdateNote(SavedNote note) => NoteAction(() => State.UpdateNote(note));

    private async Task NoteAction(Func<CanvasChangeResult> action)
    {
        if (HasAnnotationDraft || AnnotationActionsDisabled) return;
        try { action(); }
        catch (Exception exception) { await OnAnnotationError.InvokeAsync(exception.Message); }
    }

    private async Task BeginNoteAsync(SavedNote note, PointerEventArgs args, bool resize)
    {
        if (await TakeOverNavigationAsync(args) || HasAnnotationDraft || AnnotationActionsDisabled || args.Button != 0) return;
        State.SelectNote(note.Id);
        pendingSource = null;
        notePreview = note;
        resizingNote = resize;
        await BeginGesture(args, null, wire: false);
    }

    private CanvasStateSnapshot Snapshot => State.Snapshot;
    private string? Hint => pendingSource is not null
        ? "Connect to an input · Esc to cancel"
        : marqueeMode ? "Drag background to select · Ctrl/Meta adds · Esc cancels"
        : null;
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
        finderOpen = false;
        finderTarget = "";
        selectionPanelOpen = marqueeMode = false;
        selectionCandidate = "";
        previousSecondaryDetail = Snapshot.ShowSecondaryAnnotations;
        previousSelectedNodes = Snapshot.SelectedNodeIds;
        fitGeneration++;
        fitTaps.Reset();
        EndGesture();
        subscription = State.Subscribe(change =>
        {
            if (change.Kind is CanvasChangeKind.VisualizationChanged or CanvasChangeKind.DescriptionsChanged) visualizationGeometryGeneration++;
            if (previousSecondaryDetail != change.Snapshot.ShowSecondaryAnnotations ||
                (!change.Snapshot.ShowSecondaryAnnotations && !previousSelectedNodes.SequenceEqual(change.Snapshot.SelectedNodeIds)))
                visualizationGeometryGeneration++;
            previousSecondaryDetail = change.Snapshot.ShowSecondaryAnnotations;
            previousSelectedNodes = change.Snapshot.SelectedNodeIds;
            if (TouchWireStale)
            {
                pendingSource = null;
                EndGesture();
            }
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
            module = await JavaScript.InvokeAsync<IJSObjectReference>("import", BrowserBuild.ModulePath("canvasDom.js"));

        // Node offsets are invariant under pan and zoom. Re-measure after a
        // committed graph edit, or explicitly before fitting measured bounds.
        if (measuredRevision != Snapshot.GraphRevision || measuredVisualizationGeneration != visualizationGeometryGeneration)
        {
            await MeasureAsync();
            if (!initialFit && !HasPendingElementFocus && measurement is not null && (measurement.Nodes.Length > 0 || Snapshot.Notes.Count > 0 || Snapshot.VisualGroups.Count > 0))
            {
                initialFit = true;
                FitMeasured();
            }
            if (!disposed) StateHasChanged();
        }
        await ApplyOutlineFocusAsync();
    }

    private bool HasPendingElementFocus => OutlineFocus is { } request && request.Token != handledOutlineFocus &&
        request.Target.GraphId == Snapshot.GraphId && request.Target.Kind != OutlineKind.Canvas;

    private async Task ApplyOutlineFocusAsync()
    {
        if (disposed || OutlineFocus is not { } request || request.Token == handledOutlineFocus ||
            request.Target.GraphId != Snapshot.GraphId || AnnotationControlsLocked) return;
        if (measurement is null || measuredRevision != Snapshot.GraphRevision || measuredVisualizationGeneration != visualizationGeometryGeneration) return;
        handledOutlineFocus = request.Token;
        var target = request.Target;
        CanvasBounds? bounds = target.Kind switch
        {
            OutlineKind.Note when Snapshot.SelectedNoteId == target.Id && Snapshot.Notes.FirstOrDefault(note => note.Id == target.Id) is { } note => CanvasStateService.NoteBounds(note),
            OutlineKind.Group when Snapshot.SelectedGroupId == target.Id && Snapshot.VisualGroups.FirstOrDefault(group => group.Id == target.Id) is { } group => CanvasStateService.GroupBounds(group),
            OutlineKind.Node or OutlineKind.Composite when Snapshot.SelectedNodeIds.Contains(target.Id) => MeasuredNodeBounds(target.Id),
            _ => null
        };
        if (bounds is { IsValid: true } fit && measurement.Width > 0 && measurement.Height > 0)
        {
            initialFit = true;
            State.FitToBounds(fit, measurement.Width, measurement.Height, 32);
        }
        await viewportElement.FocusAsync(preventScroll: true);
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
        Snapshot.NodePositions.ContainsKey(address.NodeId) &&
        portOffsets.TryGetValue(address, out var offset) ? DisplayNodePosition(address.NodeId).Add(offset) : null;

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
                    ConnectionBadgeText.Describe(Snapshot.Graph, edge), center.X, center.Y,
                    Snapshot.ConnectionDescriptions.GetValueOrDefault(edge.Id)?.Label ?? ""));
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
        if (await TakeOverNavigationAsync(args) || args.Button is not (0 or 1)) return;
        if (args.Button == 0 && (marqueeMode || args.ShiftKey))
        {
            await BeginMarqueeAsync(args);
            return;
        }
        if (args.Button == 0)
        {
            State.ClearSelection();
            pendingSource = null;
        }
        await BeginGesture(args, null, wire: false,
            backgroundTap: args.Button == 0 && !args.ShiftKey && !args.CtrlKey && !args.MetaKey && !args.AltKey);
    }

    private async Task BeginNode(Guid nodeId, PointerEventArgs args)
    {
        if (await TakeOverNavigationAsync(args) || args.Button is not (0 or 1)) return;
        if (args.Button == 0)
            State.SelectNode(nodeId, args.ShiftKey || args.CtrlKey || args.MetaKey);
        pendingSource = null;
        await BeginGesture(args, args.Button == 0 ? nodeId : null, wire: false);
    }

    private async Task BeginOutput(PortPointerStart start)
    {
        if (await TakeOverNavigationAsync(start.Pointer)) return;
        ClearTouchWire();
        pendingSource = start.Address;
        draftEnd = PortPoint(start.Address);
        assistedWire = start.Pointer.PointerType == "touch";
        touchWireStart = new CanvasPoint(start.Pointer.ClientX, start.Pointer.ClientY);
        touchWireRevision = Snapshot.GraphRevision;
        touchWireLayoutRevision = Snapshot.LayoutRevision;
        await BeginGesture(start.Pointer, null, wire: true);
        if (assistedWire && gesturePointer == start.Pointer.PointerId)
            await PrepareTouchWireAsync(start);
    }

    private async Task BeginGesture(PointerEventArgs args, Guid? nodeId, bool wire, bool backgroundTap = false)
    {
        StartNavigationGesture(args, nodeId, backgroundTap);
        gesturePointer = args.PointerId;
        draggedNode = nodeId;
        drawingWire = wire;
        lastClientPoint = new CanvasPoint(args.ClientX, args.ClientY);
        if (module is not null)
            await module.InvokeVoidAsync("capturePointer", viewportElement, args.PointerId);
    }

    private async Task PointerMoved(PointerEventArgs args)
    {
        if (MovePinch(args)) return;
        if (gesturePointer != args.PointerId) return;
        var current = new CanvasPoint(args.ClientX, args.ClientY);
        CheckTapMovement(current);
        var delta = current.Subtract(lastClientPoint);
        lastClientPoint = current;
        if (marquee is { } selection)
            marquee = selection with { End = current };
        else if (groupPreview is not null)
            PreviewGroupDelta(Snapshot.Viewport.ScreenDeltaToWorld(delta));
        else if (notePreview is { } note)
        {
            var world = Snapshot.Viewport.ScreenDeltaToWorld(delta);
            notePreview = resizingNote
                ? note with { Width = Math.Clamp(note.Width + world.X, SavedNote.MinWidth, SavedNote.MaxSize),
                    Height = Math.Clamp(note.Height + world.Y, SavedNote.MinHeight, SavedNote.MaxSize) }
                : note with { X = Math.Clamp(note.X + world.X, -SavedNote.MaxCoordinate, SavedNote.MaxCoordinate),
                    Y = Math.Clamp(note.Y + world.Y, -SavedNote.MaxCoordinate, SavedNote.MaxCoordinate) };
        }
        else if (drawingWire)
        {
            if (assistedWire)
            {
                if (UpdateTouchWire(current) && module is not null)
                    await module.InvokeVoidAsync("touchWireFeedback");
            }
            else
            {
                // Mouse drag uses the existing world delta.
                draftEnd = (draftEnd ?? CanvasPoint.Zero).Add(Snapshot.Viewport.ScreenDeltaToWorld(delta));
            }
        }
        else if (draggedNode is Guid nodeId && Snapshot.NodePositions.ContainsKey(nodeId))
            State.MoveNodeByScreenDelta(nodeId, delta);
        else if (draggedNode is null)
            State.PanBy(delta);
    }

    private async Task PointerReleasedAsync(PointerEventArgs args)
    {
        if (ReleasePinch(args)) return;
        if (gesturePointer != args.PointerId) return;
        var shouldFit = CompleteFitTap(args);
        if (shouldFit)
        {
            EndGesture();
            await FitFromGestureAsync();
            return;
        }
        if (marquee is { } selection)
        {
            EndGesture();
            await CompleteMarqueeAsync(selection with { End = new CanvasPoint(args.ClientX, args.ClientY) });
            return;
        }
        if (assistedWire)
        {
            // Layout may have changed since the last observer notification.
            if (!await RefreshTouchWireForReleaseAsync(new CanvasPoint(args.ClientX, args.ClientY))) return;
            var snappedTarget = TouchWireStale ? null : wireAim?.Target;
            var moved = touchWireMoved;
            EndGesture();
            if (snappedTarget is not null)
                await ActivatePortAsync(snappedTarget.Address);
            else if (moved)
                pendingSource = null;
            return;
        }
        var wasWire = drawingWire;
        var note = notePreview;
        var originalGroup = groupStart;
        var group = groupPreview;
        var wasResizingGroup = resizingGroup;
        EndGesture();
        if (originalGroup is not null && group is not null)
        {
            await CommitGroupGestureAsync(originalGroup, group, wasResizingGroup);
            return;
        }
        if (note is not null)
        {
            await UpdateNote(note);
            return;
        }
        if (!wasWire || pendingSource is null || module is null) return;
        var target = await module.InvokeAsync<PortAddress?>("findInputPortAt", viewportElement, args.ClientX, args.ClientY);
        if (target is PortAddress destination)
            await ActivatePortAsync(destination);
    }

    private void CancelGesture(PointerEventArgs args)
    {
        if (gesturePointer != args.PointerId && pinchPointer != args.PointerId) return;
        fitTaps.Reset();
        if (assistedWire) pendingSource = null;
        EndGesture();
    }

    private void EndGesture()
    {
        ClearNavigationGesture();
        ClearTouchWire();
        gesturePointer = null;
        draggedNode = null;
        drawingWire = false;
        draftEnd = null;
        notePreview = null;
        resizingNote = false;
        groupPreview = groupStart = null;
        resizingGroup = false;
        marquee = null;
    }

    private async Task ActivatePortAsync(PortAddress address)
    {
        fitTaps.Reset();
        if (assistedWire) return;
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
        fitTaps.Reset();
        pendingSource = null;
        State.SelectConnection(connectionId);
    }

    private async Task CloseConnectionEditorAsync()
    {
        State.ClearSelection();
        await viewportElement.FocusAsync(preventScroll: true);
    }

    private async Task KeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
        {
            var selecting = marquee is not null || marqueeMode;
            fitGeneration++;
            fitTaps.Reset();
            EndGesture();
            pendingSource = null;
            marqueeMode = false;
            selectionPanelOpen = false;
            if (!selecting) State.ClearSelection();
        }
        else if (module is not null && gesturePointer is null && !args.CtrlKey && !args.MetaKey && !args.AltKey &&
            (args.Key is "f" or "F" or "+" or "=" or "-" or "0") &&
            await module.InvokeAsync<bool>("canvasHasFocus", viewportElement))
        {
            fitTaps.Reset();
            if (args.Key is "f" or "F") await FitFromGestureAsync();
            else
            {
                fitGeneration++;
                if (args.Key == "0") State.ResetViewport();
                else await ZoomByAsync(args.Key == "-" ? 0.8 : 1.25);
            }
        }
    }

    private async Task OnWheelAsync(WheelEventArgs args)
    {
        if (module is null || marquee is not null || gesturePointer is not null) return;
        fitGeneration++;
        fitTaps.Reset();
        var bounds = await module.InvokeAsync<ViewportMeasurement>("viewportBounds", viewportElement);
        var anchor = new CanvasPoint(args.ClientX - bounds.Left, args.ClientY - bounds.Top);
        var delta = args.DeltaY * (args.DeltaMode == 1 ? 16 : args.DeltaMode == 2 ? bounds.Height : 1);
        var factor = Math.Exp(-Math.Clamp(delta, -1000, 1000) * 0.0015);
        State.ZoomAt(Snapshot.Viewport.Zoom * factor, anchor);
    }

    private async Task ZoomByAsync(double factor)
    {
        if (module is null) return;
        var bounds = await module.InvokeAsync<ViewportMeasurement>("viewportBounds", viewportElement);
        State.ZoomAt(Snapshot.Viewport.Zoom * factor, new CanvasPoint(bounds.Width / 2, bounds.Height / 2));
    }

    public async Task FocusNodeAsync(Guid nodeId)
    {
        var state = State;
        await MeasureAsync();
        if (disposed || !ReferenceEquals(state, State) || measurement is null ||
            MeasuredNodeBounds(nodeId) is not { IsValid: true } bounds || measurement.Width <= 0 || measurement.Height <= 0) return;
        State.SelectNode(nodeId);
        State.FitToBounds(bounds, measurement.Width, measurement.Height, 32);
    }

    private CanvasBounds? MeasuredNodeBounds(Guid nodeId)
    {
        if (!Snapshot.NodePositions.TryGetValue(nodeId, out var position) ||
            measurement?.Nodes.FirstOrDefault(item => item.NodeId == nodeId) is not { } node) return null;
        return new CanvasBounds(position.X, position.Y, position.X + node.Width, position.Y + node.Height);
    }

    /// <summary>Measure at Add time, including resizes and scrolling not associated with graph edits.
    /// The reservation is consumed by the next synchronous node-add commit on this canvas.</summary>
    public async Task<IDisposable?> ReserveVisibleNodePlacementAsync()
    {
        var context = await MeasurePlacementAsync();
        return context is { } value ? State.ReserveVisibleNodePlacement(value.Visible, value.Occupied) : null;
    }

    public async Task<bool> AddNoteAsync()
    {
        var context = await MeasurePlacementAsync();
        if (context is not { } value || HasAnnotationDraft || AnnotationActionsDisabled) return false;
        State.AddNote(value.Visible, value.Occupied);
        return true;
    }

    private async Task<(CanvasBounds Visible, CanvasBounds[] Occupied, Dictionary<Guid, CanvasBounds> NodeBounds)?> MeasurePlacementAsync()
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
            .ToDictionary(node => node.NodeId, node =>
            {
                var position = snapshot.NodePositions[node.NodeId];
                return new CanvasBounds(position.X, position.Y, position.X + node.Width, position.Y + node.Height);
            });
        return (new CanvasBounds(start.X, start.Y, end.X, end.Y), occupied.Values.ToArray(), occupied);
    }

    private void FitMeasured(bool signalOnly = false)
    {
        if (MeasuredFit(signalOnly) is { } viewport) State.SetViewport(viewport);
    }

    private CanvasViewport? MeasuredFit(bool signalOnly = false)
    {
        if (measurement is null || measurement.Width <= 0 || measurement.Height <= 0) return null;
        var rectangles = measurement.Nodes.Where(node => Snapshot.NodePositions.ContainsKey(node.NodeId))
            .Select(node =>
            {
                var position = Snapshot.NodePositions[node.NodeId];
                return new CanvasBounds(position.X, position.Y, position.X + node.Width, position.Y + node.Height);
            }).Concat(signalOnly ? [] : AnnotationDetailPolicy.FitBounds(Snapshot)).ToArray();
        if (rectangles.Length == 0)
        {
            return CanvasViewport.Initial;
        }
        var bounds = new CanvasBounds(rectangles.Min(node => node.Left), rectangles.Min(node => node.Top),
            rectangles.Max(node => node.Right), rectangles.Max(node => node.Bottom));
        return bounds.IsValid ? CanvasViewport.Fit(bounds, measurement.Width, measurement.Height, 32) : null;
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        subscription?.Dispose();
        var watcher = touchWireWatcher;
        touchWireWatcher = null;
        ClearTouchWire();
        if (watcher is not null) await DisposeTouchWireWatcherAsync(watcher);
        touchWireReference?.Dispose();
        if (module is not null)
        {
            if (draftWarningActive) await module.InvokeVoidAsync("setAnnotationDraftWarning", false);
            await module.DisposeAsync();
        }
    }
}

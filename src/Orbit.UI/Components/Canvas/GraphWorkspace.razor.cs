using Microsoft.AspNetCore.Components;
using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Models;
using Orbit.Application.Services;
using Orbit.Domain.Parameters;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Presentation.Canvas;
using Orbit.Application.Persistence;
using Orbit.Application.Presets;
using Orbit.Domain.Timing;

namespace Orbit.UI.Components.Canvas;

/// <summary>Composition and command boundary for the complete patch editor.</summary>
public partial class GraphWorkspace : IAsyncDisposable
{
    [Inject] private IGraphModulationRangeEstimator ModulationEstimator { get; set; } = default!;
    [Inject] private PatchDocumentSerializer Serializer { get; set; } = default!;
    [Inject] private PatchPersistenceService Persistence { get; set; } = default!;
    [Inject] private IMusicalTransportService Transport { get; set; } = default!;
    [Inject] private NodeRegistry Registry { get; set; } = default!;
    [Inject] private NodePresetService PresetCatalog { get; set; } = default!;
    [Inject] private NodePresetLibraryService PresetLibrary { get; set; } = default!;
    private bool initialized;
    private bool autosaveEnabled = true;
    private bool disposed;
    private double tempo = MusicalTransport.DefaultTempo;
    private string? PersistenceError { get; set; }
    private string? PresetLibraryError { get; set; }
    private (Guid GraphId, Guid NodeId)? managingPresets;
    private GraphNodeSnapshot? ManagingPresetNode => managingPresets is { } address && address.GraphId == Canvas.Snapshot.GraphId
        ? Canvas.Snapshot.Graph.Nodes.SingleOrDefault(node => node.Id == address.NodeId) : null;
    private IReadOnlyDictionary<ParameterAddress, ParameterRange?> ModulationRanges { get; set; } = new Dictionary<ParameterAddress, ParameterRange?>();
    private static readonly IReadOnlyDictionary<ParameterAddress, double> EmptyValues = new Dictionary<ParameterAddress, double>();
    private static readonly IReadOnlyDictionary<Guid, EnvelopePlayhead> EmptyPlayheads = new Dictionary<Guid, EnvelopePlayhead>();
    private long rangeRevision = -1;
    private Guid rangeGraphId;
    private GraphParameterTelemetry? telemetry;
    private long performanceEpoch;
    // The last compatible single selection owns the dock independently of canvas selection.
    private (Guid GraphId, Guid NodeId)? performanceAddress;
    private string? performanceLabel;
    private GraphNodeSnapshot? PerformanceTarget => performanceAddress is { } address
        ? session.Snapshot.FindGraph(address.GraphId)?.Nodes.SingleOrDefault(node => node.Id == address.NodeId && IsPerformanceNode(node)) : null;
    private static bool IsPerformanceNode(GraphNodeSnapshot node) => node.TypeKey is NoteSequencerGenerator.NodeType or NoteSourceGenerator.NodeType;
    private void ReconcilePerformanceTarget()
    {
        if (PerformanceTarget is null) { performanceAddress = null; performanceLabel = null; }
        var view = Canvas.Snapshot;
        if (view.SelectedNodeIds.Count == 1 && view.Graph.Nodes.SingleOrDefault(node =>
            node.Id == view.SelectedNodeIds[0] && IsPerformanceNode(node)) is { } selected)
            performanceAddress = (view.GraphId, selected.Id);
        if (performanceAddress is { } address && address.GraphId == view.GraphId && PerformanceTarget is { } target)
        {
            var alias = view.NodeDescriptions.GetValueOrDefault(address.NodeId)?.Alias;
            performanceLabel = string.IsNullOrWhiteSpace(alias) ? target.Name : alias;
        }
    }
    private double? PerformanceOffset(Guid graphId, Guid nodeId) => telemetry is { } frame && frame.GraphId == session.GraphId && frame.Revision == session.Snapshot.Revision &&
        frame.PitchOffsets.TryGetValue(graphId, out var offsets) && offsets.TryGetValue(nodeId, out var value) ? value : null;
    private int? PerformanceStep(Guid graphId, Guid nodeId) => telemetry is { } frame && frame.GraphId == session.GraphId && frame.Revision == session.Snapshot.Revision &&
        frame.PitchSteps.TryGetValue(graphId, out var steps) && steps.TryGetValue(nodeId, out var value) && value > 0 ? value : null;
    private void ResetPerformance() => performanceEpoch++;
    private int? responseSampleRate;
    private IReadOnlyDictionary<ParameterAddress, double> EffectiveValues =>
        telemetry is { } frame && frame.GraphId == session.GraphId && frame.Revision == session.Snapshot.Revision
            ? frame.GetValues(Canvas.Snapshot.GraphId) : EmptyValues;
    private IReadOnlyDictionary<Guid, EnvelopePlayhead> EnvelopePlayheads =>
        telemetry is { } frame && frame.GraphId == session.GraphId && frame.Revision == session.Snapshot.Revision &&
        frame.EnvelopePlayheads.TryGetValue(Canvas.Snapshot.GraphId, out var playheads)
            ? playheads : EmptyPlayheads;
    // Do not use the previous graph's fold state while the new keyed canvas
    // is mounting. The new canvas starts expanded by design.
    private GraphCanvas? ActiveTelemetryCanvas => canvasView is { } view &&
        view.OwnsTelemetryState(Canvas) ? view : null;
    private IReadOnlyDictionary<ParameterAddress, double> CanvasEffectiveValues =>
        ActiveTelemetryCanvas?.FilterTelemetry(EffectiveValues, static address => address.NodeId, EmptyValues) ?? EffectiveValues;
    private IReadOnlyDictionary<Guid, EnvelopePlayhead> CanvasEnvelopePlayheads =>
        ActiveTelemetryCanvas?.FilterTelemetry(EnvelopePlayheads, static id => id, EmptyPlayheads) ?? EnvelopePlayheads;

    private bool HasCanvasTelemetryDemand(GraphCanvas? view) => view is null
        ? EffectiveValues.Count != 0 || EnvelopePlayheads.Count != 0
        : EffectiveValues.Keys.Any(address => view.IsTelemetryNodeExpanded(address.NodeId)) ||
          EnvelopePlayheads.Keys.Any(view.IsTelemetryNodeExpanded);

    // Waveform/spectrum/level capture is independently owned by SignalScope.
    // Only response and envelope overlays need workspace parameter/playhead data.
    private bool HasOverlayTelemetryDemand()
    {
        var snapshot = Canvas.Snapshot;
        if (snapshot.ExpandedVisualizationNodeId is not Guid nodeId) return false;
        return snapshot.VisualizationFor(nodeId).Mode switch
        {
            "response" => EffectiveValues.Keys.Any(address => address.NodeId == nodeId),
            "envelope" => EnvelopePlayheads.ContainsKey(nodeId),
            _ => false
        };
    }

    private IPatchSession session = default!;
    private IDisposable? subscription;
    private CanvasNavigationService Navigation { get; set; } = default!;
    private ICanvasStateService Canvas => Navigation.ActiveCanvas;
    private GraphCanvas? canvasView;
    private GraphPlaybackControls? playbackControls;
    private long workspaceGeneration;
    private bool switchingWorkspace;
    private bool addingNode;
    private bool annotationDraft;
    private bool overviewDraft;
    private SavedPatchOverview overview = SavedPatchOverview.Empty;
    private const string NoteType = "canvas.note";
    private static readonly GridNodePlacementPolicy Placement = new(columnSpacing: 320, rowSpacing: 360, columns: 4);
    private NodeRegistry catalog = default!;
    private IEnumerable<string> RegisteredTypes => catalog.RegisteredTypes.Where(type =>
        Canvas.Snapshot.GraphId == session.GraphId || type != MasterOutput.NodeType).Append(NoteType);
    private IReadOnlyList<NodeLibraryEntry>? rootLibrary, nestedLibrary;
    private IReadOnlySet<string>? libraryKeys;
    private IReadOnlySet<string> LibraryKeys => libraryKeys ??= catalog.RegisteredTypes.Append(NoteType).ToHashSet(StringComparer.Ordinal);
    private IReadOnlyList<NodeLibraryEntry> LibraryEntries => Canvas.Snapshot.GraphId == session.GraphId
        ? rootLibrary ??= BuildLibrary() : nestedLibrary ??= BuildLibrary();
    private IReadOnlyList<NodeLibraryEntry> BuildLibrary() => RegisteredTypes
        .Select(type => new NodeLibraryEntry(type, DescribeType(type), NodeAppearanceCatalog.Get(type))).ToArray();
    private string? Error { get; set; }
    private (Guid GraphId, Guid NodeId)? editingRack;
    private GraphNodeSnapshot? EditingRack => editingRack is { } address && address.GraphId == Canvas.Snapshot.GraphId
        ? Canvas.Snapshot.Graph.Nodes.SingleOrDefault(node => node.Id == address.NodeId) : null;
    private bool ScopeOpen => Canvas.Snapshot.ExpandedVisualizationNodeId is not null;
    private GraphNodeSnapshot? ScopeSelection => Canvas.Snapshot.SelectedNodeIds.Count == 1
        ? Canvas.Snapshot.Graph.Nodes.SingleOrDefault(node => node.Id == Canvas.Snapshot.SelectedNodeIds[0] &&
            NodeVisualizationCapabilities.EligiblePorts(node).Any()) : null;
    private GraphNodeSnapshot? ScopeNode => Canvas.Snapshot.Graph.Nodes.SingleOrDefault(node => node.Id == Canvas.Snapshot.ExpandedVisualizationNodeId);
    private void ToggleScope() => Canvas.SetExpandedVisualization(ScopeOpen ? null : ScopeSelection?.Id);
    private void CloseScope() => Canvas.SetExpandedVisualization(null);
    private void UpdateScopeSettings(Guid graphId, Guid nodeId, NodeVisualizationSettings settings)
    {
        if (Canvas.Snapshot.GraphId == graphId && Canvas.Snapshot.ExpandedVisualizationNodeId == nodeId)
            Canvas.SetVisualization(nodeId, settings);
    }
    private bool HasSelection => DeletableNodes.Any() ||
        Canvas.Snapshot.SelectedConnectionIds.Count > 0 || Canvas.Snapshot.SelectedNoteId is not null || Canvas.Snapshot.SelectedGroupId is not null;
    private bool CanDescribeSelection => Canvas.Snapshot.SelectedNodeIds.Count + Canvas.Snapshot.SelectedConnectionIds.Count == 1;
    private SignalConnection? SelectedConnection => Canvas.Snapshot.SelectedConnectionIds.Count == 1
        ? Canvas.Snapshot.Graph.Connections.SingleOrDefault(edge => edge.Id == Canvas.Snapshot.SelectedConnectionIds[0])
        : null;
    private IEnumerable<Guid> DeletableNodes => Canvas.Snapshot.SelectedNodeIds.Where(id =>
        Canvas.Snapshot.Graph.GetNode(id).TypeKey is not (InputRack.NodeType or OutputRack.NodeType));

    protected override async Task OnInitializedAsync()
    {
        var registry = Registry;
        catalog = registry;
        try
        {
            if (await Persistence.LoadAsync() is { } document)
            {
                session = Serializer.Restore(document, registry);
                Navigation = new CanvasNavigationService(session, Placement, document.CanvasLayouts, document.ActiveGraphId);
                await Transport.SetTempoAsync(document.BeatsPerMinute);
                tempo = document.BeatsPerMinute;
                overview = document.Overview;
            }
        }
        catch (Exception exception)
        {
            Navigation?.Dispose();
            session?.Dispose();
            session = null!;
            // Do not replace an unreadable or newer document with a demo patch.
            autosaveEnabled = false;
            PersistenceError = $"Saved patch could not be restored; autosave is disabled: {exception.Message}";
        }
        try { await PresetLibrary.LoadAsync(); }
        catch (Exception exception) { PresetLibraryError = $"Custom presets could not be loaded: {exception.Message}"; }
        if (disposed) { Navigation?.Dispose(); session?.Dispose(); return; }
        if (session is null)
        {
            session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings(), registry);
            Navigation = new CanvasNavigationService(session, Placement);
        }
        RefreshRanges();
        ReconcilePerformanceTarget();
        SubscribeToNavigation();
        initialized = true;
    }

    private void SubscribeToNavigation()
    {
        subscription = Navigation.Subscribe(change => { _ = InvokeAsync(() =>
        {
            RefreshRanges();
            ReconcilePerformanceTarget();
            StateHasChanged();
            _ = SaveWorkspaceAsync();
        }); });
    }

    private PatchDocument CaptureWorkspace() => Serializer.Capture(session, tempo,
        Canvas.Snapshot.GraphId, Navigation.CaptureLayouts(), overview);

    private async Task SetOverviewDraftAsync(bool active)
    {
        if (active && (annotationDraft || switchingWorkspace))
            throw new InvalidOperationException("Apply or cancel the annotation edit first.");
        overviewDraft = active;
        OnAnnotationDraftChanged(active);
        try
        {
            if (canvasView is not null) await canvasView.SetExternalDraftWarningAsync(active);
        }
        catch
        {
            overviewDraft = !active;
            OnAnnotationDraftChanged(!active);
            throw;
        }
    }

    private void ApplyOverview(SavedPatchOverview value)
    {
        overview = SavedPatchOverview.Validate(value);
        _ = SaveWorkspaceAsync();
    }

    private PatchDocument CaptureCommittedWorkspace()
    {
        if (annotationDraft) throw new InvalidOperationException("Apply or cancel the annotation edit first.");
        return CaptureWorkspace();
    }

    private bool CheckAnnotationDraft()
    {
        if (!annotationDraft) return true;
        Error = "Apply or cancel the annotation edit first.";
        return false;
    }

    private void OnAnnotationDraftChanged(bool active)
    {
        annotationDraft = active;
        if (!active && Error == "Apply or cancel the annotation edit first.") Error = null;
    }

    private async Task NewWorkspaceAsync()
    {
        using var fresh = new PatchSession(catalog);
        fresh.Execute(new AddNodeCommand(fresh.GraphId, MasterOutput.NodeType));
        await ReplaceWorkspaceAsync(Serializer.Capture(fresh, MusicalTransport.DefaultTempo, fresh.GraphId, []));
    }

    // Construct and validate the complete candidate before touching the current workspace.
    // Playback must be stopped; a new generation recreates presentation and playback views.
    private async Task ReplaceWorkspaceAsync(PatchDocument document)
    {
        if (annotationDraft) throw new InvalidOperationException("Apply or cancel the annotation edit first.");
        if (switchingWorkspace || playbackControls?.CanReplaceWorkspace != true)
            throw new InvalidOperationException("Stop playback before opening another patch.");
        switchingWorkspace = true;
        StateHasChanged();
        IPatchSession? candidate = null;
        CanvasNavigationService? candidateNavigation = null;
        try
        {
            candidate = Serializer.Restore(document, catalog);
            candidateNavigation = new CanvasNavigationService(candidate, Placement, document.CanvasLayouts, document.ActiveGraphId);
            await Transport.SetTempoAsync(document.BeatsPerMinute);
            await Transport.ResetAsync();
            try { await Persistence.SaveNowAsync(document); }
            catch
            {
                await Transport.SetTempoAsync(tempo);
                throw;
            }
            subscription?.Dispose();
            Navigation.Dispose();
            session.Dispose();
            session = candidate;
            Navigation = candidateNavigation;
            candidate = null;
            candidateNavigation = null;
            tempo = document.BeatsPerMinute;
            overview = document.Overview;
            rangeRevision = -1;
            telemetry = null;
            editingRack = null;
            managingPresets = null;
            Error = PersistenceError = null;
            autosaveEnabled = true;
            workspaceGeneration++;
            performanceAddress = null;
            performanceLabel = null;
            ReconcilePerformanceTarget();
            outlineOpen = false;
            outlineFocus = null;
            RefreshRanges();
            SubscribeToNavigation();
        }
        finally
        {
            candidateNavigation?.Dispose();
            candidate?.Dispose();
            switchingWorkspace = false;
            StateHasChanged();
        }
    }

    private async Task SaveWorkspaceAsync()
    {
        if (!autosaveEnabled || disposed || switchingWorkspace) return;
        try
        {
            await Persistence.ScheduleSaveAsync(CaptureWorkspace);
            if (!disposed && PersistenceError is not null) { PersistenceError = null; StateHasChanged(); }
        }
        catch (Exception exception)
        {
            if (!disposed) { PersistenceError = $"Autosave failed: {exception.Message}"; StateHasChanged(); }
        }
    }

    private void OnTempoChanged(double value)
    {
        if (tempo == value) return;
        tempo = value;
        _ = SaveWorkspaceAsync();
    }

    // Analyze only graph/revision changes, not viewport changes or meter frames.
    private void RefreshRanges()
    {
        var root = session.Snapshot;
        var graphId = Canvas.Snapshot.GraphId;
        if (rangeRevision == root.Revision && rangeGraphId == graphId) return;
        ModulationRanges = ModulationEstimator.Estimate(root, graphId);
        if (rangeRevision != root.Revision) telemetry = null;
        rangeRevision = root.Revision;
        rangeGraphId = graphId;
    }

    private void OnTelemetryChanged(GraphParameterTelemetry? frame)
    {
        if (disposed) return;
        // Keep the latest valid root frame for navigation and newly opened consumers.
        // Empty active-graph samples do not warrant a whole-workspace render.
        // Still render a non-empty -> empty transition to clear stale readouts.
        var view = ActiveTelemetryCanvas;
        // Use the last actually delivered canvas inputs to clear them once
        // after the last consumer folds, even when its old raw frame is nonempty.
        var hadCanvasTelemetry = view is null
            ? HasCanvasTelemetryDemand(null)
            : view.EffectiveValues.Count != 0 || view.EnvelopePlayheads.Count != 0;
        var hadOverlayTelemetry = HasOverlayTelemetryDemand();
        telemetry = frame is { } current && current.GraphId == session.GraphId &&
            current.Revision == session.Snapshot.Revision ? current : null;
        // The keyboard is independently armed, potentially in a different graph.
        // Preserve its existing update cadence; narrower demand is a later slice.
        if (hadCanvasTelemetry || HasCanvasTelemetryDemand(view) || hadOverlayTelemetry ||
            HasOverlayTelemetryDemand() || performanceAddress is not null)
            StateHasChanged();
    }

    private void OnSampleRateChanged(int? sampleRate) => responseSampleRate = sampleRate;

    private string DescribeType(string type) => type == NoteType ? "Note (annotation)" : catalog.Describe(type);

    private void OpenComposite(Guid graphId, Guid nodeId)
    {
        if (!CheckAnnotationDraft()) return;
        if (graphId != Canvas.Snapshot.GraphId) return;
        outlineFocus = null;
        Error = null;
        session.CompleteHistoryGesture();
        CloseRackEditor();
        ClosePresetManager();
        if (SelectedConnection is not null) Canvas.ClearSelection();
        Navigation.OpenChild(nodeId);
    }

    private void NavigateTo(Guid graphId)
    {
        if (!CheckAnnotationDraft()) return;
        outlineFocus = null;
        Error = null;
        session.CompleteHistoryGesture();
        CloseRackEditor();
        ClosePresetManager();
        if (SelectedConnection is not null) Canvas.ClearSelection();
        Navigation.NavigateTo(graphId);
    }

    private void OpenRackEditor(Guid graphId, Guid nodeId)
    {
        if (graphId != Canvas.Snapshot.GraphId) return;
        var rack = Canvas.Snapshot.Graph.Nodes.SingleOrDefault(node => node.Id == nodeId);
        if (rack?.TypeKey is not (InputRack.NodeType or OutputRack.NodeType)) return;
        editingRack = (graphId, nodeId);
    }

    private void CloseRackEditor() => editingRack = null;

    private async Task OpenPresetManagerAsync(Guid graphId, Guid nodeId)
    {
        if (graphId != Canvas.Snapshot.GraphId) return;
        var node = Canvas.Snapshot.Graph.Nodes.SingleOrDefault(candidate => candidate.Id == nodeId);
        if (node is null || !PresetCatalog.Supports(node.TypeKey)) return;
        try { await PresetLibrary.LoadAsync(); PresetLibraryError = null; }
        catch (Exception exception) { PresetLibraryError = $"Custom presets could not be loaded: {exception.Message}"; }
        if (graphId == Canvas.Snapshot.GraphId) managingPresets = (graphId, nodeId);
    }

    private void ClosePresetManager() => managingPresets = null;
    private void OnPresetLibraryChanged() => PresetLibraryError = null;

    // The reusable editor receives validation feedback at the affected control.
    // A stale callback cannot mutate a different canvas after navigation.
    private string? DispatchRackCommand(IGraphCommand command)
    {
        if (command.GraphId != Canvas.Snapshot.GraphId || EditingRack is null)
            return "This rack is no longer open.";
        return ExecuteForEditor(command);
    }

    private string? DispatchConnectionCommand(SetConnectionAmountCommand command)
    {
        if (command.GraphId != Canvas.Snapshot.GraphId || SelectedConnection?.Id != command.ConnectionId)
            return "This connection is no longer selected.";
        return ExecuteForEditor(command);
    }

    private string? ExecuteForEditor(IGraphCommand command)
    {
        Execute(command);
        var error = Error;
        Error = null;
        return error;
    }

    private async Task<bool> AddNodeAsync(string type)
    {
        if (switchingWorkspace || addingNode || canvasView is null || !CheckAnnotationDraft() || !RegisteredTypes.Contains(type)) return false;
        var canvas = Canvas;
        addingNode = true;
        try
        {
            if (type == NoteType)
            {
                if (!await canvasView.AddNoteAsync()) Error = "The canvas view changed. Try adding the note again.";
                else Error = null;
                return Error is null;
            }
            using var placement = await canvasView.ReserveVisibleNodePlacementAsync();
            if (disposed || switchingWorkspace || !ReferenceEquals(Canvas, canvas)) return false;
            if (placement is null)
            {
                Error = "The canvas view changed. Try adding the node again.";
                return false;
            }
            return Execute(new AddNodeCommand(canvas.Snapshot.GraphId, type));
        }
        catch (Exception exception) { Error = exception.Message; return false; }
        finally { addingNode = false; }
    }

    private void DuplicateNode(Guid graphId, Guid nodeId)
    {
        if (switchingWorkspace || addingNode || graphId != Canvas.Snapshot.GraphId || !CheckAnnotationDraft()) return;
        var before = Canvas.Snapshot;
        if (before.SelectedNodeIds.Count != 1 || before.SelectedNodeIds[0] != nodeId ||
            before.SelectedConnectionIds.Count != 0 ||
            !before.NodePositions.TryGetValue(nodeId, out var position) ||
            before.Graph.GetNode(nodeId).TypeKey is InputRack.NodeType or OutputRack.NodeType or MasterOutput.NodeType)
            return;

        var duplicateId = Guid.NewGuid();
        var description = before.NodeDescriptions.GetValueOrDefault(nodeId);
        var visualization = before.Visualizations.GetValueOrDefault(nodeId);
        if (!Execute(new DuplicateNodeCommand(graphId, nodeId, duplicateId))) return;

        // Keep the copy close to its source while making both headers visible.
        Canvas.MoveNode(duplicateId, position.Add(new CanvasPoint(320, 32)));
        if (description is not null) Canvas.SetNodeDescription(description with { NodeId = duplicateId });
        if (visualization is not null) Canvas.SetVisualization(duplicateId, visualization);
        Canvas.SelectNode(duplicateId);
    }

    private void Undo() => RestoreHistory(session.Undo);

    private void GroupNodes(GroupNodesCommand command)
    {
        if (switchingWorkspace || !CheckAnnotationDraft()) return;
        if (command.GraphId != Canvas.Snapshot.GraphId || command.ExpectedRevision != Canvas.Snapshot.GraphRevision ||
            !command.NodeIds.ToHashSet().SetEquals(Canvas.Snapshot.SelectedNodeIds) || Canvas.Snapshot.SelectedConnectionIds.Count > 0)
        {
            Error = "The selection changed. Review it before creating a composite.";
            return;
        }
        if (Canvas.Snapshot.VisualGroups.Any(group => group.NodeIds.Any(command.NodeIds.Contains)))
        {
            Error = "Remove selected nodes from their visual groups before creating a composite.";
            return;
        }
        CloseRackEditor();
        ClosePresetManager();
        Execute(command);
    }

    private void UngroupComposite(UngroupCompositeCommand command)
    {
        if (switchingWorkspace || !CheckAnnotationDraft()) return;
        if (command.GraphId != Canvas.Snapshot.GraphId || command.ExpectedRevision != Canvas.Snapshot.GraphRevision ||
            Canvas.Snapshot.SelectedNodeIds.Count != 1 || Canvas.Snapshot.SelectedNodeIds[0] != command.CompositeId ||
            Canvas.Snapshot.SelectedConnectionIds.Count > 0)
        {
            Error = "The selection changed. Select one composite before expanding it.";
            return;
        }
        Error = null;
        try
        {
            CloseRackEditor();
            ClosePresetManager();
            var result = Navigation.UngroupComposite(command);
            if (result.SubscriberErrors.Count > 0)
                Error = "The graph was committed, but a view failed to update: " + result.SubscriberErrors[0].Message;
        }
        catch (Exception exception) { Error = exception.Message; }
    }
    private void Redo() => RestoreHistory(session.Redo);
    private void UndoAnnotation() { if (CheckAnnotationDraft()) Canvas.UndoAnnotation(); }
    private void RedoAnnotation() { if (CheckAnnotationDraft()) Canvas.RedoAnnotation(); }

    private void RestoreHistory(Func<GraphCommandResult> operation)
    {
        if (switchingWorkspace || !CheckAnnotationDraft()) return;
        Error = null;
        try
        {
            CloseRackEditor();
            ClosePresetManager();
            var result = operation();
            if (result.SubscriberErrors.Count > 0)
                Error = "History was committed, but a view failed to update: " + result.SubscriberErrors[0].Message;
        }
        catch (Exception exception) { Error = exception.Message; }
    }

    private void Connect(Guid graphId, PortConnectionRequest request)
    {
        if (graphId == Canvas.Snapshot.GraphId)
            Execute(new ConnectNodesCommand(graphId, request.Source, request.Destination));
    }

    private (Guid GraphId, Guid GestureId)? canvasParameterGesture;

    private void EditParameter(Guid graphId, ParameterEditRequest request)
    {
        if (graphId == Canvas.Snapshot.GraphId)
        {
            canvasParameterGesture = request.GestureId is Guid id ? (graphId, id) : null;
            Execute(new SetParameterValueCommand(graphId, request.Address, request.Value), request.GestureId);
        }
    }

    private void CancelCanvasParameterGesture(Guid graphId)
    {
        if (switchingWorkspace || graphId != Canvas.Snapshot.GraphId || canvasParameterGesture is not { } gesture || gesture.GraphId != graphId) return;
        canvasParameterGesture = null;
        try { session.CancelParameterGesture(gesture.GestureId); }
        catch (Exception exception) { Error = exception.Message; }
    }

    private void EditPerformanceParameter(Guid graphId, Guid nodeId, ParameterEditRequest request)
    {
        if (performanceAddress == (graphId, nodeId) && PerformanceTarget is not null && request.Address.NodeId == nodeId)
            Execute(new SetParameterValueCommand(graphId, request.Address, request.Value), request.GestureId);
    }

    private void ClearPitchPattern(Guid graphId, Guid nodeId)
    {
        if (performanceAddress == (graphId, nodeId) && PerformanceTarget is not null)
            Execute(new SetPitchPatternStepsCommand(graphId, nodeId, new int[NoteSequencerGenerator.Capacity]));
    }

    private bool HumanizePitchPattern(Guid graphId, Guid nodeId, int first, int count, PitchHumanization profile)
    {
        if (disposed || performanceAddress != (graphId, nodeId) || PerformanceTarget is not { } target ||
            target.Id != nodeId || target.TypeKey != NoteSequencerGenerator.NodeType)
            return false;
        return Execute(new HumanizePitchPatternCommand(graphId, nodeId, first, count, profile));
    }

    private bool CommitPitchTake(Guid graphId, Guid nodeId, PitchRecordingTake take)
    {
        if (disposed || performanceAddress != (graphId, nodeId) || PerformanceTarget is not { } target || target.Id != nodeId)
            return false;
        var reference = (int)target.Parameters.Single(p => p.Definition.Key == NoteSequencerGenerator.ReferenceKey).BaseValue;
        return take.MatchesSource(PitchPattern.FromNode(target), reference) &&
            Execute(new SetPitchPatternStepsCommand(graphId, nodeId, take.CaptureSteps()));
    }

    private void SetNodeAudition(Guid graphId, NodeAuditionRequest request)
    {
        if (graphId != Canvas.Snapshot.GraphId) return;
        Execute(request.Kind switch
        {
            NodeAuditionKind.Mute => new SetNodeMutedCommand(graphId, request.NodeId, request.Active),
            NodeAuditionKind.Bypass => new SetNodeBypassedCommand(graphId, request.NodeId, request.Active),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        });
    }

    private void ApplyNodePreset(Guid graphId, NodePresetApplyRequest request)
    {
        if (graphId == Canvas.Snapshot.GraphId)
            Execute(new ApplyNodePresetCommand(graphId, request.NodeId, request.Preset));
    }

    private void CompleteParameterGesture(Guid graphId, Guid gestureId)
    {
        if (canvasParameterGesture == (graphId, gestureId)) canvasParameterGesture = null;
        if (!switchingWorkspace && graphId == Canvas.Snapshot.GraphId)
            session.CompleteHistoryGesture(gestureId);
    }

    private void DeleteSelection()
    {
        if (!CheckAnnotationDraft()) return;
        // Snapshot selection before commands reconcile the view. Each node is
        // removed atomically with all its incident edges by SignalGraph.
        var selected = Canvas.Snapshot;
        if (selected.SelectedGroupId is Guid groupId)
        {
            Canvas.DeleteGroup(groupId);
            return;
        }
        if (selected.SelectedNoteId is Guid noteId)
        {
            Canvas.DeleteNote(noteId);
            return;
        }
        var nodes = DeletableNodes.ToArray();
        foreach (var id in selected.SelectedConnectionIds)
            if (!Execute(new RemoveConnectionCommand(selected.GraphId, id))) return;
        foreach (var id in nodes)
            if (!Execute(new RemoveNodeCommand(selected.GraphId, id))) return;
    }

    private bool Execute(IGraphCommand command, Guid? historyGestureId = null)
    {
        if (switchingWorkspace) return false;
        Error = null;
        try
        {
            var result = session.Execute(command, historyGestureId);
            if (result.SubscriberErrors.Count > 0)
                Error = "The graph was committed, but a view failed to update: " + result.SubscriberErrors[0].Message;
            return true;
        }
        catch (Exception exception)
        {
            Error = exception.Message;
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        Persistence.Dispose();
        subscription?.Dispose();
        Navigation?.Dispose();
        session?.Dispose();
        await DisposeMobilePanelsAsync();
    }
}

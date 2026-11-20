using Microsoft.AspNetCore.Components;
using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Models;
using Orbit.Application.Services;
using Orbit.Domain.Parameters;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Presentation.Canvas;

namespace Orbit.UI.Components.Canvas;

/// <summary>Composition and command boundary for the complete patch editor.</summary>
public partial class GraphWorkspace : IDisposable
{
    [Inject] private IGraphModulationRangeEstimator ModulationEstimator { get; set; } = default!;
    private IReadOnlyDictionary<ParameterAddress, ParameterRange?> ModulationRanges { get; set; } = new Dictionary<ParameterAddress, ParameterRange?>();
    private static readonly IReadOnlyDictionary<ParameterAddress, double> EmptyValues = new Dictionary<ParameterAddress, double>();
    private long rangeRevision = -1;
    private Guid rangeGraphId;
    private GraphParameterTelemetry? telemetry;
    private IReadOnlyDictionary<ParameterAddress, double> EffectiveValues =>
        telemetry is { } frame && frame.GraphId == session.GraphId && frame.Revision == session.Snapshot.Revision
            ? frame.GetValues(Canvas.Snapshot.GraphId) : EmptyValues;
    private IPatchSession session = default!;
    private IDisposable? subscription;
    private CanvasNavigationService Navigation { get; set; } = default!;
    private ICanvasStateService Canvas => Navigation.ActiveCanvas;
    private IReadOnlyList<string> catalog = [];
    private IEnumerable<string> RegisteredTypes => catalog.Where(type =>
        Canvas.Snapshot.GraphId == session.GraphId || type != MasterOutput.NodeType);
    private string SelectedType { get; set; } = "";
    private string? Error { get; set; }
    private (Guid GraphId, Guid NodeId)? editingRack;
    private GraphNodeSnapshot? EditingRack => editingRack is { } address && address.GraphId == Canvas.Snapshot.GraphId
        ? Canvas.Snapshot.Graph.Nodes.SingleOrDefault(node => node.Id == address.NodeId) : null;
    private bool HasSelection => DeletableNodes.Any() ||
        Canvas.Snapshot.SelectedConnectionIds.Count > 0;
    private SignalConnection? SelectedConnection => Canvas.Snapshot.SelectedConnectionIds.Count == 1
        ? Canvas.Snapshot.Graph.Connections.SingleOrDefault(edge => edge.Id == Canvas.Snapshot.SelectedConnectionIds[0])
        : null;
    private IEnumerable<Guid> DeletableNodes => Canvas.Snapshot.SelectedNodeIds.Where(id =>
        Canvas.Snapshot.Graph.GetNode(id).TypeKey is not (InputRack.NodeType or OutputRack.NodeType));

    protected override void OnInitialized()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        catalog = registry.RegisteredTypes;
        SelectedType = SineGenerator.NodeType;
        session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings(), registry);
        Navigation = new CanvasNavigationService(session, new GridNodePlacementPolicy(columnSpacing: 320, rowSpacing: 360, columns: 4));
        RefreshRanges();
        subscription = Navigation.Subscribe(change => { _ = InvokeAsync(() =>
        {
            if (!RegisteredTypes.Contains(SelectedType)) SelectedType = SineGenerator.NodeType;
            RefreshRanges();
            StateHasChanged();
        }); });
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

    private void OnTelemetryChanged(GraphParameterTelemetry? frame) =>
        telemetry = frame;

    private static string DescribeType(string type) => type switch
    {
        CompositeNode.NodeType => "Drone (composite)",
        SineGenerator.NodeType => "Sine generator",
        TempoSineGenerator.NodeType => "Tempo sine LFO",
        GainProcessor.NodeType => "Gain",
        MasterOutput.NodeType => "Master output",
        _ => type
    };

    private void OpenComposite(Guid graphId, Guid nodeId)
    {
        if (graphId != Canvas.Snapshot.GraphId) return;
        Error = null;
        CloseRackEditor();
        Navigation.OpenChild(nodeId);
    }

    private void NavigateTo(Guid graphId)
    {
        Error = null;
        CloseRackEditor();
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

    private void AddNode() => Execute(new AddNodeCommand(Canvas.Snapshot.GraphId, SelectedType));

    private void Connect(Guid graphId, PortConnectionRequest request)
    {
        if (graphId == Canvas.Snapshot.GraphId)
            Execute(new ConnectNodesCommand(graphId, request.Source, request.Destination));
    }

    private void EditParameter(Guid graphId, ParameterEditRequest request)
    {
        if (graphId == Canvas.Snapshot.GraphId)
            Execute(new SetParameterValueCommand(graphId, request.Address, request.Value));
    }

    private void DeleteSelection()
    {
        // Snapshot selection before commands reconcile the view. Each node is
        // removed atomically with all its incident edges by SignalGraph.
        var selected = Canvas.Snapshot;
        var nodes = DeletableNodes.ToArray();
        foreach (var id in selected.SelectedConnectionIds)
            if (!Execute(new RemoveConnectionCommand(selected.GraphId, id))) return;
        foreach (var id in nodes)
            if (!Execute(new RemoveNodeCommand(selected.GraphId, id))) return;
    }

    private bool Execute(IGraphCommand command)
    {
        Error = null;
        try
        {
            var result = session.Execute(command);
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

    public void Dispose()
    {
        subscription?.Dispose();
        Navigation?.Dispose();
        session?.Dispose();
    }
}

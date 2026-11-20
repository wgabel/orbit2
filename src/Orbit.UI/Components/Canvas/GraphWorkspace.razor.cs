using Microsoft.AspNetCore.Components;
using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Models;
using Orbit.Application.Services;
using Orbit.Domain.Parameters;
using Orbit.Presentation.Canvas;

namespace Orbit.UI.Components.Canvas;

/// <summary>Composition and graph command boundary for the root editor.</summary>
public partial class GraphWorkspace : IDisposable
{
    [Inject] private IGraphModulationRangeEstimator ModulationEstimator { get; set; } = default!;
    private IReadOnlyDictionary<ParameterAddress, ParameterRange?> ModulationRanges { get; set; } = new Dictionary<ParameterAddress, ParameterRange?>();
    private static readonly IReadOnlyDictionary<ParameterAddress, double> EmptyValues = new Dictionary<ParameterAddress, double>();
    private long rangeRevision = -1;
    private GraphParameterTelemetry? telemetry;
    private IReadOnlyDictionary<ParameterAddress, double> EffectiveValues =>
        telemetry is { } frame && frame.GraphId == Canvas.Snapshot.Graph.Id && frame.Revision == Canvas.Snapshot.Graph.Revision
            ? frame.Values : EmptyValues;
    private IPatchSession session = default!;
    private IDisposable? subscription;
    private ICanvasStateService Canvas { get; set; } = default!;
    private IReadOnlyList<string> RegisteredTypes { get; set; } = [];
    private string SelectedType { get; set; } = "";
    private string? Error { get; set; }
    private bool HasSelection => Canvas.Snapshot.SelectedNodeIds.Count > 0 ||
        Canvas.Snapshot.SelectedConnectionIds.Count > 0;

    protected override void OnInitialized()
    {
        var registry = BuiltInNodes.CreateRegistry();
        RegisteredTypes = registry.RegisteredTypes;
        SelectedType = RegisteredTypes.First();
        session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings(), registry);
        Canvas = new CanvasStateService(session, new GridNodePlacementPolicy(columnSpacing: 320, rowSpacing: 360, columns: 4));
        RefreshRanges();
        subscription = Canvas.Subscribe(change => { _ = InvokeAsync(() =>
        {
            RefreshRanges();
            StateHasChanged();
        }); });
    }

    // Analyze only committed graph revisions, not viewport changes or meter frames.
    private void RefreshRanges()
    {
        var graph = Canvas.Snapshot.Graph;
        if (rangeRevision == graph.Revision) return;
        ModulationRanges = ModulationEstimator.Estimate(graph);
        rangeRevision = graph.Revision;
        telemetry = null;
    }

    private void OnTelemetryChanged(GraphParameterTelemetry? frame) =>
        telemetry = frame;

    private void AddNode() => Execute(new AddNodeCommand(session.GraphId, SelectedType));

    private void Connect(PortConnectionRequest request) =>
        Execute(new ConnectNodesCommand(session.GraphId, request.Source, request.Destination));

    private void EditParameter(ParameterEditRequest request) =>
        Execute(new SetParameterValueCommand(session.GraphId, request.Address, request.Value));

    private void DeleteSelection()
    {
        // Snapshot selection before commands reconcile the view. Each node is
        // removed atomically with all its incident edges by SignalGraph.
        var selected = Canvas.Snapshot;
        foreach (var id in selected.SelectedConnectionIds)
            if (!Execute(new RemoveConnectionCommand(session.GraphId, id))) return;
        foreach (var id in selected.SelectedNodeIds)
            if (!Execute(new RemoveNodeCommand(session.GraphId, id))) return;
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
        Canvas?.Dispose();
        session?.Dispose();
    }
}

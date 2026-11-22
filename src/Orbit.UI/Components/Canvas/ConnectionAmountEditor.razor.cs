using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Graphs;
using Orbit.Domain.Ports;
using Orbit.Presentation.Canvas;
using Orbit.UI.Components.Parameters;

namespace Orbit.UI.Components.Canvas;

/// <summary>Presentation-only anchored editor for a single selected connection.</summary>
public partial class ConnectionAmountEditor : IAsyncDisposable
{
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Parameter, EditorRequired] public GraphSnapshot Graph { get; set; } = default!;
    [Parameter, EditorRequired] public SignalConnection Connection { get; set; } = default!;
    [Parameter, EditorRequired] public Func<SetConnectionAmountCommand, string?> Dispatch { get; set; } = default!;
    [Parameter, EditorRequired] public CanvasPoint Anchor { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }
    [Parameter] public string AuthoredLabel { get; set; } = "";
    [Parameter] public EventCallback OnEditDescription { get; set; }
    [Parameter] public bool DescriptionEditDisabled { get; set; }

    private ElementReference root;
    private IJSObjectReference? module;
    private IJSObjectReference? placement;
    private Task? initialization;
    private bool disposed;

    private readonly string inputId = $"connection-amount-{Guid.NewGuid():N}";
    private string InputId => inputId;
    private string ErrorId => $"{inputId}-error";
    private string Draft { get; set; } = "";
    private string? Error { get; set; }
    private double? committedAmount;
    private GraphNodeSnapshot Source => Graph.GetNode(Connection.Source.NodeId);
    private GraphNodeSnapshot Destination => Graph.GetNode(Connection.Destination.NodeId);
    private PortDefinition SourcePort => Source.Outputs.Single(port => port.Id == Connection.Source.PortId);
    private PortDefinition DestinationPort => Destination.Inputs.Single(port => port.Id == Connection.Destination.PortId);
    private ParameterSnapshot? TargetParameter => Destination.Parameters.SingleOrDefault(parameter =>
        parameter.ModulationPortId == Connection.Destination.PortId);
    private bool IsModulation => DestinationPort.SignalType == SignalType.Modulation;
    private bool IsEvent => DestinationPort.SignalType.IsEvent();
    private string RouteName => $"{Source.Name}: {SourcePort.Name} → {Destination.Name}: {DestinationPort.Name}";
    private string AmountLabel => IsModulation ? "Modulation depth" : "Audio gain";
    private string UnitLabel => TargetParameter is { } target
        ? ParameterUnitLabel.Format(target.Definition.Unit) : "multiplier";
    private string Explanation => IsModulation && TargetParameter is not null
        ? "The incoming signal is multiplied by this depth and added to the target base value. Parameter limits still apply."
        : "The incoming signal is multiplied by this amount. Negative values invert it; zero silences this route.";

    protected override void OnParametersSet()
    {
        // Other graph updates and live telemetry must not erase an unfinished edit.
        if (committedAmount == Connection.Amount) return;
        committedAmount = Connection.Amount;
        Draft = Connection.Amount.ToString("G", CultureInfo.InvariantCulture);
        Error = null;
    }

    private void EditDraft(ChangeEventArgs args)
    {
        Draft = args.Value?.ToString() ?? "";
        Error = null;
    }

    private void Apply()
    {
        if (IsEvent) return;
        if (!double.TryParse(Draft, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount)
            || !double.IsFinite(amount))
        {
            Error = "Enter a finite number.";
            return;
        }
        Error = Dispatch(new SetConnectionAmountCommand(Graph.Id, Connection.Id, amount));
    }

    private Task HandleKeyDown(KeyboardEventArgs args) => args.Key == "Escape"
        ? OnCancel.InvokeAsync() : Task.CompletedTask;

    protected override Task OnAfterRenderAsync(bool firstRender) =>
        firstRender ? initialization = InitializeAsync() : Task.CompletedTask;

    private async Task InitializeAsync()
    {
        module = await JavaScript.InvokeAsync<IJSObjectReference>("import", "./js/canvasDom.js");
        if (!disposed)
            placement = await module.InvokeAsync<IJSObjectReference>("mountConnectionEditor", root);
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        if (initialization is not null) await initialization;
        try
        {
            if (placement is not null)
            {
                try { await placement.InvokeVoidAsync("dispose"); }
                finally { await placement.DisposeAsync(); }
            }
        }
        finally
        {
            if (module is not null) await module.DisposeAsync();
        }
    }
}

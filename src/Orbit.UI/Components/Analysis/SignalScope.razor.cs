using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Orbit.Application.Models;
using Orbit.Application.Persistence;
using Orbit.Application.Services;

namespace Orbit.UI.Components.Analysis;

/// <summary>Owns only the address and browser view lifetime; drawing stays in JavaScript.</summary>
public partial class SignalScope : IAsyncDisposable
{
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private ISignalAnalysisService Analysis { get; set; } = default!;
    [Inject] private ScopeBrowserAssets BrowserAssets { get; set; } = default!;
    [Parameter, EditorRequired] public SignalObservationAddress Address { get; set; }
    [Parameter] public bool Compact { get; set; }
    [Parameter] public bool ExternalModeControl { get; set; }
    [Parameter] public double? CycleSeconds { get; set; }
    [Parameter, EditorRequired] public NodeVisualizationSettings Settings { get; set; } = NodeVisualizationSettings.Default;
    [Parameter] public EventCallback<NodeVisualizationSettings> SettingsChanged { get; set; }

    // The browser owns these native controls together with the live renderer.
    // Forward edits explicitly rather than combining DOM-owned and Blazor-owned change handlers.
    [JSInvokable]
    public Task ChangeSettings(string mode, string timebase, string trigger, string fftSize, string window, string smoothing) =>
        InvokeAsync(() => disposed ? Task.CompletedTask : SettingsChanged.InvokeAsync(Settings with
        {
            Mode = mode, Timebase = timebase, Trigger = trigger, FftSize = fftSize,
            Window = window, Smoothing = smoothing
        }));

    private ElementReference root;
    private IJSObjectReference? module;
    private IJSObjectReference? view;
    private DotNetObjectReference<SignalScope>? reference;
    private readonly Guid lease = Guid.NewGuid();
    private Task? initialization;
    private bool disposed;
    private string? error;

    [JSInvokable]
    public SignalObservationBinding ResolveBinding() =>
        disposed ? new(SignalObservationAvailability.Inactive) : Analysis.Resolve(Address);

    protected override Task OnAfterRenderAsync(bool firstRender) =>
        firstRender ? initialization = InitializeAsync() : Task.CompletedTask;

    private async Task InitializeAsync()
    {
        try
        {
            module = await JavaScript.InvokeAsync<IJSObjectReference>("import", BrowserAssets.ScopeModulePath);
            if (disposed) return;
            reference = DotNetObjectReference.Create(this);
            view = await module.InvokeAsync<IJSObjectReference>("mountScope", root, BrowserAssets.AudioModulePath, reference, lease, Settings);
        }
        catch (JSException)
        {
            error = "Scope unavailable";
            if (!disposed) StateHasChanged();
        }
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        if (initialization is not null) await initialization;
        try
        {
            if (view is not null)
            {
                try { await view.InvokeVoidAsync("dispose"); }
                finally { await view.DisposeAsync(); }
            }
        }
        finally
        {
            reference?.Dispose();
            if (module is not null) await module.DisposeAsync();
        }
    }
}

using Microsoft.JSInterop;
using Orbit.Application.Persistence;

namespace Orbit.Infrastructure.Persistence;

public sealed class BrowserPatchStorage(IJSRuntime runtime) : IPatchStorage, IAsyncDisposable
{
    private Task<IJSObjectReference>? module;
    private Task<IJSObjectReference> GetModuleAsync() => module ??= runtime.InvokeAsync<IJSObjectReference>(
        "import", "./_content/Orbit.Infrastructure/persistence/patchStorage.js").AsTask();

    public async Task<string?> LoadAsync() => await (await GetModuleAsync()).InvokeAsync<string?>("load");
    public async Task SaveAsync(string json) => await (await GetModuleAsync()).InvokeVoidAsync("save", json);
    public async ValueTask DisposeAsync()
    {
        if (module is { IsCompletedSuccessfully: true }) await module.Result.DisposeAsync();
    }
}

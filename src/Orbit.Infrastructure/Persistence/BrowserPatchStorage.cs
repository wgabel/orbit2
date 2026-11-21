using Microsoft.JSInterop;
using Orbit.Application.Persistence;
using Orbit.Application.Presets;

namespace Orbit.Infrastructure.Persistence;

public sealed class BrowserPatchStorage(IJSRuntime runtime) : IPatchStorage, IPatchLibraryStorage, INodePresetStorage, IAsyncDisposable
{
    private Task<IJSObjectReference>? module;
    private Task<IJSObjectReference> GetModuleAsync() => module ??= runtime.InvokeAsync<IJSObjectReference>(
        "import", BrowserModulePaths.GetPath("persistence", "patchStorage.js")).AsTask();

    public async Task<string?> LoadAsync() => await (await GetModuleAsync()).InvokeAsync<string?>("load");
    public async Task SaveAsync(string json) => await (await GetModuleAsync()).InvokeVoidAsync("save", json);
    public async Task<string[]> ListNamesAsync() => await (await GetModuleAsync()).InvokeAsync<string[]>("listNames");
    public async Task<string?> ReadAsync(string name) => await (await GetModuleAsync()).InvokeAsync<string?>("readNamed", name);
    public async Task WriteAsync(string name, string json, bool overwrite) => await (await GetModuleAsync()).InvokeVoidAsync("writeNamed", name, json, overwrite);
    public async Task DeleteAsync(string name) => await (await GetModuleAsync()).InvokeVoidAsync("deleteNamed", name);
    public async Task DownloadAsync(string name, string json) => await (await GetModuleAsync()).InvokeVoidAsync("download", name, json);
    public async Task<string?> LoadCustomAsync() => await (await GetModuleAsync()).InvokeAsync<string?>("loadNodePresets");
    public async Task<bool> ReplaceCustomAsync(string? expectedJson, string json) =>
        await (await GetModuleAsync()).InvokeAsync<bool>("replaceNodePresets", expectedJson, json);
    public async ValueTask DisposeAsync()
    {
        if (module is { IsCompletedSuccessfully: true }) await module.Result.DisposeAsync();
    }
}

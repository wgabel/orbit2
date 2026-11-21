namespace Orbit.UI.Components.Analysis;

/// <summary>Composition-root configuration: use the identical versioned audio module as playback.</summary>
public sealed record ScopeBrowserAssets(string AudioModulePath)
{
    // The entry module and its DOM contract must match the deployed Blazor
    // build. The audio module URL already carries the deployment identity.
    public string ScopeModulePath => $"./js/scopeView.js?v={Uri.EscapeDataString(AudioModulePath)}";
}

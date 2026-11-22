namespace Orbit.UI.Components.Analysis;

/// <summary>Composition-root configuration: use the identical versioned audio module as playback.</summary>
public sealed record ScopeBrowserAssets(string AudioModulePath)
{
    // Relative imports stay inside the same commit-versioned editor module tree.
    public string ScopeModulePath => BrowserBuild.ModulePath("scopeView.js");
}

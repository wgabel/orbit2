using Microsoft.AspNetCore.Components;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Presets;
using Orbit.Presentation.Canvas;

namespace Orbit.UI.Components.Canvas;

/// <summary>Projects catalog choices and matching state from committed base configuration.</summary>
public partial class NodePresetSelector
{
    [Inject] private NodePresetService Catalog { get; set; } = default!;
    [Parameter, EditorRequired] public GraphNodeSnapshot Node { get; set; } = default!;
    [Parameter] public NodeAppearance? Appearance { get; set; }
    [Parameter] public EventCallback<NodePresetApplyRequest> OnApply { get; set; }
    [Parameter] public EventCallback<Guid> OnManage { get; set; }
    [Parameter] public long CatalogRevision { get; set; }

    private GraphNodeSnapshot? previousNode;
    private long previousRevision = -1;
    private NodePreset? matchingPreset;
    private IReadOnlyList<NodePresetGroup> Groups { get; set; } = [];
    private string InputId => $"node-preset-{Node.Id:N}";
    private string DescriptionId => $"{InputId}-description";
    private string SelectedPresetId => matchingPreset?.Id ?? string.Empty;
    private string Description => matchingPreset is null ? "No matching preset. Choose one to load."
        : matchingPreset.Description ?? "Current settings match this preset.";

    protected override void OnParametersSet()
    {
        // Layout and telemetry refreshes reuse the immutable node snapshot.
        if (ReferenceEquals(previousNode, Node) && previousRevision == CatalogRevision) return;
        previousNode = Node;
        previousRevision = CatalogRevision;
        Groups = Catalog.GetGroups(Node.TypeKey);
        matchingPreset = Catalog.FindMatchingPreset(Node);
    }

    private async Task SelectPresetAsync(string presetId)
    {
        var preset = Groups.SelectMany(group => group.Presets).FirstOrDefault(preset => preset.Id == presetId);
        if (preset is not null)
            await OnApply.InvokeAsync(new NodePresetApplyRequest(Node.Id, preset));
    }
}

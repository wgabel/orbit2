using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Persistence;
using Orbit.Domain.Ports;
using Orbit.Domain.Parameters;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Timing;
using Orbit.Presentation.Canvas;

namespace Orbit.UI.Components.Canvas;

/// <summary>Reusable node shell; the owning canvas supplies graph data and coordinates.</summary>
public partial class NodeFrame
{
    [Parameter, EditorRequired] public GraphNodeSnapshot Node { get; set; } = default!;
    [Parameter] public Guid PatchId { get; set; }
    [Parameter] public Guid GraphId { get; set; }
    [Parameter] public SavedNodeDescription? Documentation { get; set; }
    [Parameter] public EventCallback<Guid> OnHelp { get; set; }
    [Parameter] public bool HelpOpen { get; set; }
    [Parameter] public EventCallback OnEditDescription { get; set; }
    [Parameter] public bool DocumentationEditDisabled { get; set; }
    [Parameter] public bool ShowSecondaryDocumentation { get; set; } = true;
    private string DisplayName => Documentation is { } metadata && !string.IsNullOrWhiteSpace(metadata.Alias) ? metadata.Alias : Node.Name;
    private string DocumentationTooltip => Documentation is null ? Node.Name :
        ShowSecondaryDocumentation ? $"{DisplayName} ({Node.Name})\n{Documentation.Description}" : $"{DisplayName} ({Node.Name})";
    [Parameter] public NodeVisualizationSettings Visualization { get; set; } = NodeVisualizationSettings.Default;
    [Parameter] public bool VisualizationExpanded { get; set; }
    [Parameter] public EventCallback<NodeVisualizationSettings> OnVisualizationChange { get; set; }
    [Parameter] public EventCallback OnExpandVisualization { get; set; }
    [Parameter] public CanvasPoint Position { get; set; }
    [Parameter] public NodeVisualRole Role { get; set; }
    [Parameter] public bool Selected { get; set; }
    [Parameter] public bool Expanded { get; set; }
    [Parameter] public EventCallback<Guid> OnToggleExpand { get; set; }
    [Parameter] public double BeatsPerMinute { get; set; } = MusicalTransport.DefaultTempo;
    [Parameter] public int? SampleRate { get; set; }
    [Parameter] public PortAddress? PendingSource { get; set; }
    [Parameter] public IReadOnlySet<PortAddress>? WireTargets { get; set; }
    [Parameter] public EventCallback<PointerEventArgs> OnHeaderPointerDown { get; set; }
    [Parameter] public EventCallback<PointerEventArgs> OnBodyPointerDown { get; set; }
    [Parameter] public EventCallback<PortAddress> OnPortActivate { get; set; }
    [Parameter] public EventCallback<PortPointerStart> OnOutputPointerDown { get; set; }
    [Parameter] public EventCallback<PointerEventArgs> OnInputPointerDown { get; set; }
    [Parameter] public EventCallback<ParameterEditRequest> OnParameterEdit { get; set; }
    [Parameter] public EventCallback<MacroEditRequest> OnMacroEdit { get; set; }
    [Parameter] public EventCallback<NodeAuditionRequest> OnAudition { get; set; }
    [Parameter] public EventCallback<NodePresetApplyRequest> OnPresetApply { get; set; }
    [Parameter] public EventCallback<Guid> OnManagePresets { get; set; }
    [Parameter] public long PresetCatalogRevision { get; set; }
    [Parameter] public EventCallback<Guid> OnParameterGestureEnded { get; set; }
    [Parameter] public EventCallback<Guid> OnOpenComposite { get; set; }
    [Parameter] public EventCallback<Guid> OnEditRack { get; set; }
    [Parameter] public IReadOnlyDictionary<ParameterAddress, ParameterRange?> ModulationRanges { get; set; } = new Dictionary<ParameterAddress, ParameterRange?>();
    [Parameter] public IReadOnlyDictionary<ParameterAddress, double> EffectiveValues { get; set; } = new Dictionary<ParameterAddress, double>();
    [Parameter] public Orbit.Application.Models.EnvelopePlayhead? Playhead { get; set; }

    // A frame is keyed by node ID within a single canvas. Parent callback
    // closures address that same node and canvas throughout its lifetime.
    // Child components retain their independent event-driven render lifecycle.
    [Parameter] public bool CountRenders { get; set; }
    private long renderCount;
    private RenderInputs? rendered;

    private readonly record struct RenderInputs(
        GraphNodeSnapshot Node, Guid PatchId, Guid GraphId, CanvasPoint Position,
        SavedNodeDescription? Documentation, bool ShowSecondaryDocumentation,
        bool DocumentationEditDisabled, bool HelpOpen, NodeVisualizationSettings Visualization,
        bool VisualizationExpanded, NodeVisualRole Role, bool Selected, bool Expanded,
        double BeatsPerMinute, int? SampleRate, PortAddress? PendingSource,
        IReadOnlyDictionary<ParameterAddress, ParameterRange?> Ranges,
        IReadOnlyDictionary<ParameterAddress, double> Values,
        Orbit.Application.Models.EnvelopePlayhead? Playhead, long PresetCatalogRevision,
        bool CountRenders);

    private RenderInputs CurrentInputs => new(Node, PatchId, GraphId, Position, Documentation,
        ShowSecondaryDocumentation, DocumentationEditDisabled, HelpOpen, Visualization,
        VisualizationExpanded, Role, Selected, Expanded, BeatsPerMinute, SampleRate, PendingSource,
        ModulationRanges, EffectiveValues, Playhead, PresetCatalogRevision, CountRenders);

    // Wire candidates may change in place. Conservatively render both entry
    // and exit until dedicated wire invalidation is implemented.
    private bool renderedWithWireTargets;
    protected override bool ShouldRender() =>
        WireTargets is not null || renderedWithWireTargets || rendered != CurrentInputs;

    private void RecordRender()
    {
        rendered = CurrentInputs;
        renderedWithWireTargets = WireTargets is not null;
        if (CountRenders) renderCount++;
    }

    private string? DiagnosticRenderCount => CountRenders
        ? renderCount.ToString(CultureInfo.InvariantCulture) : null;

    // Presentation metadata only: macro identity and authored values remain
    // in the composite snapshot, not in synthetic graph parameters.
    private static ParameterDefinition MacroSliderDefinition(CompositeMacroDefinition macro) =>
        new(macro.Id.ToString("N"), macro.Name, macro.DefaultValue, 0, 1,
            ParameterUnit.Normalized, ParameterScale.Linear, false, ParameterSmoothing.None);

    private bool IsModulated(Guid id) => ModulationRanges.ContainsKey(new ParameterAddress(Node.Id, id));
    private ParameterRange? GetRange(Guid id) => ModulationRanges.GetValueOrDefault(new ParameterAddress(Node.Id, id));
    private double? GetEffective(Guid id) => EffectiveValues.TryGetValue(new ParameterAddress(Node.Id, id), out var value) ? value : null;

    private bool Compactable => NodeExpansionState.IsEligible(Node);
    private bool ShowFullControls => !Compactable || Expanded;
    private NodeAppearance Appearance => NodeAppearanceCatalog.Get(Node.TypeKey, Role);
    private string FrameClass => $"node-frame {Appearance.CssClass} node-role-{Role.ToString().ToLowerInvariant()}{(Selected ? " node-selected" : "")}{(Node.IsMuted ? " node-muted" : "")}{(Node.IsBypassed ? " node-bypassed" : "")}{(Compactable ? " node-compactable" : "")}{(Compactable && Expanded ? " node-expanded" : "")}";
    private string PositionStyle => FormattableString.Invariant($"left:{Position.X}px;top:{Position.Y}px;");
}

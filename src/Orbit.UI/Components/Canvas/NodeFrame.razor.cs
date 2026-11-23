using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Persistence;
using Orbit.Domain.Ports;
using Orbit.Domain.Parameters;
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
    [Parameter] public bool Pinned { get; set; }
    [Parameter] public EventCallback<Guid> OnToggleExpanded { get; set; }
    [Parameter] public EventCallback<Guid> OnTogglePinned { get; set; }
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

    private bool IsModulated(Guid id) => ModulationRanges.ContainsKey(new ParameterAddress(Node.Id, id));
    private ParameterRange? GetRange(Guid id) => ModulationRanges.GetValueOrDefault(new ParameterAddress(Node.Id, id));
    private double? GetEffective(Guid id) => EffectiveValues.TryGetValue(new ParameterAddress(Node.Id, id), out var value) ? value : null;

    // Composite and routing shells retain their established full presentation.
    private bool CanExpand => Node.Composite is null &&
        Node.TypeKey is not (Orbit.Domain.Nodes.Composites.InputRack.NodeType or
            Orbit.Domain.Nodes.Composites.OutputRack.NodeType or
            Orbit.Domain.Nodes.Outputs.MasterOutput.NodeType);

    private Orbit.Application.Graphs.Snapshots.ParameterSnapshot? ParameterForPort(Guid portId) =>
        Node.Parameters.FirstOrDefault(parameter => parameter.ModulationPortId == portId);

    private IEnumerable<Orbit.Application.Graphs.Snapshots.ParameterSnapshot> CompactUnportedParameters =>
        Node.Parameters.Where(parameter => parameter.ModulationPortId is null &&
            (Node.TypeKey != Orbit.Domain.Nodes.Generators.NoteSequencerGenerator.NodeType ||
             !parameter.Definition.Key.StartsWith("pattern-step-", StringComparison.Ordinal)));

    private Task EditCompactAsync(Guid parameterId, double value) =>
        OnParameterEdit.InvokeAsync(new ParameterEditRequest(new ParameterAddress(Node.Id, parameterId), value));

    private string EnvelopePoints => string.Join(" ", EnvelopePreview.Create(Node).Points.Select(point =>
        FormattableString.Invariant($"{point.X * 120:0.##},{(1 - point.Level) * 32 + 1:0.##}")));

    private NodeAppearance Appearance => NodeAppearanceCatalog.Get(Node.TypeKey, Role);
    private string FrameClass => $"node-frame {Appearance.CssClass} node-role-{Role.ToString().ToLowerInvariant()}{(Selected ? " node-selected" : "")}{(Node.IsMuted ? " node-muted" : "")}{(Node.IsBypassed ? " node-bypassed" : "")}{(CanExpand ? (Expanded ? " node-expanded" : " node-compact") : "")}";
    private string PositionStyle => FormattableString.Invariant($"left:{Position.X}px;top:{Position.Y}px;");
}

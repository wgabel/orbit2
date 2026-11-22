namespace Orbit.Presentation.Canvas;

/// <summary>Presentation-only notification categories; never audio graph commands.</summary>
public enum CanvasChangeKind
{
    GraphCommitted,
    NodeMoved,
    SelectionChanged,
    ViewportChanged,
    GraphRemoved,
    ConnectionLabelsChanged,
    VisualizationChanged,
    NotesChanged,
    DescriptionsChanged,
    GroupsChanged,
    AnnotationVisibilityChanged
}

using Orbit.Application.Persistence;

namespace Orbit.Presentation.Canvas;

public enum AnnotationDetail { Full, Titles, Marker, Hidden }

/// <summary>Screen-space detail bands with hysteresis. Never modifies authored geometry or collapse.</summary>
public static class AnnotationDetailPolicy
{
    public const double TitlesEnter = .65;
    public const double TitlesExit = .75;
    public const double MarkerEnter = .35;
    public const double MarkerExit = .45;
    public const double MarkerHeight = 30;
    public const double TitleWidth = 170;
    public const double CompactWidth = 100;

    public static AnnotationDetail Initial(double zoom) => zoom <= MarkerEnter ? AnnotationDetail.Marker :
        zoom <= TitlesEnter ? AnnotationDetail.Titles : AnnotationDetail.Full;

    public static AnnotationDetail Update(double zoom, AnnotationDetail previous) => previous switch
    {
        AnnotationDetail.Full => Initial(zoom),
        AnnotationDetail.Titles => zoom <= MarkerEnter ? AnnotationDetail.Marker :
            zoom >= TitlesExit ? AnnotationDetail.Full : AnnotationDetail.Titles,
        AnnotationDetail.Marker => zoom >= TitlesExit ? AnnotationDetail.Full :
            zoom >= MarkerExit ? AnnotationDetail.Titles : AnnotationDetail.Marker,
        _ => Initial(zoom)
    };

    public static AnnotationDetail Resolve(AnnotationVisibility preference, AnnotationDetail zoomDetail, bool selected) =>
        selected ? AnnotationDetail.Full : preference switch
        {
            AnnotationVisibility.Hide => AnnotationDetail.Hidden,
            AnnotationVisibility.TitlesOnly when zoomDetail == AnnotationDetail.Full => AnnotationDetail.Titles,
            _ => zoomDetail
        };

    public static double MarkerWidth(double worldWidth, double zoom, AnnotationDetail detail) =>
        Math.Min(worldWidth * zoom, detail == AnnotationDetail.Marker ? CompactWidth : TitleWidth);

    /// <summary>Fit uses stable authored bounds in Show mode (zoom may expand a card);
    /// in TitlesOnly reserve only its compact header. Hidden items contribute nothing unless selected.</summary>
    public static IEnumerable<CanvasBounds> FitBounds(CanvasStateSnapshot snapshot)
    {
        foreach (var note in snapshot.Notes)
        {
            var detail = snapshot.NoteDetail(note.Id);
            if (detail == AnnotationDetail.Hidden) continue;
            if (detail == AnnotationDetail.Full || snapshot.AnnotationVisibility == AnnotationVisibility.Show)
                yield return CanvasStateService.NoteBounds(note);
            else
                yield return new CanvasBounds(note.X, note.Y, note.X + note.Width,
                    note.Y + MarkerHeight / CanvasViewport.MinimumZoom);
        }
        foreach (var group in snapshot.VisualGroups)
            if (snapshot.GroupDetail(group.Id) != AnnotationDetail.Hidden)
                yield return CanvasStateService.GroupBounds(group);
    }
}

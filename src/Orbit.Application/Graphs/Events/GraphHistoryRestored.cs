using Orbit.Application.Graphs.Snapshots;

namespace Orbit.Application.Graphs.Events;

/// <summary>A history operation replaced an affected graph atomically. Removed/added graphs have a null side.</summary>
public sealed record GraphHistoryRestored(Guid GraphId, GraphSnapshot? Before, GraphSnapshot? After, bool IsUndo) : IGraphEvent;

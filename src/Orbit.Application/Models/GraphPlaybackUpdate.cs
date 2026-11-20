namespace Orbit.Application.Models;

/// <summary>The audible (or starting) revision and whether a newer snapshot needs a restart.</summary>
public sealed record GraphPlaybackUpdate(long AppliedRevision, bool RequiresRestart, bool IsActive);

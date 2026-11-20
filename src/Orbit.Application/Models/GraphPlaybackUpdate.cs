namespace Orbit.Application.Models;

/// <summary>The most recently accepted playback revision and whether it is still active.</summary>
public sealed record GraphPlaybackUpdate(long AppliedRevision, bool IsActive);

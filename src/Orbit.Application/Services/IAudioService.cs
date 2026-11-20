namespace Orbit.Application.Services;

/// <summary>
/// Presentation-facing audio use cases. The UI does not construct playback
/// requests or know which engine implements them.
/// </summary>
public interface IAudioService
{
    Task InitializeAsync();
    Task PlayTestToneAsync();
}

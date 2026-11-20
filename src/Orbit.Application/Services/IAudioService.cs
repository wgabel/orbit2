namespace Orbit.Application.Services;

/// <summary>
/// Application-facing contract for the audio adapter. It does not expose
/// browser, JavaScript or Faust types to the presentation layer.
/// </summary>
public interface IAudioService
{
    /// <summary>Prepares the audio adapter without starting audio playback.</summary>
    Task InitializeAsync();

    /// <summary>Plays the short Faust test tone.</summary>
    Task PlayTestToneAsync();
}

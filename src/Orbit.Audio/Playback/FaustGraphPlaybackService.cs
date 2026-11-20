using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Interfaces;
using Orbit.Application.Models;
using Orbit.Application.Services;
using Orbit.Audio.Compilation;

namespace Orbit.Audio.Playback;

/// <summary>Maps a committed graph to an engine request. The compiler and
/// engine are interchangeable dependencies; the UI knows neither Faust nor JS.</summary>
public sealed class FaustGraphPlaybackService(FaustGraphCompiler compiler, IAudioEngine engine)
    : IGraphPlaybackService
{
    // Leave headroom at the browser output in addition to graph-defined gain.
    private const double PlaybackVolume = 0.5;

    public Task InitializeAsync() => engine.InitializeAsync();

    public Task PlayAsync(GraphSnapshot graph)
    {
        var program = compiler.Compile(graph);
        return engine.PlayAsync(new PlaybackRequest(
            SoundId(graph.Id), "OrbitGraph", PlaybackVolume,
            program.Controls.ToDictionary(control => control.Name, control => control.InitialValue),
            Source: program.Source));
    }

    public Task StopAsync(Guid graphId) => engine.StopAsync(SoundId(graphId));

    public Task<PlaybackState> GetStateAsync(Guid graphId) => engine.GetPlaybackStateAsync(SoundId(graphId));

    private static string SoundId(Guid graphId) => $"graph-{graphId:N}";
}

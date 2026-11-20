using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Interfaces;
using Orbit.Application.Models;
using Orbit.Application.Services;
using Orbit.Audio.Compilation;
using System.Collections.Concurrent;

namespace Orbit.Audio.Playback;

/// <summary>Maps a committed graph to an engine request. The compiler and
/// engine are interchangeable dependencies; the UI knows neither Faust nor JS.</summary>
public sealed class FaustGraphPlaybackService(FaustGraphCompiler compiler, IAudioEngine engine)
    : IGraphPlaybackService
{
    // Leave headroom at the browser output in addition to graph-defined gain.
    private const double PlaybackVolume = 0.5;
    private readonly ConcurrentDictionary<Guid, ActiveGraphPlayback> active = new();

    public Task InitializeAsync() => engine.InitializeAsync();

    public async Task PlayAsync(GraphSnapshot graph)
    {
        var program = compiler.Compile(graph);
        var playback = new ActiveGraphPlayback(graph, program);
        if (!active.TryAdd(graph.Id, playback))
            throw new InvalidOperationException("Graph playback is already active.");
        try
        {
            await engine.PlayAsync(new PlaybackRequest(
                SoundId(graph.Id), "OrbitGraph", PlaybackVolume,
                new Dictionary<string, double>(playback.Values), Source: program.Source));
        }
        finally { active.TryRemove(graph.Id, out _); }
    }

    public async Task StopAsync(Guid graphId)
    {
        if (!active.TryGetValue(graphId, out var playback)) return;
        playback.Stopping = true;
        try { await engine.StopAsync(SoundId(graphId)); }
        catch { playback.Stopping = false; throw; }
    }

    public async Task<GraphPlaybackUpdate> UpdateAsync(GraphSnapshot graph)
    {
        if (!active.TryGetValue(graph.Id, out var playback))
            return new(graph.Revision, false, false);

        await playback.UpdateLock.WaitAsync();
        try
        {
            if (playback.Stopping || !active.TryGetValue(graph.Id, out var current) ||
                !ReferenceEquals(current, playback))
                return playback.Result with { IsActive = false };
            if (graph.Revision <= playback.ExaminedRevision) return playback.Result;

            if (!GraphTopology.Matches(playback.Topology, graph))
            {
                playback.ExaminedRevision = graph.Revision;
                return playback.Result = playback.Result with { RequiresRestart = true };
            }

            // Use the manifest captured at Play; do not compile source or touch
            // oscillator state when a validated base value changes.
            var nodes = graph.Nodes.ToDictionary(node => node.Id);
            var changes = new Dictionary<string, double>();
            foreach (var control in playback.Controls)
            {
                var parameter = nodes[control.Address.NodeId].Parameters.Single(p => p.Id == control.Address.ParameterId);
                if (!parameter.Definition.IsValidBaseValue(parameter.BaseValue))
                    throw new InvalidOperationException("Invalid base parameter value.");
                if (playback.Values[control.Name] != parameter.BaseValue)
                    changes.Add(control.Name, parameter.BaseValue);
            }
            if (changes.Count > 0 && !await engine.SetParametersAsync(SoundId(graph.Id), changes))
                return playback.Result with { IsActive = false };
            foreach (var change in changes) playback.Values[change.Key] = change.Value;
            playback.ExaminedRevision = graph.Revision;
            return playback.Result = new(graph.Revision, false, true);
        }
        finally { playback.UpdateLock.Release(); }
    }

    public Task<PlaybackState> GetStateAsync(Guid graphId) => engine.GetPlaybackStateAsync(SoundId(graphId));

    private static string SoundId(Guid graphId) => $"graph-{graphId:N}";
}

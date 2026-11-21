using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Interfaces;
using Orbit.Application.Models;
using Orbit.Application.Services;
using Orbit.Audio.Compilation;
using System.Collections.Concurrent;
using Orbit.Domain.Parameters;

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
            await engine.PlayAsync(CreateRequest(program));
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
            return new(graph.Revision, false);

        if (!playback.Request(graph.Revision)) return playback.Result;
        // Announce a newer revision immediately, even while the previous worker
        // request is pending. The browser checks this before starting a new voice.
        await engine.MarkRevisionAsync(SoundId(graph.Id), graph.Revision);

        await playback.UpdateLock.WaitAsync();
        try
        {
            if (playback.Stopping || !active.TryGetValue(graph.Id, out var current) ||
                !ReferenceEquals(current, playback))
                return playback.Result with { IsActive = false };
            if (graph.Revision != playback.RequestedRevision) return playback.Result;

            // Compiling a different topology can still produce identical DSP,
            // e.g. adding an unconnected node. Preserve the existing voice then.
            var program = GraphTopology.Matches(playback.Topology, graph)
                ? playback.Program : compiler.Compile(graph);
            var values = GetControlValues(graph, program);
            if (program.Source != playback.Program.Source)
            {
                var request = CreateRequest(program, values);
                if (!await engine.ReplaceAsync(request, graph.Revision))
                    return playback.Result with { IsActive = !playback.Stopping };
                return playback.Accept(graph, program, values);
            }

            // Use the manifest captured at Play; do not compile source or touch
            // oscillator state when a validated base value changes.
            var changes = values.Where(value => !playback.Values.TryGetValue(value.Key, out var previous) || previous != value.Value)
                .ToDictionary(value => value.Key, value => value.Value);
            if (changes.Count > 0 && !await engine.SetParametersAsync(SoundId(graph.Id), changes))
                return playback.Result with { IsActive = false };
            return playback.Accept(graph, program, values);
        }
        finally { playback.UpdateLock.Release(); }
    }

    public Task<PlaybackState> GetStateAsync(Guid graphId) => engine.GetPlaybackStateAsync(SoundId(graphId));

    public async Task<GraphParameterTelemetry?> GetTelemetryAsync(Guid graphId)
    {
        if (!active.TryGetValue(graphId, out var playback) || playback.Stopping ||
            playback.RequestedRevision != playback.Result.AppliedRevision) return null;
        var revision = playback.Result.AppliedRevision;
        var controls = playback.Controls;
        if (!controls.Any(control => control.MeterName is not null)) return null;
        var outputs = await engine.GetOutputValuesAsync(SoundId(graphId));
        // Do not attach values from an old or crossfading worklet to a new graph.
        if (outputs is null || playback.Stopping || !active.TryGetValue(graphId, out var current) ||
            !ReferenceEquals(current, playback) || playback.RequestedRevision != revision ||
            playback.Result.AppliedRevision != revision) return null;
        var addresses = controls.Where(control => control.MeterName is not null)
            .ToDictionary(control => control.MeterName!, StringComparer.Ordinal);
        var values = new Dictionary<Guid, Dictionary<ParameterAddress, double>>();
        foreach (var (path, value) in outputs)
        {
            var name = path[(path.LastIndexOf('/') + 1)..];
            if (double.IsFinite(value) && addresses.TryGetValue(name, out var control))
            {
                if (!values.TryGetValue(control.GraphId, out var local))
                    values.Add(control.GraphId, local = new Dictionary<ParameterAddress, double>());
                local[control.Address] = value;
            }
        }
        return values.Count == 0 ? null : new(graphId, revision,
            values.Remove(graphId, out var rootValues) ? rootValues : new Dictionary<ParameterAddress, double>())
        {
            ChildValues = values.ToDictionary(pair => pair.Key,
                pair => (IReadOnlyDictionary<ParameterAddress, double>)pair.Value)
        };
    }

    private static PlaybackRequest CreateRequest(FaustGraphProgram program, IReadOnlyDictionary<string, double>? values = null) =>
        new(SoundId(program.GraphId), "OrbitGraph", PlaybackVolume,
            values ?? program.InitialControlValues(),
            Source: program.Source, RequiresTransport: program.RequiresTransport);

    private static Dictionary<string, double> GetControlValues(GraphSnapshot graph, FaustGraphProgram program)
    {
        var values = program.Controls.ToDictionary(control => control.Name, control =>
        {
            var parameter = graph.GetGraph(control.GraphId).GetNode(control.Address.NodeId)
                .Parameters.Single(p => p.Id == control.Address.ParameterId);
            if (!parameter.Definition.IsValidBaseValue(parameter.BaseValue))
                throw new InvalidOperationException("Invalid base parameter value.");
            return parameter.BaseValue;
        });
        foreach (var control in program.MuteControls)
            values.Add(control.Name, graph.GetGraph(control.GraphId).GetNode(control.NodeId).IsMuted ? 0 : 1);
        return values;
    }

    private static string SoundId(Guid graphId) => $"graph-{graphId:N}";
}

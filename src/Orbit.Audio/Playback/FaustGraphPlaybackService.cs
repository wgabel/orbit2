using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Interfaces;
using Orbit.Application.Models;
using Orbit.Application.Services;
using Orbit.Audio.Compilation;
using System.Collections.Concurrent;
using Orbit.Domain.Parameters;
using Orbit.Domain.Nodes.Generators;

namespace Orbit.Audio.Playback;

/// <summary>Maps a committed graph to an engine request. The compiler and
/// engine are interchangeable dependencies; the UI knows neither Faust nor JS.</summary>
public sealed class FaustGraphPlaybackService(FaustGraphCompiler compiler, IAudioEngine engine)
    : IGraphPlaybackService, ISignalAnalysisService, IRhythmicActionService, IPerformanceNoteService
{
    // Leave headroom at the browser output in addition to graph-defined gain.
    private const double PlaybackVolume = 0.5;
    private readonly ConcurrentDictionary<Guid, ActiveGraphPlayback> active = new();
    private int TapCapacity => engine is ISignalCapture ? SignalTapManifest.MaxAuxiliaryChannels : 0;
    private ISignalCapture? Capture => engine as ISignalCapture;

    public Task InitializeAsync() => engine.InitializeAsync();

    public async Task PlayAsync(GraphSnapshot graph)
    {
        var program = compiler.Compile(graph, TapCapacity);
        var playback = new ActiveGraphPlayback(graph, program);
        if (!active.TryAdd(graph.Id, playback))
            throw new InvalidOperationException("Graph playback is already active.");
        try
        {
            await engine.PlayAsync(CreateRequest(program, playback.Generation));
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
                ? playback.Program : compiler.Compile(graph, TapCapacity);
            var values = GetControlValues(graph, program);
            if (program.Source != playback.Program.Source)
            {
                var generation = Guid.NewGuid();
                var request = CreateRequest(program, generation, values, playback.LatchedPitchActions);
                if (!await engine.ReplaceAsync(request, graph.Revision))
                    return playback.Result with { IsActive = !playback.Stopping };
                foreach (var target in playback.LatchedPitchActions.Keys.Where(key => !program.RhythmicInputs.Any(input =>
                    input.IsPitch && (input.GraphId, input.NodeId) == key)).ToArray()) playback.LatchedPitchActions.Remove(target);
                return playback.Accept(graph, program, values, generation);
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

    public Task<bool> SendAsync(Guid patchId, Guid graphId, Guid nodeId, RhythmicAction action)
    {
        if (engine is not IRhythmicActionSink sink || !active.TryGetValue(patchId, out var playback) || playback.Stopping ||
            playback.RequestedRevision != playback.Result.AppliedRevision || !Enum.IsDefined(action)) return Task.FromResult(false);
        var generation = playback.Generation;
        var inputs = playback.Program.RhythmicInputs;
        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];
            if (!input.IsPitch && input.GraphId == graphId && input.NodeId == nodeId && (action == RhythmicAction.Fire || input.AllowClose))
                return sink.SendRhythmicActionAsync(SoundId(patchId), generation, index, action);
        }
        return Task.FromResult(false);
    }

    public async Task<bool> SendAsync(Guid patchId, Guid graphId, Guid nodeId, PerformanceNoteAction action)
    {
        if (!action.IsValid || engine is not IPerformanceNoteSink sink || !active.TryGetValue(patchId, out var playback) ||
            !await playback.UpdateLock.WaitAsync(0)) return false;
        try
        {
            if (playback.Stopping || playback.RequestedRevision != playback.Result.AppliedRevision ||
                !active.TryGetValue(patchId, out var current) || !ReferenceEquals(current, playback)) return false;
            var inputs = playback.Program.RhythmicInputs;
            for (var index = 0; index < inputs.Count; index++)
            {
                var input = inputs[index];
                if (!input.IsPitch || input.GraphId != graphId || input.NodeId != nodeId) continue;
                var reference = (int)playback.Topology.GetGraph(graphId).GetNode(nodeId).Parameters
                    .Single(parameter => parameter.Definition.Key == NoteSequencerGenerator.ReferenceKey).BaseValue;
                if (action.Kind == PerformanceNoteKind.NoteOff) return true; // Continuous mode never releases pitch.
                if (action.Kind == PerformanceNoteKind.NoteOn && !NoteSequencerGenerator.IsPlayable(action.Note, reference)) return false;
                if (!await sink.SendPerformanceNoteAsync(SoundId(patchId), playback.Generation, index, action.Code)) return false;
                playback.LatchedPitchActions[(graphId, nodeId)] = action.Code;
                return true;
            }
            return false;
        }
        finally { playback.UpdateLock.Release(); }
    }

    public async Task<GraphParameterTelemetry?> GetTelemetryAsync(Guid graphId)
    {
        if (!active.TryGetValue(graphId, out var playback) || playback.Stopping ||
            playback.RequestedRevision != playback.Result.AppliedRevision) return null;
        var revision = playback.Result.AppliedRevision;
        var controls = playback.Controls;
        var envelopeMeters = playback.Program.EnvelopeMeters;
        var pitchMeters = playback.Program.PitchMeters;
        var generation = playback.Generation;
        if (!controls.Any(control => control.MeterName is not null) && envelopeMeters.Count == 0 && pitchMeters.Count == 0) return null;
        var outputs = await engine.GetOutputValuesAsync(SoundId(graphId));
        // Do not attach values from an old or crossfading worklet to a new graph.
        if (outputs is null || playback.Stopping || !active.TryGetValue(graphId, out var current) ||
            !ReferenceEquals(current, playback) || playback.RequestedRevision != revision ||
            playback.Result.AppliedRevision != revision || playback.Generation != generation) return null;
        var addresses = controls.Where(control => control.MeterName is not null)
            .ToDictionary(control => control.MeterName!, StringComparer.Ordinal);
        var values = new Dictionary<Guid, Dictionary<ParameterAddress, double>>();
        var envelopeAddresses = envelopeMeters.ToDictionary(meter => meter.Name, StringComparer.Ordinal);
        var playheads = new Dictionary<Guid, Dictionary<Guid, EnvelopePlayhead>>();
        var pitchAddresses = pitchMeters.ToDictionary(meter => meter.Name, StringComparer.Ordinal);
        var pitchOffsets = new Dictionary<Guid, Dictionary<Guid, double>>();
        foreach (var (path, value) in outputs)
        {
            var name = path[(path.LastIndexOf('/') + 1)..];
            if (double.IsFinite(value) && addresses.TryGetValue(name, out var control))
            {
                if (!values.TryGetValue(control.GraphId, out var local))
                    values.Add(control.GraphId, local = new Dictionary<ParameterAddress, double>());
                local[control.Address] = value;
            }
            else if (envelopeAddresses.TryGetValue(name, out var meter) && EnvelopePlayhead.Decode(value) is { } playhead)
            {
                if (!playheads.TryGetValue(meter.GraphId, out var local))
                    playheads.Add(meter.GraphId, local = new Dictionary<Guid, EnvelopePlayhead>());
                local[meter.NodeId] = playhead;
            }
            else if (pitchAddresses.TryGetValue(name, out var pitch) && double.IsFinite(value) && Math.Abs(value) <= NoteSequencerGenerator.MaximumOffset)
            {
                if (!pitchOffsets.TryGetValue(pitch.GraphId, out var local)) pitchOffsets.Add(pitch.GraphId, local = []);
                local[pitch.NodeId] = value;
            }
        }
        return values.Count == 0 && playheads.Count == 0 && pitchOffsets.Count == 0 ? null : new(graphId, revision,
            values.Remove(graphId, out var rootValues) ? rootValues : new Dictionary<ParameterAddress, double>())
        {
            ChildValues = values.ToDictionary(pair => pair.Key,
                pair => (IReadOnlyDictionary<ParameterAddress, double>)pair.Value),
            EnvelopePlayheads = playheads.ToDictionary(pair => pair.Key,
                pair => (IReadOnlyDictionary<Guid, EnvelopePlayhead>)pair.Value),
            PitchOffsets = pitchOffsets.ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<Guid, double>)pair.Value)
        };
    }

    public ISignalObservationSubscription Subscribe(SignalObservationAddress address) =>
        new SignalSubscription(this, address);

    public SignalObservationBinding Resolve(SignalObservationAddress address)
    {
        if (engine is not ISignalCapture ||
            !active.TryGetValue(address.PatchId, out var playback) || playback.Stopping)
            return new(SignalObservationAvailability.Inactive);
        return playback.Program.SignalTaps.Channels.TryGetValue(address, out var channel)
            ? new(SignalObservationAvailability.Available, SoundId(address.PatchId), playback.Generation, channel)
            : new(SignalObservationAvailability.Unavailable);
    }

    private async Task<SignalObservationResult> ReadSignalAsync(SignalObservationAddress address, Guid subscriptionId)
    {
        if (engine is not ISignalCapture capture ||
            !active.TryGetValue(address.PatchId, out var playback) || playback.Stopping)
            return new(SignalObservationAvailability.Inactive);
        var generation = playback.Generation;
        var program = playback.Program;
        if (!program.SignalTaps.Channels.TryGetValue(address, out var channel))
        {
            await capture.ReleaseSignalAsync(SoundId(address.PatchId), subscriptionId);
            return new(SignalObservationAvailability.Unavailable);
        }
        var frame = await capture.ReadSignalAsync(SoundId(address.PatchId), generation, subscriptionId, channel);
        if (frame is null || frame.Generation != generation || playback.Stopping ||
            !active.TryGetValue(address.PatchId, out var current) || !ReferenceEquals(current, playback) ||
            generation != playback.Generation || !ReferenceEquals(program, playback.Program))
            return new(SignalObservationAvailability.Pending);
        return new(SignalObservationAvailability.Available, frame);
    }

    private sealed class SignalSubscription(FaustGraphPlaybackService owner, SignalObservationAddress address)
        : ISignalObservationSubscription
    {
        private readonly Guid id = Guid.NewGuid();
        private readonly SemaphoreSlim gate = new(1);
        private bool disposed;

        public async Task<SignalObservationResult> ReadAsync()
        {
            await gate.WaitAsync();
            try
            {
                return disposed ? new(SignalObservationAvailability.Inactive)
                    : await owner.ReadSignalAsync(address, id);
            }
            finally { gate.Release(); }
        }

        public async ValueTask DisposeAsync()
        {
            await gate.WaitAsync();
            try
            {
                if (disposed) return;
                disposed = true;
                if (owner.Capture is ISignalCapture capture)
                    await capture.ReleaseSignalAsync(SoundId(address.PatchId), id);
            }
            finally { gate.Release(); }
        }
    }

    private static PlaybackRequest CreateRequest(FaustGraphProgram program, Guid generation, IReadOnlyDictionary<string, double>? values = null,
        IReadOnlyDictionary<(Guid GraphId, Guid NodeId), int>? pitchActions = null) =>
        new(SoundId(program.GraphId), "OrbitGraph", PlaybackVolume,
            values ?? program.InitialControlValues(),
            Source: program.Source, RequiresTransport: program.RequiresTransport,
            OutputChannels: program.SignalTaps.ChannelCount, Generation: generation,
            TransportInputChannels: program.TransportInputChannels, RhythmicInputs: program.RhythmicInputs.Select(input => input.IsPitch && pitchActions is not null &&
                pitchActions.TryGetValue((input.GraphId, input.NodeId), out var code) ? input with { InitialAction = code } : input).ToArray());

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
        foreach (var control in program.AuditionControls)
            values.Add(control.Name, FaustAuditionControl.Target(control.Kind, graph.GetGraph(control.GraphId).GetNode(control.NodeId)));
        return values;
    }

    private static string SoundId(Guid graphId) => $"graph-{graphId:N}";
}

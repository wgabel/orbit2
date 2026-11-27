using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Interfaces;
using Orbit.Application.Models;
using Orbit.Application.Services;
using Orbit.Audio.Compilation;
using System.Collections.Concurrent;
using System.Diagnostics;
using Orbit.Domain.Parameters;
using Orbit.Domain.Nodes.Generators;

namespace Orbit.Audio.Playback;

/// <summary>Maps a committed graph to an engine request. The compiler and
/// engine are interchangeable dependencies; the UI knows neither Faust nor JS.</summary>
public sealed class FaustGraphPlaybackService(FaustGraphCompiler compiler, IAudioEngine engine)
    : IGraphPlaybackService, ISignalAnalysisService, IRhythmicActionService, IPerformanceNoteService, IGraphPerformanceSource
{
    // Leave headroom at the browser output in addition to graph-defined gain.
    private const double PlaybackVolume = 0.5;
    private readonly ConcurrentDictionary<Guid, ActiveGraphPlayback> active = new();
    private GraphPerformanceTracker? lastDiagnostics;
    private bool collectDiagnostics;

    public void SetPerformanceCollection(bool enabled)
    {
        if (collectDiagnostics == enabled) return;
        collectDiagnostics = enabled;
        if (!enabled)
        {
            lastDiagnostics?.Stop();
            lastDiagnostics = null;
            foreach (var playback in active.Values)
            {
                playback.Diagnostics?.Stop();
                playback.Diagnostics = null;
            }
            return;
        }
        foreach (var (patchId, playback) in active)
        {
            var tracker = new GraphPerformanceTracker(patchId, playback.RequestedRevision);
            tracker.Prepare(playback.Program);
            if (playback.HasStarted) tracker.Playing(playback.Result.AppliedRevision);
            playback.Diagnostics = tracker;
            lastDiagnostics = tracker;
        }
    }

    public GraphPerformanceSnapshot? GetGraphPerformanceSnapshot(Guid patchId)
    {
        if (!collectDiagnostics) return null;
        if (active.TryGetValue(patchId, out var playback) && playback.Diagnostics is { } tracker)
        {
            // A replacement started while closed may finish in this session.
            // Expose its current program without inventing operation timings/counts.
            tracker.ObserveCurrent(playback.RequestedRevision, playback.Result.AppliedRevision, playback.Program);
            return tracker.Snapshot;
        }
        var previous = Volatile.Read(ref lastDiagnostics)?.Snapshot;
        return previous?.PatchId == patchId ? previous : null;
    }

    private FaustGraphProgram CompileMeasured(GraphSnapshot graph, GraphPerformanceTracker? diagnostics)
    {
        if (diagnostics?.Collecting != true)
            return compiler.Compile(graph, TapCapacity, Capture?.SupportsDynamicTaps == true, enableMacroModulation: true);
        var start = Stopwatch.GetTimestamp();
        try { return compiler.Compile(graph, TapCapacity, Capture?.SupportsDynamicTaps == true, enableMacroModulation: true); }
        finally { if (diagnostics.Collecting) diagnostics.Build(graph.Revision, Stopwatch.GetElapsedTime(start).TotalMilliseconds); }
    }

    private int TapCapacity => Capture?.SupportsDynamicTaps == true ? SignalTapManifest.MaxAuxiliaryChannels : 0;
    private ISignalCapture? Capture => engine as ISignalCapture;

    public Task InitializeAsync() => engine.InitializeAsync();

    public async Task PlayAsync(GraphSnapshot graph)
    {
        graph = graph.WithEffectiveMacroBaseValues();
        if (active.ContainsKey(graph.Id))
            throw new InvalidOperationException("Graph playback is already active.");
        var diagnostics = collectDiagnostics ? new GraphPerformanceTracker(graph.Id, graph.Revision) : null;
        if (diagnostics is not null) Volatile.Write(ref lastDiagnostics, diagnostics);
        FaustGraphProgram program;
        try { program = CompileMeasured(graph, diagnostics); }
        catch { diagnostics?.Failed(graph.Revision); throw; }
        diagnostics?.Prepare(program);
        var playback = new ActiveGraphPlayback(graph, program, diagnostics);
        if (!active.TryAdd(graph.Id, playback))
            throw new InvalidOperationException("Graph playback is already active.");
        try
        {
            await engine.PlayAsync(CreateRequest(program, playback.Generation));
        }
        catch { diagnostics?.Failed(graph.Revision); throw; }
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
        var diagnostics = playback.Diagnostics;
        diagnostics?.Request(graph.Revision);
        // Announce a newer revision immediately, even while the previous worker
        // request is pending. The browser checks this before starting a new voice.
        try { await engine.MarkRevisionAsync(SoundId(graph.Id), graph.Revision); }
        catch { diagnostics?.Failed(graph.Revision); throw; }

        await playback.UpdateLock.WaitAsync();
        try
        {
            if (playback.Stopping || !active.TryGetValue(graph.Id, out var current) ||
                !ReferenceEquals(current, playback))
            {
                diagnostics?.Skipped(graph.Revision);
                return playback.Result with { IsActive = false };
            }
            if (graph.Revision != playback.RequestedRevision)
            {
                diagnostics?.Skipped(graph.Revision);
                return playback.Result;
            }

            // Use the same resolved bases for controls, host patterns and note actions.
            graph = graph.WithEffectiveMacroBaseValues();

            // Compiling a different topology can still produce identical DSP,
            // e.g. adding an unconnected node. Preserve the existing voice then.
            var program = GraphTopology.Matches(playback.Topology, graph)
                ? playback.Program : CompileMeasured(graph, diagnostics);
            var values = GetControlValues(graph, program);
            if (program.Source != playback.Program.Source)
            {
                var generation = Guid.NewGuid();
                var request = CreateRequest(program, generation, values, playback.LatchedPitchActions);
                if (!await engine.ReplaceAsync(request, graph.Revision))
                {
                    diagnostics?.Skipped(graph.Revision);
                    return playback.Result with { IsActive = !playback.Stopping };
                }
                foreach (var target in playback.LatchedPitchActions.Keys.Where(key => !program.RhythmicInputs.Any(input =>
                    input.IsPitch && (input.GraphId, input.NodeId) == key)).ToArray()) playback.LatchedPitchActions.Remove(target);
                var accepted = playback.Accept(graph, program, values, generation);
                diagnostics?.Applied(graph.Revision, program, "replacement");
                return accepted;
            }

            // Use the manifest captured at Play; do not compile source or touch
            // oscillator state when a validated base value changes.
            var changes = values.Where(value => !playback.Values.TryGetValue(value.Key, out var previous) || previous != value.Value)
                .ToDictionary(value => value.Key, value => value.Value);
            var patterns = program.RhythmicInputs.Select((input, index) => (input, index)).Where(entry => entry.input.IsPitch)
                .Select(entry => (entry.index, Previous: PitchPattern.FromNode(playback.Topology.GetGraph(entry.input.GraphId).GetNode(entry.input.NodeId)),
                    Current: PitchPattern.FromNode(graph.GetGraph(entry.input.GraphId).GetNode(entry.input.NodeId))))
                .Where(entry => !entry.Current.Matches(entry.Previous)).Select(entry => new PitchPatternUpdate(entry.index, entry.Current)).ToArray();
            double? controlMs = null;
            if (changes.Count > 0 || patterns.Length > 0)
            {
                var started = diagnostics?.Collecting == true ? Stopwatch.GetTimestamp() : (long?)null;
                if (!await engine.SetGraphControlsAsync(SoundId(graph.Id), changes, patterns))
                {
                    diagnostics?.Skipped(graph.Revision);
                    return playback.Result with { IsActive = false };
                }
                if (started is long timestamp && diagnostics?.Collecting == true)
                    controlMs = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
            }
            var reused = playback.Accept(graph, program, values);
            diagnostics?.Applied(graph.Revision, program, "reused", controlMs);
            return reused;
        }
        catch { diagnostics?.Failed(graph.Revision); throw; }
        finally { playback.UpdateLock.Release(); }
    }

    public async Task<PlaybackState> GetStateAsync(Guid graphId)
    {
        active.TryGetValue(graphId, out var playback);
        var state = await engine.GetPlaybackStateAsync(SoundId(graphId));
        if (state == PlaybackState.Playing && playback is not null &&
            active.TryGetValue(graphId, out var current) && ReferenceEquals(current, playback))
        {
            playback.HasStarted = true;
            playback.Diagnostics?.Playing(playback.Result.AppliedRevision);
        }
        return state;
    }

    public Task<bool> SendAsync(Guid patchId, Guid graphId, Guid nodeId, RhythmicAction action)
    {
        if (engine is not IRhythmicActionSink sink || !active.TryGetValue(patchId, out var playback) || playback.Stopping ||
            playback.RequestedRevision != playback.Result.AppliedRevision || !Enum.IsDefined(action)) return Task.FromResult(false);
        var generation = playback.Generation;
        var inputs = playback.Program.RhythmicInputs;
        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];
            if (!input.IsPitch && !input.IsNote && input.GraphId == graphId && input.NodeId == nodeId && (action == RhythmicAction.Fire || input.AllowClose))
                return sink.SendRhythmicActionAsync(SoundId(patchId), generation, index, action);
        }
        return Task.FromResult(false);
    }

    public async Task<bool> SendAsync(Guid patchId, Guid graphId, Guid nodeId, PerformanceNoteAction action)
    {
        if (!action.IsValid || engine is not IPerformanceNoteSink sink || !active.TryGetValue(patchId, out var playback)) return false;
        // Releases must not be dropped merely because a live parameter update is in flight.
        if (action.Kind == PerformanceNoteKind.NoteOn)
        { if (!await playback.UpdateLock.WaitAsync(0)) return false; }
        else await playback.UpdateLock.WaitAsync();
        try
        {
            if (playback.Stopping || (action.Kind == PerformanceNoteKind.NoteOn && playback.RequestedRevision != playback.Result.AppliedRevision) ||
                !active.TryGetValue(patchId, out var current) || !ReferenceEquals(current, playback)) return false;
            var inputs = playback.Program.RhythmicInputs;
            for (var index = 0; index < inputs.Count; index++)
            {
                var input = inputs[index];
                if ((!input.IsPitch && !input.IsNote) || input.GraphId != graphId || input.NodeId != nodeId) continue;
                var reference = (int)playback.Topology.GetGraph(graphId).GetNode(nodeId).Parameters
                    .Single(parameter => parameter.Definition.Key == NoteSequencerGenerator.ReferenceKey).BaseValue;
                if (input.IsNote)
                {
                    if (action.Kind == PerformanceNoteKind.NoteOn && !NoteSequencerGenerator.IsPlayable(action.Note, reference)) return false;
                    return await sink.SendPerformanceNoteAsync(SoundId(patchId), playback.Generation, index, action.NoteCode);
                }
                if (action.Kind == PerformanceNoteKind.NoteOff)
                {
                    var articulated = playback.Topology.GetGraph(graphId).GetNode(nodeId).Parameters
                        .Any(parameter => parameter.Definition.Key == NoteSequencerGenerator.ArticulationKey && parameter.BaseValue == 1);
                    return !articulated || await sink.SendPerformanceNoteAsync(SoundId(patchId), playback.Generation, index, 130);
                }
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
        var macroControls = playback.Program.MacroControls;
        var macroTargets = playback.Program.MacroTargets;
        var generation = playback.Generation;
        if (!controls.Any(control => control.MeterName is not null) && envelopeMeters.Count == 0 &&
            pitchMeters.Count == 0 && macroControls.Count == 0 && macroTargets.Count == 0) return null;
        var outputs = await engine.GetOutputValuesAsync(SoundId(graphId));
        // Do not attach values from an old or crossfading worklet to a new graph.
        if (outputs is null || playback.Stopping || !active.TryGetValue(graphId, out var current) ||
            !ReferenceEquals(current, playback) || playback.RequestedRevision != revision ||
            playback.Result.AppliedRevision != revision || playback.Generation != generation) return null;
        var addresses = controls.Where(control => control.MeterName is not null)
            .ToDictionary(control => control.MeterName!, StringComparer.Ordinal);
        var macroAddresses = macroControls.ToDictionary(control => control.MeterName, control => control.Address, StringComparer.Ordinal);
        var targetAddresses = macroTargets.ToDictionary(target => target.MeterName, StringComparer.Ordinal);
        var macroValues = new Dictionary<GraphMacroAddress, double>();
        var values = new Dictionary<Guid, Dictionary<ParameterAddress, double>>();
        var envelopeAddresses = envelopeMeters.ToDictionary(meter => meter.Name, StringComparer.Ordinal);
        var playheads = new Dictionary<Guid, Dictionary<Guid, EnvelopePlayhead>>();
        var pitchAddresses = pitchMeters.ToDictionary(meter => meter.Name, StringComparer.Ordinal);
        var stepAddresses = pitchMeters.ToDictionary(meter => meter.StepName, StringComparer.Ordinal);
        var pitchOffsets = new Dictionary<Guid, Dictionary<Guid, double>>();
        var pitchSteps = new Dictionary<Guid, Dictionary<Guid, int>>();
        foreach (var (path, value) in outputs)
        {
            var name = path[(path.LastIndexOf('/') + 1)..];
            if (double.IsFinite(value) && macroAddresses.TryGetValue(name, out var macro))
            {
                if (value is >= 0 and <= 1)
                    macroValues[new GraphMacroAddress(macro.GraphId, macro.CompositeId, macro.MacroId)] = value;
            }
            else if (double.IsFinite(value) && targetAddresses.TryGetValue(name, out var target))
            {
                if (!values.TryGetValue(target.GraphId, out var local))
                    values.Add(target.GraphId, local = new Dictionary<ParameterAddress, double>());
                local[target.Address] = value;
            }
            else if (double.IsFinite(value) && addresses.TryGetValue(name, out var control))
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
            else if (stepAddresses.TryGetValue(name, out var step) && value is >= 0 and <= 64 && value == Math.Truncate(value))
            {
                if (!pitchSteps.TryGetValue(step.GraphId, out var local)) pitchSteps.Add(step.GraphId, local = []);
                local[step.NodeId] = (int)value;
            }
        }
        return values.Count == 0 && playheads.Count == 0 && pitchOffsets.Count == 0 && macroValues.Count == 0 ? null : new(graphId, revision,
            values.Remove(graphId, out var rootValues) ? rootValues : new Dictionary<ParameterAddress, double>())
        {
            ChildValues = values.ToDictionary(pair => pair.Key,
                pair => (IReadOnlyDictionary<ParameterAddress, double>)pair.Value),
            EnvelopePlayheads = playheads.ToDictionary(pair => pair.Key,
                pair => (IReadOnlyDictionary<Guid, EnvelopePlayhead>)pair.Value),
            PitchOffsets = pitchOffsets.ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<Guid, double>)pair.Value),
            PitchSteps = pitchSteps.ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<Guid, int>)pair.Value),
            MacroValues = macroValues
        };
    }

    public ISignalObservationSubscription Subscribe(SignalObservationAddress address) =>
        new SignalSubscription(this, address);

    public SignalObservationBinding Resolve(SignalObservationAddress address)
    {
        if (!active.TryGetValue(address.PatchId, out var playback) || playback.Stopping)
            return new(SignalObservationAvailability.Inactive);
        if (Capture?.SupportsDynamicTaps != true) return new(SignalObservationAvailability.Unsupported);
        var accepted = playback.Accepted;
        return accepted.Program.SignalTaps.ObservationIds.TryGetValue(address, out var channel)
            ? new(SignalObservationAvailability.Available, SoundId(address.PatchId), accepted.Generation, channel)
            : new(SignalObservationAvailability.Unavailable);
    }

    private async Task<SignalObservationResult> ReadSignalAsync(SignalObservationAddress address, Guid subscriptionId)
    {
        if (!active.TryGetValue(address.PatchId, out var playback) || playback.Stopping)
            return new(SignalObservationAvailability.Inactive);
        if (Capture is not { SupportsDynamicTaps: true } capture) return new(SignalObservationAvailability.Unsupported);
        var accepted = playback.Accepted;
        var generation = accepted.Generation;
        var program = accepted.Program;
        if (!program.SignalTaps.ObservationIds.TryGetValue(address, out var channel))
        {
            await capture.ReleaseSignalAsync(SoundId(address.PatchId), subscriptionId);
            return new(SignalObservationAvailability.Unavailable);
        }
        var result = await capture.ReadObservationAsync(SoundId(address.PatchId), generation, subscriptionId, channel);
        if ((result.Frame is not null && result.Frame.Generation != generation) || playback.Stopping ||
            !active.TryGetValue(address.PatchId, out var current) || !ReferenceEquals(current, playback) ||
            generation != playback.Generation || !ReferenceEquals(program, playback.Program))
            return new(SignalObservationAvailability.Pending);
        return result;
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
            OutputChannels: program.SignalTaps.ChannelCount, Generation: generation, TapSelectorCount: program.SignalTaps.SelectorCount,
            GraphRevision: program.Revision, TransportInputChannels: program.TransportInputChannels, RhythmicInputs: program.RhythmicInputs.Select(input => input.IsPitch && pitchActions is not null &&
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
        foreach (var control in program.MacroControls)
        {
            var definition = graph.GetGraph(control.Address.GraphId).GetNode(control.Address.CompositeId)
                .Composite?.Macros.SingleOrDefault(macro => macro.Id == control.Address.MacroId)
                ?? throw new InvalidOperationException("The accepted macro control no longer exists in the graph.");
            if (!double.IsFinite(definition.BaseValue) || definition.BaseValue is < 0 or > 1)
                throw new InvalidOperationException("Invalid macro base parameter value.");
            values.Add(control.Name, definition.BaseValue);
        }
        foreach (var control in program.AuditionControls)
            values.Add(control.Name, FaustAuditionControl.Target(control.Kind, graph.GetGraph(control.GraphId).GetNode(control.NodeId)));
        return values;
    }

    private static string SoundId(Guid graphId) => $"graph-{graphId:N}";
}

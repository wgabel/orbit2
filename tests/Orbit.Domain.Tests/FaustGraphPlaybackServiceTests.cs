using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Interfaces;
using Orbit.Application.Models;
using Orbit.Application.Presets;
using Orbit.Audio.Compilation;
using Orbit.Audio.Playback;
using Orbit.Domain.Parameters;
using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Ports;
using Orbit.Infrastructure.Presets;
using Xunit;

namespace Orbit.Domain.Tests;

/// <summary>The playback use case translates the exact immutable snapshot to engine input.</summary>
public sealed class FaustGraphPlaybackServiceTests
{
    [Fact]
    public async Task SignalSubscriptionsFollowAcceptedGenerationsWithoutRecompiling()
    {
        using var patch = new NestedFaustPatch();
        var engine = new CapturingEngine();
        var service = new FaustGraphPlaybackService(FaustGraphCompiler.CreateBuiltIn(), engine);
        var graph = patch.Session.Snapshot;
        var oscillator = graph.GetGraph(patch.ChildId).GetNode(patch.Frequency.NodeId);
        var address = new SignalObservationAddress(graph.Id, patch.ChildId, oscillator.Id,
            oscillator.Outputs[0].Id, PortDirection.Output);
        await using var subscription = service.Subscribe(address);
        Assert.Equal(SignalObservationAvailability.Inactive, service.Resolve(address).Availability);
        Assert.Equal(SignalObservationAvailability.Inactive, (await subscription.ReadAsync()).Availability);
        var playing = service.PlayAsync(graph);
        var initial = Assert.IsType<PlaybackRequest>(engine.Request);
        Assert.InRange(initial.OutputChannels, 2, 9);
        var binding = service.Resolve(address);
        Assert.Equal(SignalObservationAvailability.Available, binding.Availability);
        Assert.Equal(initial.Generation, binding.Generation);
        Assert.Equal(initial.SoundId, binding.SoundId);
        Assert.Equal(SignalObservationAvailability.Unavailable, service.Resolve(address with { PortId = Guid.NewGuid() }).Availability);
        Assert.Equal(initial.Generation, (await subscription.ReadAsync()).Frame!.Generation);
        patch.Session.Execute(new SetParameterValueCommand(patch.ChildId, patch.Frequency, 330));
        await service.UpdateAsync(patch.Session.Snapshot);
        Assert.Equal(initial.Generation, (await subscription.ReadAsync()).Frame!.Generation);
        Assert.Empty(engine.Replacements);
        Assert.Equal(binding, service.Resolve(address));
        var waveform = oscillator.Parameters.Single(p => p.Definition.Key == "waveform");
        patch.Session.Execute(new SetParameterValueCommand(patch.ChildId,
            new ParameterAddress(oscillator.Id, waveform.Id), (int)OscillatorWaveform.Square));
        engine.AcceptReplacement = false;
        await service.UpdateAsync(patch.Session.Snapshot);
        Assert.Equal(binding, service.Resolve(address));
        Assert.Equal(initial.Generation, (await subscription.ReadAsync()).Frame!.Generation);
        engine.AcceptReplacement = true;
        patch.Session.Execute(new SetParameterValueCommand(patch.ChildId,
            new ParameterAddress(oscillator.Id, waveform.Id), (int)OscillatorWaveform.Triangle));
        await service.UpdateAsync(patch.Session.Snapshot);
        Assert.NotEqual(initial.Generation, (await subscription.ReadAsync()).Frame!.Generation);
        Assert.NotEqual(binding.Generation, service.Resolve(address).Generation);
        await subscription.DisposeAsync();
        Assert.Single(engine.Released);
        await service.StopAsync(graph.Id);
        await playing;
        Assert.Equal(SignalObservationAvailability.Inactive, service.Resolve(address).Availability);
        Assert.Equal(SignalObservationAvailability.Inactive, (await subscription.ReadAsync()).Availability);
    }

    [Fact]
    public async Task MultiParameterPresetUpdatesOneVoiceOrReplacesOnceForStructuralChanges()
    {
        using var patch = new NestedFaustPatch();
        var session = patch.Session;
        var engine = new RecordingEngine();
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var service = new FaustGraphPlaybackService(compiler, engine);
        var original = session.Snapshot;
        var nodeId = patch.Frequency.NodeId;
        var catalog = new NodePresetService(new EmbeddedNodePresetCatalog(), new NodePresetValidator(BuiltInNodes.CreateRegistry()));
        var playing = service.PlayAsync(original);
        var basePreset = catalog.GetPreset("oscillator-deep-sine");
        session.Execute(new ApplyNodePresetCommand(patch.ChildId, nodeId, basePreset));
        var applied = session.Snapshot;
        Assert.Equal(new GraphPlaybackUpdate(applied.Revision, true), await service.UpdateAsync(applied));
        var updates = Assert.Single(engine.Updates);
        var nodeControls = compiler.Compile(applied).Controls.Where(control => control.GraphId == patch.ChildId && control.Address.NodeId == nodeId).ToArray();
        var frequency = nodeControls.Single(control => control.Address == patch.Frequency);
        Assert.Equal(110, updates[frequency.Name]);
        Assert.Equal(2, updates.Count); // Frequency and amplitude; shape and timing remain unchanged.
        Assert.Contains(0.2, updates.Values);
        Assert.Empty(engine.Replacements);

        var structuralPreset = catalog.GetPreset("oscillator-quarter-pulse");
        session.Execute(new ApplyNodePresetCommand(patch.ChildId, nodeId, structuralPreset));
        var structural = session.Snapshot;
        Assert.Equal(new GraphPlaybackUpdate(structural.Revision, true), await service.UpdateAsync(structural));
        var replacement = Assert.Single(engine.Replacements);
        var expected = compiler.Compile(structural);
        Assert.Equal(expected.Source, replacement.Source);
        Assert.NotEqual(engine.Request!.Source, replacement.Source);
        Assert.Single(engine.Updates);
        foreach (var control in expected.Controls)
            Assert.Equal(control.InitialValue, replacement.Parameters[control.Name]);
        Assert.Equal(1, engine.PlayCount);
        await service.StopAsync(session.GraphId);
        await playing;
    }

    [Fact]
    public async Task WaveformChoiceReplacesOnlyTheProgramAndKeepsLiveControls()
    {
        using var patch = new NestedFaustPatch();
        var engine = new RecordingEngine();
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var service = new FaustGraphPlaybackService(compiler, engine);
        var original = patch.Session.Snapshot;
        var playing = service.PlayAsync(original);
        var oscillator = original.GetGraph(patch.ChildId).GetNode(patch.Frequency.NodeId);
        var waveform = oscillator.Parameters.Single(parameter => parameter.Definition.Key == "waveform");
        var address = new ParameterAddress(oscillator.Id, waveform.Id);
        patch.Session.Execute(new SetParameterValueCommand(patch.ChildId, address, (int)OscillatorWaveform.Square));
        Assert.Equal(new GraphPlaybackUpdate(patch.Session.Snapshot.Revision, true),
            await service.UpdateAsync(patch.Session.Snapshot));
        var replacement = Assert.Single(engine.Replacements);
        Assert.NotEqual(engine.Request!.Source, replacement.Source);
        Assert.Contains("float(p < 0.5)", replacement.Source);
        Assert.Empty(engine.Updates);
        Assert.Equal(engine.Request.Parameters.Keys.Order(), replacement.Parameters.Keys.Order());
        Assert.Equal(original.GetGraph(patch.ChildId).Connections.ToArray(),
            patch.Session.Snapshot.GetGraph(patch.ChildId).Connections.ToArray());
        patch.Session.Execute(new SetParameterValueCommand(patch.ChildId, patch.Frequency, 330));
        await service.UpdateAsync(patch.Session.Snapshot);
        Assert.Single(engine.Replacements);
        Assert.Single(engine.Updates);
        await service.StopAsync(patch.Session.GraphId);
        await playing;
    }

    [Fact]
    public async Task NestedControlEditsAndMetersUseTheOwningCanvasWithoutReplacingVoice()
    {
        using var patch = new NestedFaustPatch();
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var engine = new RecordingEngine();
        var service = new FaustGraphPlaybackService(compiler, engine);
        var rootId = patch.Session.GraphId;
        var playing = service.PlayAsync(patch.Session.Snapshot);
        var control = compiler.Compile(patch.Session.Snapshot).Controls.Single(c =>
            c.GraphId == patch.ChildId && c.Address == patch.Frequency);
        patch.Session.Execute(new SetParameterValueCommand(patch.ChildId, patch.Frequency, 330));
        var revision = patch.Session.Snapshot.Revision;
        Assert.Equal(new GraphPlaybackUpdate(revision, true), await service.UpdateAsync(patch.Session.Snapshot));
        Assert.Equal(330, Assert.Single(engine.Updates)[control.Name]);
        Assert.Empty(engine.Replacements);
        engine.Outputs = new() { ["/OrbitGraph/" + control.MeterName] = 350 };
        var frame = await service.GetTelemetryAsync(rootId);
        Assert.NotNull(frame);
        Assert.Empty(frame.Values);
        Assert.Equal(350, frame.GetValues(patch.ChildId)[patch.Frequency]);
        Assert.Equal(revision, frame.Revision);
        // Removing an internal route changes the DSP, not the playback identity.
        var child = patch.Session.Snapshot.GetGraph(patch.ChildId);
        var edge = child.Connections.Single(e => e.Destination.NodeId == patch.Frequency.NodeId);
        patch.Session.Execute(new RemoveConnectionCommand(patch.ChildId, edge.Id));
        await service.UpdateAsync(patch.Session.Snapshot);
        Assert.Single(engine.Replacements);
        await service.StopAsync(rootId);
        await playing;
    }

    [Fact]
    public async Task PassesGeneratedSourceAndHostValuesToTheEngine()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings(220, 1, 50));
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var engine = new RecordingEngine();
        var service = new FaustGraphPlaybackService(compiler, engine);
        await service.InitializeAsync();
        var playing = service.PlayAsync(session.Snapshot);
        var expected = compiler.Compile(session.Snapshot);
        Assert.True(engine.Initialized);
        Assert.NotNull(engine.Request);
        Assert.Equal(expected.Source, engine.Request.Source);
        Assert.Equal("OrbitGraph", engine.Request.Processor);
        Assert.Equal(expected.Controls.Count + expected.AuditionControls.Count, engine.Request.Parameters.Count);
        foreach (var control in expected.Controls)
            Assert.Equal(control.InitialValue, engine.Request.Parameters[control.Name]);
        foreach (var control in expected.AuditionControls)
            Assert.Equal(control.InitialValue, engine.Request.Parameters[control.Name]);
        Assert.Equal(PlaybackState.Playing, await service.GetStateAsync(session.GraphId));
        await service.StopAsync(session.GraphId);
        await playing;
        Assert.Equal(engine.Request.SoundId, engine.StoppedSound);
        Assert.Equal(PlaybackState.Idle, await service.GetStateAsync(session.GraphId));
    }

    [Fact]
    public async Task CompositeMuteUsesExistingVoiceAndRestoresWithUndo()
    {
        using var patch = new NestedFaustPatch();
        var engine = new RecordingEngine();
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var service = new FaustGraphPlaybackService(compiler, engine);
        var root = patch.Session.Snapshot;
        var composite = root.Nodes.Single(node => node.Composite is not null);
        var control = compiler.Compile(root).AuditionControls.Single(control => control.NodeId == composite.Id && control.Kind == NodeAuditionKind.Mute);
        patch.Session.Execute(new SetNodeMutedCommand(root.Id, composite.Id, true));
        var playing = service.PlayAsync(patch.Session.Snapshot);
        Assert.Equal(0, engine.Request!.Parameters[control.Name]);
        patch.Session.Undo();
        await service.UpdateAsync(patch.Session.Snapshot);
        Assert.Equal(1, Assert.Single(engine.Updates)[control.Name]);
        Assert.Empty(engine.Replacements);
        patch.Session.Redo();
        await service.UpdateAsync(patch.Session.Snapshot);
        Assert.Equal(0, engine.Updates.Last()[control.Name]);
        Assert.Empty(engine.Replacements);
        await service.StopAsync(root.Id);
        await playing;
    }

    [Fact]
    public async Task BypassAndMuteUpdateIndependentControlsWithoutReplacingVoice()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings());
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var engine = new RecordingEngine();
        var service = new FaustGraphPlaybackService(compiler, engine);
        var graph = session.Snapshot;
        var gain = graph.Nodes.Single(node => node.TypeKey == GainProcessor.NodeType);
        var controls = compiler.Compile(graph).AuditionControls.Where(control => control.NodeId == gain.Id).ToArray();
        var bypass = controls.Single(control => control.Kind == NodeAuditionKind.Bypass);
        var mute = controls.Single(control => control.Kind == NodeAuditionKind.Mute);
        session.Execute(new SetNodeBypassedCommand(graph.Id, gain.Id, true));
        var playing = service.PlayAsync(session.Snapshot);
        Assert.Equal(1, engine.Request!.Parameters[bypass.Name]);
        session.Execute(new SetNodeMutedCommand(graph.Id, gain.Id, true));
        await service.UpdateAsync(session.Snapshot);
        Assert.Equal(0, Assert.Single(engine.Updates)[mute.Name]);
        session.Execute(new SetNodeMutedCommand(graph.Id, gain.Id, false));
        await service.UpdateAsync(session.Snapshot);
        Assert.Equal(1, Assert.Single(engine.Updates.Last()).Value);
        Assert.True(session.Snapshot.GetNode(gain.Id).IsBypassed);
        session.Execute(new SetNodeBypassedCommand(graph.Id, gain.Id, false));
        await service.UpdateAsync(session.Snapshot);
        Assert.Equal(0, Assert.Single(engine.Updates.Last()).Value);
        Assert.Equal(bypass.Name, Assert.Single(engine.Updates.Last()).Key);
        Assert.Empty(engine.Replacements);
        await service.StopAsync(graph.Id);
        await playing;
    }

    [Fact]
    public async Task BaseEditsUpdateOnlyChangedControlsAndNeverRecreateVoice()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings(220, 1, 50));
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var engine = new RecordingEngine();
        var service = new FaustGraphPlaybackService(compiler, engine);
        var playing = service.PlayAsync(session.Snapshot);
        var original = session.Snapshot;
        var oscillator = original.Nodes[1];
        var address = new ParameterAddress(oscillator.Id, oscillator.Parameters.Single(p => p.Definition.Key == "frequency").Id);
        var name = compiler.Compile(original).Controls.Single(control => control.Address == address).Name;
        session.Execute(new SetParameterValueCommand(session.GraphId, address, 330));
        var edited = session.Snapshot;
        var result = await service.UpdateAsync(edited);
        Assert.Equal(new GraphPlaybackUpdate(edited.Revision, true), result);
        Assert.Equal(330, Assert.Single(Assert.Single(engine.Updates)).Value);
        Assert.Equal(name, Assert.Single(Assert.Single(engine.Updates)).Key);
        Assert.Equal(1, engine.PlayCount);
        // Older callbacks cannot roll the worklet controls backwards.
        Assert.Equal(result, await service.UpdateAsync(original));
        Assert.Single(engine.Updates);
        await service.StopAsync(session.GraphId);
        await playing;
        Assert.False((await service.UpdateAsync(edited)).IsActive);
    }

    [Fact]
    public async Task RewiringChangesTheProgramAndThenLiveControlsUseTheNewVoice()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings(220, 1, 50));
        var engine = new RecordingEngine();
        var service = new FaustGraphPlaybackService(FaustGraphCompiler.CreateBuiltIn(), engine);
        var original = session.Snapshot;
        var playing = service.PlayAsync(original);
        var lfo = original.Nodes[0];
        var carrier = original.Nodes[1];
        var frequency = carrier.Parameters.Single(p => p.Definition.Key == "frequency");
        var amplitude = carrier.Parameters.Single(p => p.Definition.Key == "amplitude");
        var connection = original.Connections.Single(c => c.Destination == new PortAddress(carrier.Id, frequency.ModulationPortId!.Value));
        session.Execute(new RemoveConnectionCommand(session.GraphId, connection.Id));
        session.Execute(new ConnectNodesCommand(session.GraphId, connection.Source,
            new PortAddress(carrier.Id, amplitude.ModulationPortId!.Value), 0.5));
        var structural = session.Snapshot;
        var result = await service.UpdateAsync(structural);
        Assert.Equal(new GraphPlaybackUpdate(structural.Revision, true), result);
        Assert.NotEqual(engine.Request!.Source, Assert.Single(engine.Replacements).Source);
        Assert.Equal(1, engine.PlayCount);
        var address = new ParameterAddress(carrier.Id, frequency.Id);
        session.Execute(new SetParameterValueCommand(session.GraphId, address, 330));
        Assert.Equal(new GraphPlaybackUpdate(session.Snapshot.Revision, true), await service.UpdateAsync(session.Snapshot));
        Assert.Single(engine.Replacements);
        Assert.Single(engine.Updates);
        await service.StopAsync(session.GraphId);
        await playing;
    }

    [Fact]
    public async Task TelemetryMapsPassiveMetersToParameterAddressesAndCurrentRevision()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings());
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var engine = new RecordingEngine();
        var service = new FaustGraphPlaybackService(compiler, engine);
        Assert.Null(await service.GetTelemetryAsync(session.GraphId));
        var playing = service.PlayAsync(session.Snapshot);
        var meter = compiler.Compile(session.Snapshot).Controls.Single(control => control.MeterName is not null);
        engine.Outputs = new() { [$"/OrbitGraph/{meter.MeterName}"] = 465.5, ["other"] = 99 };
        var frame = await service.GetTelemetryAsync(session.GraphId);
        Assert.NotNull(frame);
        Assert.Equal(session.Snapshot.Revision, frame.Revision);
        Assert.Equal(465.5, Assert.Single(frame.Values).Value);
        Assert.Equal(meter.Address, Assert.Single(frame.Values).Key);
        session.Execute(new AddNodeCommand(session.GraphId, session.Snapshot.Nodes[^1].TypeKey));
        await Assert.ThrowsAsync<FaustCompilationException>(() => service.UpdateAsync(session.Snapshot));
        Assert.Null(await service.GetTelemetryAsync(session.GraphId));
        await service.StopAsync(session.GraphId);
        await playing;
        Assert.Null(await service.GetTelemetryAsync(session.GraphId));
    }

    [Fact]
    public async Task DisconnectedNodeDoesNotReplaceIdenticalProgram()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings());
        var engine = new RecordingEngine();
        var service = new FaustGraphPlaybackService(FaustGraphCompiler.CreateBuiltIn(), engine);
        var playing = service.PlayAsync(session.Snapshot);
        session.Execute(new AddNodeCommand(session.GraphId, session.Snapshot.Nodes[0].TypeKey));
        Assert.Equal(new GraphPlaybackUpdate(session.Snapshot.Revision, true), await service.UpdateAsync(session.Snapshot));
        Assert.Empty(engine.Replacements);
        await service.StopAsync(session.GraphId);
        await playing;
    }

    [Fact]
    public async Task InvalidTopologyKeepsLastGoodProgramAndCanRecover()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings());
        var engine = new RecordingEngine();
        var service = new FaustGraphPlaybackService(FaustGraphCompiler.CreateBuiltIn(), engine);
        var original = session.Snapshot;
        var playing = service.PlayAsync(original);
        session.Execute(new AddNodeCommand(session.GraphId, original.Nodes[^1].TypeKey));
        await Assert.ThrowsAsync<FaustCompilationException>(() => service.UpdateAsync(session.Snapshot));
        Assert.Empty(engine.Replacements);
        var extra = session.Snapshot.Nodes[^1];
        session.Execute(new RemoveNodeCommand(session.GraphId, extra.Id));
        Assert.Equal(new GraphPlaybackUpdate(session.Snapshot.Revision, true), await service.UpdateAsync(session.Snapshot));
        Assert.Empty(engine.Replacements);
        await service.StopAsync(session.GraphId);
        await playing;
    }

    private class RecordingEngine : IAudioEngine
    {
        public bool Initialized { get; private set; }
        public PlaybackRequest? Request { get; private set; }
        public string? StoppedSound { get; private set; }
        public int PlayCount { get; private set; }
        public List<IReadOnlyDictionary<string, double>> Updates { get; } = [];
        public List<PlaybackRequest> Replacements { get; } = [];
        public Dictionary<string, double>? Outputs { get; set; }
        public bool AcceptReplacement { get; set; } = true;
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task InitializeAsync() { Initialized = true; return Task.CompletedTask; }
        public Task PlayAsync(PlaybackRequest request) { Request = request; PlayCount++; return completion.Task; }
        public Task StopAsync(string soundId) { StoppedSound = soundId; completion.TrySetResult(); return Task.CompletedTask; }
        public Task<bool> SetParametersAsync(string soundId, IReadOnlyDictionary<string, double> parameters)
        {
            Assert.Equal(Request?.SoundId, soundId);
            Updates.Add(parameters);
            return Task.FromResult(true);
        }
        public Task MarkRevisionAsync(string soundId, long revision) => Task.CompletedTask;
        public Task<bool> ReplaceAsync(PlaybackRequest request, long revision)
        {
            Replacements.Add(request);
            return Task.FromResult(AcceptReplacement);
        }
        public Task<PlaybackState> GetPlaybackStateAsync(string soundId) =>
            Task.FromResult(Request?.SoundId == soundId && StoppedSound != soundId ? PlaybackState.Playing : PlaybackState.Idle);
        public Task<Dictionary<string, double>?> GetOutputValuesAsync(string soundId) =>
            Task.FromResult(Outputs);
    }

    private sealed class CapturingEngine : RecordingEngine, ISignalCapture
    {
        public List<Guid> Released { get; } = [];
        public Task<SignalObservationFrame?> ReadSignalAsync(string soundId, Guid generation, Guid subscriptionId, int channel) =>
            Task.FromResult<SignalObservationFrame?>(new(generation, 48000, 1, [0.1f]));
        public Task ReleaseSignalAsync(string soundId, Guid subscriptionId)
        {
            Released.Add(subscriptionId);
            return Task.CompletedTask;
        }
    }
}

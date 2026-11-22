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
    public async Task MonophonicNotesAreTransientAndNeverReseedHeldGateOnReplacement()
    {
        using var patch = new NestedFaustPatch();
        var session = patch.Session;
        var child = patch.ChildId;
        var carrier = session.Snapshot.GetGraph(child).GetNode(patch.Frequency.NodeId);
        var source = NestedFaustPatch.Add(session, child, NoteSourceGenerator.NodeType);
        Assert.Equal(new[] { SignalType.Modulation, SignalType.Gate, SignalType.Trigger, SignalType.Modulation }, source.Outputs.Select(p => p.SignalType));
        session.Execute(new SetParameterValueCommand(child, new(carrier.Id, carrier.Parameters.Single(p => p.Definition.Key == OscillatorGenerator.PitchTrackingKey).Id), 1));
        session.Execute(new ConnectNodesCommand(child, new(source.Id, source.Outputs[0].Id), new(carrier.Id, carrier.Inputs.Single(p => p.Key == OscillatorGenerator.PitchPortKey).Id)));
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var engine = new PitchEngine();
        var service = new FaustGraphPlaybackService(compiler, engine);
        var snapshot = session.Snapshot;
        var lane = Assert.Single(compiler.Compile(snapshot).RhythmicInputs);
        Assert.True(lane.IsNote); Assert.False(lane.IsPitch); Assert.Null(lane.Pattern);
        var playing = service.PlayAsync(snapshot);
        Assert.False(await service.SendAsync(snapshot.Id, snapshot.Id, source.Id, new PerformanceNoteAction(PerformanceNoteKind.NoteOn, 69)));
        Assert.False(await service.SendAsync(snapshot.Id, child, source.Id, new PerformanceNoteAction(PerformanceNoteKind.NoteOn, 69, double.NaN)));
        Assert.True(await service.SendAsync(snapshot.Id, child, source.Id, new PerformanceNoteAction(PerformanceNoteKind.NoteOn, 69, .5)));
        Assert.True(await service.SendAsync(snapshot.Id, child, source.Id, new PerformanceNoteAction(PerformanceNoteKind.NoteOff, 69)));
        Assert.Equal(new[] { 70 + 512 * 64, 199 }, engine.Notes.Select(n => n.Code));
        Assert.Same(snapshot, session.Snapshot);
        Assert.Empty(engine.Updates); Assert.Empty(engine.Replacements);
        var reference = source.Parameters.Single(p => p.Definition.Key == NoteSequencerGenerator.ReferenceKey);
        session.Execute(new SetParameterValueCommand(child, new(source.Id, reference.Id), 57));
        await service.UpdateAsync(session.Snapshot);
        Assert.Empty(engine.Replacements);
        var shape = carrier.Parameters.Single(p => p.Definition.Key == "waveform");
        session.Execute(new SetParameterValueCommand(child, new(carrier.Id, shape.Id), 1));
        await service.UpdateAsync(session.Snapshot);
        Assert.Equal(0, Assert.Single(Assert.Single(engine.Replacements).RhythmicInputs!).InitialAction);
        Assert.True(await service.SendAsync(snapshot.Id, child, source.Id, new PerformanceNoteAction(PerformanceNoteKind.Reset)));
        Assert.Equal(129, engine.Notes.Last().Code);
        Assert.NotEqual(engine.Notes[0].Generation, engine.Notes.Last().Generation);
        await service.StopAsync(snapshot.Id); await playing;
    }

    [Fact]
    public async Task PerformanceNotesAreGraphQualifiedLatchedAndReseedCompatibleReplacement()
    {
        using var patch = new NestedFaustPatch();
        var session = patch.Session;
        var child = patch.ChildId;
        var carrier = session.Snapshot.GetGraph(child).GetNode(patch.Frequency.NodeId);
        var sequencer = NestedFaustPatch.Add(session, child, NoteSequencerGenerator.NodeType);
        var orphan = NestedFaustPatch.Add(session, child, NoteSequencerGenerator.NodeType);
        session.Execute(new SetParameterValueCommand(child, new(carrier.Id, carrier.Parameters.Single(p => p.Definition.Key == OscillatorGenerator.PitchTrackingKey).Id), 1));
        session.Execute(new ConnectNodesCommand(child, new(sequencer.Id, sequencer.Outputs.Single().Id), new(carrier.Id, carrier.Inputs.Single(p => p.Key == OscillatorGenerator.PitchPortKey).Id)));
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var engine = new PitchEngine();
        var service = new FaustGraphPlaybackService(compiler, engine);
        var snapshot = session.Snapshot;
        var playing = service.PlayAsync(snapshot);
        var program = compiler.Compile(snapshot);
        var lane = Assert.Single(program.RhythmicInputs);
        Assert.True(lane.IsPitch);
        Assert.Equal(child, lane.GraphId);
        Assert.Equal(sequencer.Id, lane.NodeId);
        Assert.False(await service.SendAsync(snapshot.Id, snapshot.Id, sequencer.Id, new PerformanceNoteAction(PerformanceNoteKind.NoteOn, 81)));
        Assert.False(await service.SendAsync(snapshot.Id, child, orphan.Id, new PerformanceNoteAction(PerformanceNoteKind.NoteOn, 81)));
        Assert.False(await service.SendAsync(snapshot.Id, child, sequencer.Id, new PerformanceNoteAction(PerformanceNoteKind.NoteOn, 0)));
        Assert.True(await service.SendAsync(snapshot.Id, child, sequencer.Id, new PerformanceNoteAction(PerformanceNoteKind.NoteOn, 81)));
        Assert.True(await service.SendAsync(snapshot.Id, child, sequencer.Id, new PerformanceNoteAction(PerformanceNoteKind.NoteOff, 81)));
        Assert.Equal(82, Assert.Single(engine.Notes).Code);
        Assert.Same(snapshot, session.Snapshot);
        Assert.Empty(engine.Updates);
        Assert.Empty(engine.Replacements);
        engine.Outputs = new() { ["/OrbitGraph/" + program.PitchMeters.Single().Name] = 12 };
        Assert.Equal(12, (await service.GetTelemetryAsync(snapshot.Id))!.PitchOffsets[child][sequencer.Id]);
        session.Execute(new SetPitchPatternStepsCommand(child, sequencer.Id, [82, .. new int[63]]));
        await service.UpdateAsync(session.Snapshot);
        Assert.Equal(82, Assert.Single(engine.Patterns).Pattern.Steps[0]);
        Assert.Empty(engine.Replacements);
        engine.Outputs["/OrbitGraph/" + program.PitchMeters.Single().StepName] = 1;
        Assert.Equal(1, (await service.GetTelemetryAsync(snapshot.Id))!.PitchSteps[child][sequencer.Id]);
        var shape = carrier.Parameters.Single(p => p.Definition.Key == "waveform");
        session.Execute(new SetParameterValueCommand(child, new(carrier.Id, shape.Id), 1));
        await service.UpdateAsync(session.Snapshot);
        Assert.Equal(82, Assert.Single(Assert.Single(engine.Replacements).RhythmicInputs!).InitialAction);
        Assert.True(await service.SendAsync(snapshot.Id, child, sequencer.Id, new PerformanceNoteAction(PerformanceNoteKind.Reset)));
        Assert.Equal(129, engine.Notes.Last().Code);
        Assert.NotEqual(engine.Notes[0].Generation, engine.Notes[1].Generation);
        await service.StopAsync(snapshot.Id);
        await playing;
        Assert.False(await service.SendAsync(snapshot.Id, child, sequencer.Id, new PerformanceNoteAction(PerformanceNoteKind.NoteOn, 81)));
    }

    private sealed class PitchEngine : RecordingEngine, IPerformanceNoteSink
    {
        public List<(Guid Generation, int Index, int Code)> Notes { get; } = [];
        public Task<bool> SendPerformanceNoteAsync(string soundId, Guid generation, int index, int code)
        {
            Notes.Add((generation, index, code));
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task EnvelopePlayheadIsQualifiedByOwningGraphAndClearedOnStop()
    {
        using var patch = new NestedFaustPatch();
        var child = patch.ChildId;
        var session = patch.Session;
        var envelope = NestedFaustPatch.Add(session, child, EnvelopeGenerator.NodeType);
        var orphan = NestedFaustPatch.Add(session, child, EnvelopeGenerator.NodeType);
        var carrier = session.Snapshot.GetGraph(child).GetNode(patch.Frequency.NodeId);
        session.Execute(new ConnectNodesCommand(child, new(envelope.Id, envelope.Outputs.Single().Id),
            new(carrier.Id, carrier.Inputs.Single(p => p.Key == "amplitude-modulation-in").Id), 0.2));
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var meter = Assert.Single(compiler.Compile(session.Snapshot).EnvelopeMeters);
        Assert.Equal(child, meter.GraphId);
        Assert.Equal(envelope.Id, meter.NodeId);
        var engine = new RecordingEngine();
        var service = new FaustGraphPlaybackService(compiler, engine);
        var playing = service.PlayAsync(session.Snapshot);
        engine.Outputs = new() { ["/OrbitGraph/" + meter.Name] = 2.125 };
        var frame = await service.GetTelemetryAsync(session.GraphId);
        Assert.NotNull(frame);
        Assert.Equal(new EnvelopePlayhead(EnvelopeStage.Decay, 0.25), frame.EnvelopePlayheads[child][envelope.Id]);
        Assert.False(frame.EnvelopePlayheads[child].ContainsKey(orphan.Id));
        Assert.False(frame.EnvelopePlayheads.ContainsKey(session.GraphId));
        await service.StopAsync(session.GraphId);
        await playing;
        Assert.Null(await service.GetTelemetryAsync(session.GraphId));
    }

    [Fact]
    public async Task ManualActionsUseReachableNestedManifestWithoutMutatingTheGraph()
    {
        using var patch = new NestedFaustPatch();
        var session = patch.Session;
        var child = patch.ChildId;
        var carrier = session.Snapshot.GetGraph(child).GetNode(patch.Frequency.NodeId);
        var trigger = NestedFaustPatch.Add(session, child, ManualTriggerGenerator.NodeType);
        var gate = NestedFaustPatch.Add(session, child, GateGenerator.NodeType);
        var clock = NestedFaustPatch.Add(session, child, ClockGenerator.NodeType);
        var orphan = NestedFaustPatch.Add(session, child, ManualTriggerGenerator.NodeType);
        void Wire(Orbit.Application.Graphs.Snapshots.GraphNodeSnapshot from, Orbit.Application.Graphs.Snapshots.GraphNodeSnapshot to, string key, double amount = 1) =>
            session.Execute(new ConnectNodesCommand(child, new(from.Id, from.Outputs[0].Id), new(to.Id, to.Inputs.Single(p => p.Key == key).Id), amount));
        void Set(Orbit.Application.Graphs.Snapshots.GraphNodeSnapshot node, string key, double value) =>
            session.Execute(new SetParameterValueCommand(child, new(node.Id, node.Parameters.Single(p => p.Definition.Key == key).Id), value));
        foreach (var source in new[] { trigger, gate, clock })
        {
            var ad = NestedFaustPatch.Add(session, child, EnvelopeGenerator.NodeType);
            Set(ad, EnvelopeGenerator.TriggerSourceKey, source == gate ? 2 : 1);
            Wire(source, ad, source == gate ? EnvelopeGenerator.GatePortKey : EnvelopeGenerator.EventTriggerPortKey);
            Wire(ad, carrier, "amplitude-modulation-in", 0.2);
        }
        var engine = new RhythmicEngine();
        var service = new FaustGraphPlaybackService(FaustGraphCompiler.CreateBuiltIn(), engine);
        var graph = session.Snapshot;
        Assert.False(await service.SendAsync(graph.Id, child, trigger.Id, RhythmicAction.Fire));
        var playing = service.PlayAsync(graph);
        var initial = engine.Request!;
        Assert.Equal(2, initial.TransportInputChannels);
        Assert.Equal(2, initial.RhythmicInputs!.Count);
        Assert.All(initial.RhythmicInputs, input => Assert.Equal(child, input.GraphId));
        int triggerIndex = initial.RhythmicInputs.ToList().FindIndex(input => input.NodeId == trigger.Id);
        Assert.True(await service.SendAsync(graph.Id, child, trigger.Id, RhythmicAction.Fire));
        Assert.True(await service.SendAsync(graph.Id, child, trigger.Id, RhythmicAction.Fire));
        Assert.False(await service.SendAsync(graph.Id, child, trigger.Id, RhythmicAction.Close));
        Assert.False(await service.SendAsync(graph.Id, graph.Id, trigger.Id, RhythmicAction.Fire));
        Assert.False(await service.SendAsync(graph.Id, child, orphan.Id, RhythmicAction.Fire));
        Assert.Equal(2, engine.Actions.Count);
        Assert.All(engine.Actions, action => Assert.Equal((initial.Generation, triggerIndex, RhythmicAction.Fire), action));
        Assert.True(await service.SendAsync(graph.Id, child, gate.Id, RhythmicAction.Close));
        Assert.Equal(graph.Revision, session.Snapshot.Revision);
        Assert.Empty(engine.Updates); Assert.Empty(engine.Replacements);
        Set(gate, GateGenerator.DurationKey, 250);
        await service.UpdateAsync(session.Snapshot);
        Assert.Single(engine.Updates); Assert.Empty(engine.Replacements);
        Set(gate, GateGenerator.SourceKey, 1);
        Wire(clock, gate, GateGenerator.TriggerPortKey);
        await service.UpdateAsync(session.Snapshot);
        var replacement = Assert.Single(engine.Replacements);
        Assert.NotEqual(initial.Generation, replacement.Generation);
        Assert.Single(replacement.RhythmicInputs!);
        Assert.False(await service.SendAsync(graph.Id, child, gate.Id, RhythmicAction.Fire));
        Assert.True(await service.SendAsync(graph.Id, child, trigger.Id, RhythmicAction.Fire));
        Assert.Equal(replacement.Generation, engine.Actions.Last().Generation);
        await service.StopAsync(graph.Id);
        await playing;
        Assert.False(await service.SendAsync(graph.Id, child, trigger.Id, RhythmicAction.Fire));
    }

    private sealed class RhythmicEngine : RecordingEngine, IRhythmicActionSink
    {
        public List<(Guid Generation, int Index, RhythmicAction Action)> Actions { get; } = [];
        public Task<bool> SendRhythmicActionAsync(string soundId, Guid generation, int index, RhythmicAction action)
        {
            Actions.Add((generation, index, action));
            return Task.FromResult(true);
        }
    }

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
        Assert.True(initial.TapSelectorCount > 0);
        var binding = service.Resolve(address);
        Assert.Equal(SignalObservationAvailability.Available, binding.Availability);
        Assert.Equal(initial.Generation, binding.Generation);
        Assert.Equal(initial.SoundId, binding.SoundId);
        Assert.Equal(SignalObservationAvailability.Unavailable, service.Resolve(address with { PortId = Guid.NewGuid() }).Availability);
        Assert.Equal(initial.Generation, (await subscription.ReadAsync()).Frame!.Generation);
        engine.CapacityReached = true;
        Assert.Equal(SignalObservationAvailability.Capacity, (await subscription.ReadAsync()).Availability);
        engine.CapacityReached = false;
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
        Assert.Contains("float(p < (", replacement.Source);
        Assert.Empty(engine.Updates);
        Assert.Single(replacement.Parameters.Keys.Except(engine.Request.Parameters.Keys));
        Assert.Empty(engine.Request.Parameters.Keys.Except(replacement.Parameters.Keys));
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

    [Fact]
    public async Task DiagnosticsSeparateSourceBuildReuseAndAcceptedReplacements()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings());
        var engine = new RecordingEngine();
        var service = new FaustGraphPlaybackService(FaustGraphCompiler.CreateBuiltIn(), engine);
        service.SetPerformanceCollection(true);
        var original = session.Snapshot;
        var playing = service.PlayAsync(original);
        await service.GetStateAsync(session.GraphId);
        var initial = service.GetGraphPerformanceSnapshot(session.GraphId)!;
        Assert.Equal(1L, initial.SourceBuilds);
        Assert.Equal(original.Revision, initial.AppliedRevision);
        // LFO + carrier + gain; the master output is not a DSP leaf.
        Assert.Equal(3, initial.ReachableLeafNodes);
        Assert.Equal(3, initial.ReachableEdges);
        Assert.Equal(0, initial.BoundaryRoutes);
        Assert.NotNull(initial.SourceGenerationMs);

        var oscillator = original.Nodes[1];
        var frequency = oscillator.Parameters.Single(p => p.Definition.Key == "frequency");
        session.Execute(new SetParameterValueCommand(session.GraphId, new(oscillator.Id, frequency.Id), 330));
        await service.UpdateAsync(session.Snapshot);
        var live = service.GetGraphPerformanceSnapshot(session.GraphId)!;
        Assert.Equal(1L, live.SourceBuilds);
        Assert.Equal(1L, live.ReusedUpdates);
        Assert.Equal(1L, live.ControlUpdates);
        Assert.NotNull(live.ControlUpdateMs);
        Assert.Equal(0L, live.AcceptedReplacements);

        session.Execute(new AddNodeCommand(session.GraphId, original.Nodes[0].TypeKey));
        await service.UpdateAsync(session.Snapshot);
        var disconnected = service.GetGraphPerformanceSnapshot(session.GraphId)!;
        Assert.Equal(2L, disconnected.SourceBuilds);
        Assert.Equal(2L, disconnected.ReusedUpdates);
        Assert.Equal(initial.ReachableLeafNodes, disconnected.ReachableLeafNodes);
        Assert.Empty(engine.Replacements);

        var waveform = oscillator.Parameters.Single(p => p.Definition.Key == "waveform");
        session.Execute(new SetParameterValueCommand(session.GraphId, new(oscillator.Id, waveform.Id), (int)OscillatorWaveform.Square));
        await service.UpdateAsync(session.Snapshot);
        var replacement = service.GetGraphPerformanceSnapshot(session.GraphId)!;
        Assert.Equal(3L, replacement.SourceBuilds);
        Assert.Equal(1L, replacement.AcceptedReplacements);
        Assert.Equal(session.Snapshot.Revision, replacement.AppliedRevision);
        Assert.Equal(session.Snapshot.Revision, Assert.Single(engine.Replacements).GraphRevision);
        Assert.Same(replacement, service.GetGraphPerformanceSnapshot(session.GraphId));
        await service.StopAsync(session.GraphId);
        await playing;
        Assert.Equal(replacement, service.GetGraphPerformanceSnapshot(session.GraphId));
    }

    [Fact]
    public async Task DiagnosticsAreOptInAndReopeningDuringPlaybackStartsFresh()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings());
        var engine = new RecordingEngine();
        var service = new FaustGraphPlaybackService(FaustGraphCompiler.CreateBuiltIn(), engine);
        var original = session.Snapshot;
        var playing = service.PlayAsync(original);
        await service.GetStateAsync(session.GraphId);
        Assert.Null(service.GetGraphPerformanceSnapshot(session.GraphId));
        service.SetPerformanceCollection(true);
        var opened = service.GetGraphPerformanceSnapshot(session.GraphId)!;
        Assert.Equal(0L, opened.SourceBuilds);
        Assert.Null(opened.SourceGenerationMs);
        Assert.Equal(3, opened.ReachableLeafNodes);
        Assert.Equal(original.Revision, opened.AppliedRevision);
        var oscillator = original.Nodes[1];
        var frequency = oscillator.Parameters.Single(p => p.Definition.Key == "frequency");
        session.Execute(new SetParameterValueCommand(session.GraphId, new(oscillator.Id, frequency.Id), 330));
        await service.UpdateAsync(session.Snapshot);
        Assert.Equal(1L, service.GetGraphPerformanceSnapshot(session.GraphId)!.ControlUpdates);
        service.SetPerformanceCollection(false);
        session.Execute(new SetParameterValueCommand(session.GraphId, new(oscillator.Id, frequency.Id), 450));
        await service.UpdateAsync(session.Snapshot);
        Assert.Null(service.GetGraphPerformanceSnapshot(session.GraphId));
        service.SetPerformanceCollection(true);
        var reopened = service.GetGraphPerformanceSnapshot(session.GraphId)!;
        Assert.Equal(0L, reopened.ControlUpdates);
        Assert.Null(reopened.ControlUpdateMs);
        Assert.Equal(session.Snapshot.Revision, reopened.AppliedRevision);
        Assert.Equal(2, engine.Updates.Count);
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
        public List<PitchPatternUpdate> Patterns { get; } = [];
        public Task<bool> SetGraphControlsAsync(string soundId, IReadOnlyDictionary<string, double> parameters, IReadOnlyList<PitchPatternUpdate> patterns)
        {
            Patterns.AddRange(patterns);
            return SetParametersAsync(soundId, parameters);
        }
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
        public bool SupportsDynamicTaps => true;
        public bool CapacityReached { get; set; }
        public List<Guid> Released { get; } = [];
        public Task<SignalObservationFrame?> ReadSignalAsync(string soundId, Guid generation, Guid subscriptionId, int channel) =>
            Task.FromResult<SignalObservationFrame?>(new(generation, 48000, 1, [0.1f]));
        public async Task<SignalObservationResult> ReadObservationAsync(string soundId, Guid generation, Guid subscriptionId, int selector) =>
            CapacityReached ? new(SignalObservationAvailability.Capacity) : new(SignalObservationAvailability.Available,
                await ReadSignalAsync(soundId, generation, subscriptionId, selector));
        public Task ReleaseSignalAsync(string soundId, Guid subscriptionId)
        {
            Released.Add(subscriptionId);
            return Task.CompletedTask;
        }
    }
}

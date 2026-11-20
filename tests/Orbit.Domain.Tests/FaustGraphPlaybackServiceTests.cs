using Orbit.Application.Graphs;
using Orbit.Application.Interfaces;
using Orbit.Application.Models;
using Orbit.Audio.Compilation;
using Orbit.Audio.Playback;
using Xunit;

namespace Orbit.Domain.Tests;

/// <summary>The playback use case translates the exact immutable snapshot to engine input.</summary>
public sealed class FaustGraphPlaybackServiceTests
{
    [Fact]
    public async Task PassesGeneratedSourceAndHostValuesToTheEngine()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings(220, 1, 50));
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var engine = new RecordingEngine();
        var service = new FaustGraphPlaybackService(compiler, engine);
        await service.InitializeAsync();
        await service.PlayAsync(session.Snapshot);
        var expected = compiler.Compile(session.Snapshot);
        Assert.True(engine.Initialized);
        Assert.NotNull(engine.Request);
        Assert.Equal(expected.Source, engine.Request.Source);
        Assert.Equal("OrbitGraph", engine.Request.Processor);
        Assert.Equal(expected.Controls.Count, engine.Request.Parameters.Count);
        foreach (var control in expected.Controls)
            Assert.Equal(control.InitialValue, engine.Request.Parameters[control.Name]);
        Assert.Equal(PlaybackState.Playing, await service.GetStateAsync(session.GraphId));
        await service.StopAsync(session.GraphId);
        Assert.Equal(engine.Request.SoundId, engine.StoppedSound);
        Assert.Equal(PlaybackState.Idle, await service.GetStateAsync(session.GraphId));
    }

    private sealed class RecordingEngine : IAudioEngine
    {
        public bool Initialized { get; private set; }
        public PlaybackRequest? Request { get; private set; }
        public string? StoppedSound { get; private set; }

        public Task InitializeAsync() { Initialized = true; return Task.CompletedTask; }
        public Task PlayAsync(PlaybackRequest request) { Request = request; return Task.CompletedTask; }
        public Task StopAsync(string soundId) { StoppedSound = soundId; return Task.CompletedTask; }
        public Task<PlaybackState> GetPlaybackStateAsync(string soundId) =>
            Task.FromResult(Request?.SoundId == soundId && StoppedSound != soundId ? PlaybackState.Playing : PlaybackState.Idle);
        public Task<Dictionary<string, double>?> GetOutputValuesAsync(string soundId) =>
            Task.FromResult<Dictionary<string, double>?>(null);
    }
}

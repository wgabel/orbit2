import test from "node:test";
import assert from "node:assert/strict";
import { AudioPerformanceMonitor } from "../../src/Orbit.Infrastructure/wwwroot/audio/Components/AudioPerformanceMonitor.js";

const context = () => ({
    state: "running", sampleRate: 48000, baseLatency: 0.004, outputLatency: 0.0084,
    playbackStats: { underrunEvents: 12, underrunDuration: 0.024, totalDuration: 10,
        resetLatency() { throw new Error("Must not change browser statistics"); } }
});

test("idle or unsupported metrics are never fabricated", () => {
    const monitor = new AudioPerformanceMonitor();
    const empty = monitor.snapshot(undefined);
    assert.equal(empty.contextState, "not-created");
    assert.equal(empty.playbackState, "idle");
    assert.equal(empty.underrunEvents, null);
    const legacy = { state: "suspended", sampleRate: 44100, baseLatency: 0.006 };
    monitor.begin({}, legacy);
    const snapshot = monitor.snapshot(legacy);
    assert.equal(snapshot.sampleRateHz, 44100);
    assert.equal(snapshot.baseLatencyMs, 6);
    assert.equal(snapshot.outputLatencyMs, null);
    assert.equal(snapshot.estimatedPipelineLatencyMs, null);
    assert.equal(snapshot.underrunEvents, null);
    assert.equal(snapshot.missingAudioPercent, null);
});

test("session counters exclude previous context use and report milliseconds", () => {
    const monitor = new AudioPerformanceMonitor(), audio = context();
    monitor.begin({}, audio);
    assert.equal(monitor.snapshot(audio).underrunEvents, 0);
    assert.equal(monitor.snapshot(audio).missingAudioPercent, null);
    Object.assign(audio.playbackStats, { underrunEvents: 15, underrunDuration: 0.03, totalDuration: 70 });
    const sample = monitor.snapshot(audio);
    assert.equal(sample.playbackState, "active");
    assert.equal(sample.underrunEvents, 3);
    assert.equal(sample.underrunDurationMs, 6);
    assert.equal(sample.playbackDurationMs, 60000);
    assert.equal(sample.missingAudioPercent, 0.01);
    assert.ok(Math.abs(sample.estimatedPipelineLatencyMs - 12.4) < 1e-9);
});

test("baseline reset is local; stopped session freezes and next play rebaselines", () => {
    const monitor = new AudioPerformanceMonitor(), audio = context(), sound = {};
    monitor.begin(sound, audio);
    audio.playbackStats.underrunEvents += 2;
    assert.equal(monitor.snapshot(audio).underrunEvents, 2);
    const reset = monitor.snapshot(audio, true);
    assert.equal(reset.underrunEvents, 0);
    assert.equal(audio.playbackStats.underrunEvents, 14);
    audio.playbackStats.underrunEvents++;
    monitor.end(sound);
    audio.playbackStats.underrunEvents += 5;
    assert.equal(monitor.snapshot(audio).playbackState, "stopped");
    assert.equal(monitor.snapshot(audio).underrunEvents, 1);
    monitor.begin({}, audio);
    assert.equal(monitor.snapshot(audio).underrunEvents, 0);
    assert.ok(monitor.snapshot(audio).sessionEpoch > reset.sessionEpoch);
});

test("overlapping accepted lifetimes share context counters and only last end freezes", () => {
    const monitor = new AudioPerformanceMonitor(), audio = context(), a = {}, b = {};
    monitor.begin(a, audio);
    audio.playbackStats.underrunEvents++;
    monitor.begin(b, audio);
    monitor.end(a);
    assert.equal(monitor.snapshot(audio).playbackState, "active");
    assert.equal(monitor.snapshot(audio).underrunEvents, 1);
    monitor.end(b);
    assert.equal(monitor.snapshot(audio).playbackState, "stopped");
});

test("decreasing counters rebaseline and missing/invalid fields stay unavailable", () => {
    const monitor = new AudioPerformanceMonitor(), audio = context();
    monitor.begin({}, audio);
    const epoch = monitor.snapshot(audio).sessionEpoch;
    audio.playbackStats.underrunEvents = 1;
    const reset = monitor.snapshot(audio);
    assert.ok(reset.sessionEpoch > epoch);
    assert.equal(reset.underrunEvents, 0);
    audio.playbackStats.underrunEvents = NaN;
    audio.playbackStats.underrunDuration = -1;
    audio.playbackStats.totalDuration = Infinity;
    assert.equal(monitor.snapshot(audio).underrunEvents, null);
    assert.equal(monitor.snapshot(audio).underrunDurationMs, null);
    assert.equal(monitor.snapshot(audio).playbackDurationMs, null);
    audio.playbackStats.underrunEvents = 15;
    assert.equal(monitor.snapshot(audio).underrunEvents, null);
    audio.playbackStats.underrunEvents++;
    assert.equal(monitor.snapshot(audio).underrunEvents, 1);
});

test("new context or throwing optional getter does not inherit old session", () => {
    const monitor = new AudioPerformanceMonitor(), audio = context();
    monitor.begin({}, audio);
    const next = { state: "running", sampleRate: 44100,
        get playbackStats() { throw new Error("not permitted"); },
        get outputLatency() { throw new Error("unsupported"); } };
    assert.equal(monitor.snapshot(next).playbackState, "idle");
    monitor.begin({}, next);
    assert.equal(monitor.snapshot(next).underrunEvents, null);
    assert.equal(monitor.snapshot(next).outputLatencyMs, null);
});

test("missing fraction is unavailable when recovered counters cover different intervals", () => {
    const monitor = new AudioPerformanceMonitor(), audio = context();
    monitor.begin({}, audio);
    audio.playbackStats.underrunDuration = undefined;
    audio.playbackStats.totalDuration += 5;
    assert.equal(monitor.snapshot(audio).missingAudioPercent, null);
    audio.playbackStats.underrunDuration = 1;
    monitor.snapshot(audio);
    audio.playbackStats.underrunDuration = 2;
    audio.playbackStats.totalDuration += 1;
    assert.equal(monitor.snapshot(audio).underrunDurationMs, 1000);
    assert.equal(monitor.snapshot(audio).missingAudioPercent, null);
    monitor.snapshot(audio, true);
    audio.playbackStats.underrunDuration += 0.01;
    audio.playbackStats.totalDuration += 1;
    assert.ok(Math.abs(monitor.snapshot(audio).missingAudioPercent - 1) < 1e-9);
});

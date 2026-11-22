import test from "node:test";
import assert from "node:assert/strict";
import { RuntimeDiagnostics } from "../../src/Orbit.Infrastructure/wwwroot/audio/Components/RuntimeDiagnostics.js";

test("acceptance and fade completion are separate; stale attempts never replace last accepted", () => {
    let time = 0;
    const metrics = new RuntimeDiagnostics(() => time);
    const first = metrics.begin("play", { generation: "first", graphRevision: 1 });
    first.timings.workerRoundTripMs = 24;
    time = 30;
    metrics.accept(first);
    metrics.finish(first, "accepted");
    const obsolete = metrics.begin("replace", { generation: "old" }, 2);
    const latest = metrics.begin("replace", { generation: "new" }, 3);
    time = 45;
    metrics.finish(obsolete, "superseded");
    assert.equal(metrics.snapshot([]).lastAttempt.revision, 3);
    assert.equal(metrics.snapshot([]).lastAccepted.revision, 1);
    time = 70;
    metrics.accept(latest, 50);
    const fading = metrics.snapshot([]);
    assert.equal(fading.lastAccepted.acceptanceMs, 40);
    assert.equal(fading.lastAccepted.completionMs, null);
    assert.equal(fading.pending, 1);
    time = 125;
    metrics.finish(latest, "accepted");
    const complete = metrics.snapshot([]);
    assert.equal(complete.lastAccepted.completionMs, 95);
    assert.equal(complete.lastAccepted.fadeDurationMs, 50);
    assert.equal(complete.pending, 0);
    assert.equal(complete.attempts, 3);
    assert.equal(complete.accepted, 2);
    assert.equal(complete.superseded, 1);
});

test("resources count current and retiring managed voices, actual existing capture leases only", () => {
    const metrics = new RuntimeDiagnostics();
    const voice = (outputChannels, occupiedSlots, activeLeases) => ({
        outputChannels, voice: { getSignalDiagnostics: () => ({ occupiedSlots, activeLeases }) }
    });
    const old = voice(5, 2, 3), current = voice(8, 1, 2);
    const playback = { current, voices: new Set([old, current]), cancelled: false };
    metrics.processorError();
    const state = metrics.snapshot([playback]);
    assert.equal(state.currentVoices, 1);
    assert.equal(state.retiringVoices, 1);
    assert.equal(state.outputChannels, 13);
    assert.equal(state.occupiedAuxSlots, 3);
    assert.equal(state.captureLeases, 5);
    assert.equal(state.processorErrors, 1);
    playback.cancelled = true;
    assert.equal(metrics.snapshot([playback]).currentVoices, 0);
    assert.equal(metrics.snapshot([]).captureLeases, 0);
});

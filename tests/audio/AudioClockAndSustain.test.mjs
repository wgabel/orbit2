// Shared-clock observations and sustained voice behavior with fake browser objects.
import test from "node:test";
import assert from "node:assert/strict";
import { AudioClock } from "../../src/Orbit.Infrastructure/wwwroot/audio/Components/AudioClock.js";
import { AudioContextManager } from "../../src/Orbit.Infrastructure/wwwroot/audio/Components/AudioContextManager.js";
import { AudioEnvelope } from "../../src/Orbit.Infrastructure/wwwroot/audio/Components/AudioEnvelope.js";
import { AudioVoice } from "../../src/Orbit.Infrastructure/wwwroot/audio/Components/AudioVoice.js";

test("clock query does not create a context and uses its actual sample rate", async () => {
    let created = 0;
    const context = {
        currentTime: 0.5,
        sampleRate: 44100,
        state: "suspended",
        async resume() { this.state = "running"; },
        async close() { this.state = "closed"; }
    };
    const manager = new AudioContextManager(() => { created++; return context; });
    const clock = new AudioClock(manager);
    assert.equal(clock.getSnapshot(), null);
    assert.equal(created, 0);

    await manager.getRunningContext();
    assert.deepEqual(clock.getSnapshot(), { currentSample: 22050, sampleRate: 44100, isRunning: true });
    context.currentTime = 1;
    context.state = "suspended";
    assert.deepEqual(clock.getSnapshot(), { currentSample: 44100, sampleRate: 44100, isRunning: false });
    assert.equal(created, 1);

    await manager.dispose();
    assert.equal(clock.getSnapshot(), null);
});

test("sustained voice schedules an attack and releases on the audio clock", async () => {
    const calls = [];
    const context = { currentTime: 1, state: "running" };
    const gain = {
        cancelScheduledValues: time => calls.push(["cancel", time]),
        setValueAtTime: (value, time) => calls.push(["set", value, time]),
        linearRampToValueAtTime: (value, time) => calls.push(["ramp", value, time]),
        cancelAndHoldAtTime: time => calls.push(["hold", time])
    };
    const node = { disconnect: () => calls.push("disconnect"), destroy: () => calls.push("destroy") };
    const voice = new AudioVoice(context, node, { gain, disconnect: () => calls.push("gain-disconnect") },
        new AudioEnvelope());

    const playing = voice.play({ volume: 0.1 });
    assert.deepEqual(calls, [
        ["cancel", 1.01], ["set", 0, 1.01], ["ramp", 0.1, 1.03]
    ]);

    context.currentTime = 2;
    voice.stop();
    assert.deepEqual(calls.slice(-2), [["hold", 2], ["ramp", 0, 2.04]]);
    context.currentTime = 2.05;
    await playing;
    voice.dispose();
    assert.deepEqual(calls.slice(-3), ["disconnect", "destroy", "gain-disconnect"]);
});

test("stopping a suspended context cancels gain automation immediately", async () => {
    const calls = [];
    const context = { currentTime: 1, state: "running" };
    const gain = {
        value: 0.1,
        cancelScheduledValues: time => calls.push(["cancel", time]),
        setValueAtTime: (value, time) => calls.push(["set", value, time]),
        linearRampToValueAtTime: (value, time) => calls.push(["ramp", value, time])
    };
    const node = { disconnect: () => {}, destroy: () => {} };
    const voice = new AudioVoice(context, node, { gain, disconnect: () => {} },
        new AudioEnvelope());

    const playing = voice.play({ volume: 0.1 });
    context.state = "suspended";
    voice.stop();
    assert.equal(gain.value, 0);
    assert.deepEqual(calls.at(-1), ["cancel", context.currentTime]);
    await playing;
    voice.dispose();
});

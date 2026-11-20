// Regression tests for orchestration and lifecycle, with fake browser objects.
import test from "node:test";
import assert from "node:assert/strict";
import { AudioEngine } from "../../src/Orbit.Infrastructure/wwwroot/audio/AudioEngine.js";

const request = {
    soundId: "modulation-graph", processor: "ModulatedSine",
    volume: 0.1, parameters: { baseFrequency: 440, lfoRate: 0.5, depthHz: 100 }
};

function setup() {
    const calls = [];
    const context = {};
    const node = { disconnect: () => calls.push("disconnect"), destroy: () => calls.push("destroy") };
    let finishPlayback;
    const voice = {
        play: async value => {
            calls.push(["play", value]);
            await new Promise(resolve => { finishPlayback = resolve; });
        },
        stop: () => { calls.push("stop"); finishPlayback?.(); },
        dispose: () => calls.push("dispose")
    };

    const contextManager = {
        getRunningContext: async () => { calls.push("resume"); return context; },
        dispose: async () => calls.push("close")
    };
    const nodeFactory = {
        create: async (actualContext, processor, parameters) => {
            assert.equal(actualContext, context);
            calls.push(["create", processor, parameters]);
            return node;
        }
    };
    const output = { createVoice: () => voice, dispose: () => calls.push("output-dispose") };
    const monitor = { getSnapshot: () => ({ "/ModulatedSine/effectiveFrequency": 430 }),
        dispose: () => calls.push("monitor-dispose") };
    return { engine: new AudioEngine(contextManager, nodeFactory, output, () => monitor), calls,
        finish: () => finishPlayback(), contextManager, nodeFactory, output };
}

test("resumes context first, creates voice, and releases its resources", async () => {
    const { engine, calls, finish } = setup();
    const playback = engine.play(request);
    await new Promise(resolve => setImmediate(resolve));
    assert.deepEqual(calls, ["resume", ["create", "ModulatedSine", request.parameters], ["play", request]]);
    finish();
    await playback;
    assert.equal(calls.at(-1), "dispose");
});

test("rejects duplicate IDs and stops only the requested playback", async () => {
    const { engine, calls } = setup();
    const playback = engine.play(request);
    await new Promise(resolve => setImmediate(resolve));
    await assert.rejects(engine.play(request), /already active/);
    engine.stop("other");
    assert.ok(!calls.includes("stop"));
    engine.stop(request.soundId);
    await playback;
    assert.deepEqual(calls.slice(-3), ["stop", "monitor-dispose", "dispose"]);
});

test("cancels a pending node creation and destroys the resulting node", async () => {
    const { engine, calls, nodeFactory } = setup();
    let complete;
    nodeFactory.create = () => new Promise(resolve => { complete = resolve; });
    const playback = engine.play(request);
    await new Promise(resolve => setImmediate(resolve));
    engine.stop(request.soundId);
    complete({ disconnect: () => calls.push("disconnect"), destroy: () => calls.push("destroy") });
    await playback;
    assert.deepEqual(calls, ["resume", "disconnect", "destroy"]);
});

test("rejects invalid requests without invoking browser infrastructure", async () => {
    const { engine, calls } = setup();
    await assert.rejects(engine.play({ ...request, volume: 2 }), /Invalid audio playback/);
    assert.deepEqual(calls, []);
});

test("exposes meter snapshots only during active playback", async () => {
    const { engine, calls, finish } = setup();
    assert.equal(engine.getOutputValues(request.soundId), null);
    const playback = engine.play(request);
    await new Promise(resolve => setImmediate(resolve));
    assert.deepEqual(engine.getOutputValues(request.soundId), { "/ModulatedSine/effectiveFrequency": 430 });
    assert.equal(engine.getOutputValues("other"), null);
    finish();
    await playback;
    assert.equal(engine.getOutputValues(request.soundId), null);
    assert.deepEqual(calls.slice(-2), ["monitor-dispose", "dispose"]);
});

test("dynamic playback passes source after resume and exposes lifecycle state", async () => {
    const { engine, calls, nodeFactory } = setup();
    const dynamic = { ...request, processor: "OrbitGraph", source: "process=0;" };
    let finishCreation;
    nodeFactory.create = (context, processor, parameters, source) => {
        calls.push([processor, source]);
        return new Promise(resolve => { finishCreation = resolve; });
    };
    assert.equal(engine.getPlaybackState(dynamic.soundId), "idle");
    const task = engine.play(dynamic);
    assert.equal(engine.getPlaybackState(dynamic.soundId), "starting");
    await new Promise(resolve => setImmediate(resolve));
    assert.deepEqual(calls, ["resume", ["OrbitGraph", "process=0;"]]);
    finishCreation({});
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(engine.getPlaybackState(dynamic.soundId), "playing");
    engine.stop(dynamic.soundId);
    assert.equal(engine.getPlaybackState(dynamic.soundId), "stopping");
    await task;
    assert.equal(engine.getPlaybackState(dynamic.soundId), "idle");
});

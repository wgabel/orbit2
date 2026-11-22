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
    const context = { state: "running", sampleRate: 48000,
        playbackStats: { underrunEvents: 3, underrunDuration: 0, totalDuration: 10 } };
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
        getCurrentContext: () => calls.includes("resume") ? context : undefined,
        dispose: async () => calls.push("close")
    };
    const nodeFactory = {
        create: async (actualContext, processor, parameters) => {
            assert.equal(actualContext, context);
            calls.push(["create", processor, parameters]);
            return node;
        },
        setParameters: (actualNode, parameters) => calls.push(["parameters", { ...parameters }])
    };
    const output = { createVoice: () => voice, dispose: () => calls.push("output-dispose") };
    const monitor = { getSnapshot: () => ({ "/ModulatedSine/effectiveFrequency": 430 }),
        dispose: () => calls.push("monitor-dispose") };
    return { engine: new AudioEngine(contextManager, nodeFactory, output, () => monitor), calls,
        finish: () => finishPlayback(), contextManager, nodeFactory, output, context };
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

test("live changes update the existing worklet, reject unknown controls, and stop accepting edits on release", async () => {
    const { engine, calls } = setup();
    assert.equal(engine.setParameters(request.soundId, { baseFrequency: 330 }), false);
    const playback = engine.play(request);
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(engine.setParameters(request.soundId, { baseFrequency: 330 }), true);
    assert.deepEqual(calls.at(-1), ["parameters", { baseFrequency: 330 }]);
    assert.throws(() => engine.setParameters(request.soundId, { baseFrequency: 220, invalid: 1 }), /Invalid audio parameter/);
    assert.throws(() => engine.setParameters(request.soundId, { baseFrequency: NaN }), /Invalid audio parameter/);
    assert.equal(calls.filter(call => Array.isArray(call) && call[0] === "create").length, 1);
    assert.equal(calls.filter(call => Array.isArray(call) && call[0] === "parameters").length, 1);
    engine.stop(request.soundId);
    assert.equal(engine.setParameters(request.soundId, { baseFrequency: 220 }), false);
    await playback;
});

test("edits during compilation are coalesced and applied before the voice starts", async () => {
    const { engine, calls, nodeFactory } = setup();
    let complete;
    nodeFactory.create = () => new Promise(resolve => { complete = resolve; });
    const playback = engine.play(request);
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(engine.setParameters(request.soundId, { baseFrequency: 330, lfoRate: 2 }), true);
    assert.equal(engine.setParameters(request.soundId, { baseFrequency: 550 }), true);
    complete({});
    await new Promise(resolve => setImmediate(resolve));
    assert.deepEqual(calls, ["resume", ["parameters", { baseFrequency: 550, lfoRate: 2 }], ["play", request]]);
    engine.stop(request.soundId);
    await playback;
});

function replacementSetup(createActions) {
    const events = [];
    const nodes = [];
    const voices = [];
    const context = { currentTime: 3, state: "running", playbackStats: { underrunEvents: 9, underrunDuration: 0, totalDuration: 8 } };
    const makeNode = name => {
        const node = { name, disconnect() { events.push(["disconnect", name]); },
            destroy() { events.push(["destroy", name]); } };
        nodes.push(node);
        return node;
    };
    const nodeFactory = {
        create: async (_context, processor) => makeNode(processor),
        setParameters: (node, values) => events.push(["parameters", node.name, values])
    };
    const output = {
        createVoice: (_context, node) => {
            let finish;
            const voice = {
                play: (request, transition) => {
                    events.push(["play", node.name, transition]);
                    return new Promise(resolve => { finish = resolve; });
                },
                stop: transition => { events.push(["stop", node.name, transition]); finish(); },
                observeSignal: (lease, selector, generation) => {
                    events.push(['observe', node.name, lease, selector, generation]);
                    return {availability: 2, frame: null};
                },
                releaseSignal: lease => events.push(['release', node.name, lease]),
                dispose: () => { events.push(["dispose", node.name]); node.destroy(); }
            };
            voices.push(voice);
            return voice;
        }
    };
    const engine = new AudioEngine({ getRunningContext: async () => context, getCurrentContext: () => context }, nodeFactory, output,
        node => ({ getSnapshot: () => ({ name: node.name }), dispose: () => events.push(["monitor", node.name]) }), null, createActions);
    return { engine, events, nodes, voices, nodeFactory, makeNode, context };
}

test("rhythmic commands target only the accepted running generation", async () => {
    const actions = [];
    const {engine, nodeFactory, makeNode, context} = replacementSetup(async (_context, node) => ({
        send: (index, action) => { actions.push([node.name, index, action]); return true; },
        deactivate: () => actions.push([node.name, 'deactivate']), dispose: () => actions.push([node.name, 'dispose'])
    }));
    const rhythm = {...request, generation: 'original', rhythmicInputs: [{allowClose: true}]};
    const playing = engine.play(rhythm);
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(engine.sendRhythmicAction(rhythm.soundId, 'original', 0, 1), true);
    assert.equal(engine.sendRhythmicAction(rhythm.soundId, 'stale', 0, 1), false);
    context.state = 'suspended';
    assert.equal(engine.sendRhythmicAction(rhythm.soundId, 'original', 0, 1), false);
    context.state = 'running';
    let complete;
    nodeFactory.create = () => new Promise(resolve => { complete = resolve; });
    const replacing = engine.replace({...rhythm, processor: 'next', generation: 'next'}, 1);
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(engine.sendRhythmicAction(rhythm.soundId, 'original', 0, 1), false);
    complete(makeNode('next'));
    assert.equal(await replacing, true);
    assert.equal(engine.sendRhythmicAction(rhythm.soundId, 'original', 0, 1), false);
    assert.equal(engine.sendRhythmicAction(rhythm.soundId, 'next', 0, 2), true);
    engine.stop(rhythm.soundId);
    await playing;
    assert.equal(engine.sendRhythmicAction(rhythm.soundId, 'next', 0, 1), false);
    assert.deepEqual(actions, [[request.processor, 0, 1], [request.processor, 'deactivate'], [request.processor, 'dispose'], ['next', 0, 2], ['next', 'deactivate'], ['next', 'dispose']]);
});

test("replacement crossfades on one clock and does not end the logical playback", async () => {
    const { engine, events } = replacementSetup();
    const playing = engine.play(request);
    await new Promise(resolve => setImmediate(resolve));
    const replacement = { ...request, processor: "new-program", parameters: { gain: 0.2 } };
    assert.equal(await engine.replace(replacement, 10), true);
    const transition = { startTime: 3.01, duration: 0.05 };
    assert.deepEqual(events.find(event => event[0] === "play" && event[1] === "new-program")[2], transition);
    assert.deepEqual(events.find(event => event[0] === "stop" && event[1] === request.processor)[2], transition);
    assert.ok(events.some(event => event[0] === "dispose" && event[1] === request.processor));
    assert.equal(engine.getPlaybackState(request.soundId), "playing");
    assert.deepEqual(engine.getOutputValues(request.soundId), { name: "new-program" });
    assert.equal(engine.setParameters(request.soundId, { gain: 0.4 }), true);
    assert.deepEqual(events.at(-1), ["parameters", "new-program", { gain: 0.4 }]);
    engine.stop(request.soundId);
    await playing;
    assert.equal(engine.getPlaybackState(request.soundId), "idle");
});

test('observation requests and releases cannot cross playback generations', async () => {
    const {engine, events} = replacementSetup();
    const original = {...request, generation: 'old', outputChannels: 9, tapSelectorCount: 15};
    const playing = engine.play(original);
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(engine.readObservation(original.soundId, 'old', 'lease', 15).availability, 2);
    assert.equal(await engine.replace({...original, processor: 'new', generation: 'new'}, 1), true);
    assert.ok(events.some(event => event[0] === 'release' && event[1] === original.processor));
    const count = events.length;
    engine.readObservation(original.soundId, 'old', 'lease', 2);
    engine.releaseSignal(original.soundId, 'lease', 'old');
    assert.equal(events.length, count);
    engine.readObservation(original.soundId, 'new', 'lease', 15);
    assert.deepEqual(events.at(-1), ['observe', 'new', 'lease', 15, 'new']);
    engine.releaseSignal(original.soundId, 'lease', 'old');
    assert.equal(events.at(-1)[0], 'observe');
    engine.releaseSignal(original.soundId, 'lease', 'new');
    assert.deepEqual(events.at(-1), ['release', 'new', 'lease']);
    engine.stop(original.soundId);
    await playing;
});

test("failed replacement keeps the old voice alive", async () => {
    const { engine, nodeFactory, events } = replacementSetup();
    const playing = engine.play(request);
    await new Promise(resolve => setImmediate(resolve));
    nodeFactory.create = async () => { throw new Error("Faust compilation failed"); };
    await assert.rejects(engine.replace({ ...request, processor: "invalid" }, 10), /compilation failed/);
    assert.equal(engine.getPlaybackState(request.soundId), "playing");
    assert.ok(!events.some(event => event[0] === "stop"));
    engine.stop(request.soundId);
    await playing;
});

test("superseded replacement destroys its late node without interrupting playback", async () => {
    const { engine, nodeFactory, makeNode, events } = replacementSetup();
    const playing = engine.play(request);
    await new Promise(resolve => setImmediate(resolve));
    let complete;
    nodeFactory.create = () => new Promise(resolve => { complete = resolve; });
    const replacement = engine.replace({ ...request, processor: "stale" }, 10);
    await new Promise(resolve => setImmediate(resolve));
    engine.markRevision(request.soundId, 11);
    complete(makeNode("stale"));
    assert.equal(await replacement, false);
    assert.ok(events.some(event => event[0] === "destroy" && event[1] === "stale"));
    assert.ok(!events.some(event => event[0] === "stop"));
    nodeFactory.create = async (_context, processor) => makeNode(processor);
    assert.equal(await engine.replace({ ...request, processor: "latest" }, 11), true);
    engine.stop(request.soundId);
    await playing;
});

test("stop while replacement compiles cannot resurrect the playback", async () => {
    const { engine, nodeFactory, makeNode, events } = replacementSetup();
    const playing = engine.play(request);
    await new Promise(resolve => setImmediate(resolve));
    let complete;
    nodeFactory.create = () => new Promise(resolve => { complete = resolve; });
    const replacement = engine.replace({ ...request, processor: "late" }, 10);
    await new Promise(resolve => setImmediate(resolve));
    engine.stop(request.soundId);
    await playing;
    complete(makeNode("late"));
    assert.equal(await replacement, false);
    assert.ok(events.some(event => event[0] === "destroy" && event[1] === "late"));
    assert.ok(!events.some(event => event[0] === "play" && event[1] === "late"));
    assert.equal(engine.getPlaybackState(request.soundId), "idle");
});

test("performance reads do not create audio, and track accepted playback through stop", async () => {
    const { engine, calls, context } = setup();
    assert.equal(engine.getPerformanceSnapshot().contextState, "not-created");
    assert.deepEqual(calls, []);
    const playback = engine.play(request);
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(engine.getPerformanceSnapshot().playbackState, "active");
    context.playbackStats.underrunEvents++;
    const before = [...calls];
    assert.equal(engine.getPerformanceSnapshot().underrunEvents, 1);
    assert.equal(engine.getPerformanceSnapshot(true).underrunEvents, 0);
    assert.deepEqual(calls, before);
    context.playbackStats.underrunEvents += 2;
    engine.stop(request.soundId);
    await playback;
    context.playbackStats.underrunEvents += 3;
    assert.equal(engine.getPerformanceSnapshot().playbackState, "stopped");
    assert.equal(engine.getPerformanceSnapshot().underrunEvents, 2);
});

test("structural replacement preserves the accepted performance session", async () => {
    const { engine, context } = replacementSetup();
    const playing = engine.play(request);
    await new Promise(resolve => setImmediate(resolve));
    const epoch = engine.getPerformanceSnapshot().sessionEpoch;
    context.playbackStats.underrunEvents += 2;
    assert.equal(await engine.replace({ ...request, processor: "replacement" }, 1), true);
    const snapshot = engine.getPerformanceSnapshot();
    assert.equal(snapshot.sessionEpoch, epoch);
    assert.equal(snapshot.underrunEvents, 2);
    engine.stop(request.soundId);
    await playing;
});

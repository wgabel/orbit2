// Test asynchronous worker ownership, failures and retry without a real device.
import test from "node:test";
import assert from "node:assert/strict";
import { FaustCompilerClient } from "../../src/Orbit.Infrastructure/wwwroot/audio/Faust/FaustCompilerClient.js";
import { FaustNodeFactory } from "../../src/Orbit.Infrastructure/wwwroot/audio/Faust/FaustNodeFactory.js";

function setup() {
    const workers = [];
    const client = new FaustCompilerClient(() => {
        const worker = {
            messages: [], terminated: false,
            postMessage(message) { this.messages.push(message); },
            terminate() { this.terminated = true; },
            receive(data) { this.onmessage({ data }); }
        };
        workers.push(worker);
        return worker;
    });
    return { client, workers };
}

test("worker starts lazily and matches replies to concurrent requests", async () => {
    const { client, workers } = setup();
    assert.equal(workers.length, 0);
    const first = client.compile("process = 0;");
    const second = client.compile("process = 1;");
    assert.equal(workers.length, 1);
    const [a, b] = workers[0].messages;
    workers[0].receive({ id: b.id, factory: { module: "two", json: "{}" } });
    workers[0].receive({ id: a.id, factory: { module: "one", json: "{}" } });
    assert.equal((await first).module, "one");
    assert.equal((await second).module, "two");
    client.dispose();
});

test("compiler error rejects the request but keeps the worker usable", async () => {
    const { client, workers } = setup();
    const first = client.compile("invalid");
    workers[0].receive({ id: workers[0].messages[0].id, error: "syntax error" });
    await assert.rejects(first, /syntax error/);
    const next = client.compile("process=0;");
    workers[0].receive({ id: workers[0].messages[1].id, factory: { module: "fixed", json: "{}" } });
    assert.equal((await next).module, "fixed");
    client.dispose();
});

test("worker failure rejects all pending requests and permits retry", async () => {
    const { client, workers } = setup();
    const first = client.compile("process=0;");
    const second = client.compile("process=1;");
    workers[0].onerror({ message: "worker failed" });
    await assert.rejects(first, /worker failed/);
    await assert.rejects(second, /worker failed/);
    assert.equal(workers[0].terminated, true);
    const next = client.compile("process=0;");
    workers[1].receive({ id: workers[1].messages[0].id, factory: { module: "retry", json: "{}" } });
    assert.equal((await next).module, "retry");
    client.dispose();
});

test("dispose rejects pending work and prevents new compilation", async () => {
    const { client, workers } = setup();
    const pending = client.compile("process=0;");
    client.dispose();
    await assert.rejects(pending, /disposed/);
    await assert.rejects(client.compile("process=1;"), /disposed/);
    assert.equal(workers[0].terminated, true);
});

test("dynamic factory creates a worklet from a compiler result and applies graph controls", async () => {
    const compiled = { module: "wasm", json: "{}", shaKey: "source-hash" };
    const calls = [];
    const node = {
        getParams: () => ["/OrbitGraph/p_frequency"],
        setParamValue: (path, value) => calls.push([path, value])
    };
    const compiler = { compile: async source => {
        assert.equal(source, "process=0;");
        return compiled;
    }};
    const factory = new FaustNodeFactory(
        { load: () => assert.fail("precompiled loader invoked") }, new Map(),
        () => ({ createNode: async (context, name, actual, scriptProcessor) => {
            assert.equal(name, "OrbitGraph");
            assert.deepEqual(actual, { ...compiled, soundfiles: {} });
            assert.equal(scriptProcessor, false);
            return node;
        }}), compiler);
    const timings = {};
    assert.equal(await factory.create({}, "OrbitGraph", { p_frequency: 440 }, "process=0;", timings), node);
    assert.ok(timings.workerRoundTripMs >= 0);
    assert.ok(timings.workletCreateMs >= 0);
    assert.ok(timings.parameterBindingMs >= 0);
    assert.deepEqual(calls, [["/OrbitGraph/p_frequency", 440]]);
    factory.setParameters(node, { p_frequency: 330 });
    assert.deepEqual(calls.at(-1), ["/OrbitGraph/p_frequency", 330]);
    assert.throws(() => factory.setParameters(node, { p_frequency: 220, unknown: 1 }), /Unknown or ambiguous/);
    assert.equal(calls.length, 2);
});

test("silent gated instruments tolerate controls Faust optimized away during initialization", async () => {
    // An unconnected physical Clarinet Gate becomes constant zero. Faust may
    // eliminate Attack, its audio path, and an otherwise valid mute slider.
    const writes = [];
    const node = {
        getParams: () => ["/OrbitGraph/p_pitch", "/OrbitGraph/orbit_tap_select_1"],
        setParamValue: (path, value) => writes.push([path, value])
    };
    const factory = new FaustNodeFactory(null, new Map(),
        () => ({ createNode: async () => node }), { compile: async () => ({ json: "{}", module: "wasm" }) });
    await factory.create({}, "OrbitGraph",
        { p_pitch: 0, p_attack: .055, mute_clarinet: 1 }, "process = 0;");
    assert.deepEqual(writes, [["/OrbitGraph/p_pitch", 0]]);

    // Those same accepted graph controls remain editable without throwing.
    // A new, present worklet control not in the initial manifest still works.
    factory.setParameters(node, { p_attack: .2, mute_clarinet: 0, p_pitch: 5,
        orbit_tap_select_1: 2 });
    assert.deepEqual(writes.slice(1), [["/OrbitGraph/p_pitch", 5],
        ["/OrbitGraph/orbit_tap_select_1", 2]]);

    // Never swallow a typo or partially apply a batch.
    const count = writes.length;
    assert.throws(() => factory.setParameters(node, { p_pitch: 7, unrecognized: 1 }), /Unknown or ambiguous/);
    assert.equal(writes.length, count);
    assert.throws(() => factory.setParameters(node, { p_attack: Infinity }), /Invalid parameter value/);
});

test("fully pruned graphs keep only their own omitted controls, not arbitrary edits", async () => {
    const nodes = [[], ["/OrbitGraph/p_attack"]].map(paths => ({
        getParams: () => paths,
        writes: [],
        setParamValue(path, value) { this.writes.push([path, value]); }
    }));
    const factory = new FaustNodeFactory(null, new Map(),
        () => ({ createNode: async () => nodes.shift() }), { compile: async () => ({ json: "{}", module: "wasm" }) });
    const first = await factory.create({}, "OrbitGraph", { p_attack: .055 }, "process = 0;");
    factory.setParameters(first, { p_attack: .1 });
    assert.deepEqual(first.writes, []);
    assert.throws(() => factory.setParameters(first, { other: 1 }), /Unknown or ambiguous/);
    const second = await factory.create({}, "OrbitGraph", { p_attack: .055 }, "process = 1;");
    factory.setParameters(second, { p_attack: .2 });
    assert.deepEqual(second.writes, [["/OrbitGraph/p_attack", .055], ["/OrbitGraph/p_attack", .2]]);
});

test("ambiguous Faust paths still reject and clean up before writing any initial controls", async () => {
    const writes = [], cleanup = [];
    const node = {
        getParams: () => ["/first/gain", "/second/gain", "/first/pitch"],
        setParamValue: (path, value) => writes.push([path, value]),
        disconnect: () => cleanup.push("disconnect"),
        destroy: () => cleanup.push("destroy")
    };
    const factory = new FaustNodeFactory(null, new Map(),
        () => ({ createNode: async () => node }), { compile: async () => ({ json: "{}", module: "wasm" }) });
    await assert.rejects(factory.create({}, "OrbitGraph", { pitch: 440, gain: 1 }, "process = 0;"),
        /Unknown or ambiguous Faust parameter: gain/);
    assert.deepEqual(writes, []);
    assert.deepEqual(cleanup, ["disconnect", "destroy"]);
});

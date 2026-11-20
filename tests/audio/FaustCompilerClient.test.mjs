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
    assert.equal(await factory.create({}, "OrbitGraph", { p_frequency: 440 }, "process=0;"), node);
    assert.deepEqual(calls, [["/OrbitGraph/p_frequency", 440]]);
    factory.setParameters(node, { p_frequency: 330 });
    assert.deepEqual(calls.at(-1), ["/OrbitGraph/p_frequency", 330]);
    assert.throws(() => factory.setParameters(node, { p_frequency: 220, unknown: 1 }), /Unknown or ambiguous/);
    assert.equal(calls.length, 2);
});

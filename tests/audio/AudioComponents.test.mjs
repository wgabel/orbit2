// Tests the isolated timing, context, module cache and Faust node factory.
import test from "node:test";
import assert from "node:assert/strict";
import { AudioEnvelope } from "../../src/Orbit.Infrastructure/wwwroot/audio/Components/AudioEnvelope.js";
import { AudioContextManager } from "../../src/Orbit.Infrastructure/wwwroot/audio/Components/AudioContextManager.js";
import { FaustModuleLoader } from "../../src/Orbit.Infrastructure/wwwroot/audio/Faust/FaustModuleLoader.js";
import { FaustNodeFactory } from "../../src/Orbit.Infrastructure/wwwroot/audio/Faust/FaustNodeFactory.js";

test("envelope uses the audio clock and shortens fades for very short sounds", () => {
    const calls = [];
    const gain = {
        cancelScheduledValues: time => calls.push(["cancel", time]),
        setValueAtTime: (value, time) => calls.push(["set", value, time]),
        linearRampToValueAtTime: (value, time) => calls.push(["ramp", value, time])
    };
    const end = new AudioEnvelope().schedule(gain, 1, 0.009, 0.1);
    assert.equal(end, 1.009);
    assert.deepEqual(calls[0], ["cancel", 1]);
    assert.deepEqual(calls[1], ["set", 0, 1]);
    const times = calls.slice(1).map(call => call[2]);
    assert.ok(times.every((time, index) => index === 0 || time >= times[index - 1]));
    assert.deepEqual(calls.at(-1), ["ramp", 0, end]);
});

test("context manager uses one context and closes it", async () => {
    let created = 0;
    const context = {
        state: "suspended",
        async resume() { this.state = "running"; },
        async close() { this.state = "closed"; }
    };
    const manager = new AudioContextManager(() => { created++; return context; });
    assert.equal(await manager.getRunningContext(), context);
    assert.equal(await manager.getRunningContext(), context);
    assert.equal(created, 1);
    await manager.dispose();
    assert.equal(context.state, "closed");
});

test("Faust module loader shares pending compilation and retries failures", async () => {
    const origin = new URL("https://example.test/sine/");
    let requests = 0;
    let fail = true;
    const loader = new FaustModuleLoader(async url => {
        requests++;
        return {
            ok: !fail,
            arrayBuffer: async () => new ArrayBuffer(0),
            text: async () => "{}"
        };
    }, async () => "compiled");
    await assert.rejects(loader.load(origin), /Could not load/);
    fail = false;
    const first = loader.load(origin);
    const second = loader.load(origin);
    assert.equal(first, second);
    assert.deepEqual(await first, { module: "compiled", json: "{}" });
    assert.equal(requests, 4);
});

test("Faust node factory resolves parameter paths and uses AudioWorklet", async () => {
    const context = {};
    const applied = [];
    const node = {
        getParams: () => ["/Sine/frequency"],
        setParamValue: (path, value) => applied.push([path, value])
    };
    const loader = { load: async () => ({ module: "wasm", json: "{}" }) };
    const catalog = new Map([["Sine", { name: "OrbitSine", baseUrl: new URL("https://example.test/") }]]);
    const factory = new FaustNodeFactory(loader, catalog, () => ({
        createNode: async (actualContext, name, compiled, scriptProcessor) => {
            assert.equal(actualContext, context);
            assert.equal(name, "OrbitSine");
            assert.deepEqual(compiled, { module: "wasm", json: "{}", soundfiles: {} });
            assert.equal(scriptProcessor, false);
            return node;
        }
    }));
    assert.equal(await factory.create(context, "Sine", { frequency: 440 }), node);
    assert.deepEqual(applied, [["/Sine/frequency", 440]]);
});

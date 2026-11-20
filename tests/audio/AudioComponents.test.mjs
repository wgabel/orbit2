// Tests the isolated timing, context, module cache and Faust node factory.
import test from "node:test";
import assert from "node:assert/strict";
import { AudioEnvelope } from "../../src/Orbit.Infrastructure/wwwroot/audio/Components/AudioEnvelope.js";
import { AudioContextManager } from "../../src/Orbit.Infrastructure/wwwroot/audio/Components/AudioContextManager.js";
import { FaustModuleLoader } from "../../src/Orbit.Infrastructure/wwwroot/audio/Faust/FaustModuleLoader.js";
import { FaustNodeFactory } from "../../src/Orbit.Infrastructure/wwwroot/audio/Faust/FaustNodeFactory.js";

test("envelope fallback cancels scheduled values and releases from current gain", () => {
    const calls = [];
    const gain = {
        value: 0.07,
        cancelScheduledValues: time => calls.push(["cancel", time]),
        setValueAtTime: (value, time) => calls.push(["set", value, time]),
        linearRampToValueAtTime: (value, time) => calls.push(["ramp", value, time])
    };
    const end = new AudioEnvelope().release(gain, 1);
    assert.equal(end, 1.04);
    assert.deepEqual(calls, [["cancel", 1], ["set", 0.07, 1], ["ramp", 0, 1.04]]);
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
    const origin = new URL("https://example.test/modulated-sine/");
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
        getParams: () => ["/ModulatedSine/baseFrequency"],
        setParamValue: (path, value) => applied.push([path, value])
    };
    const loader = { load: async () => ({ module: "wasm", json: "{}" }) };
    const catalog = new Map([["ModulatedSine", { name: "OrbitModulatedSine", baseUrl: new URL("https://example.test/") }]]);
    const factory = new FaustNodeFactory(loader, catalog, () => ({
        createNode: async (actualContext, name, compiled, scriptProcessor) => {
            assert.equal(actualContext, context);
            assert.equal(name, "OrbitModulatedSine");
            assert.deepEqual(compiled, { module: "wasm", json: "{}", soundfiles: {} });
            assert.equal(scriptProcessor, false);
            return node;
        }
    }));
    assert.equal(await factory.create(context, "ModulatedSine", { baseFrequency: 440 }), node);
    assert.deepEqual(applied, [["/ModulatedSine/baseFrequency", 440]]);
});

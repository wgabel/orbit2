// Offline tests render the real compiled Faust WASM, not a JS approximation.
// CI builds these assets before executing the tests; no audio device is needed.
import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const generated = new URL("../../src/Orbit.Infrastructure/wwwroot/audio/generated/", import.meta.url);
const { FaustMonoDspGenerator } = await import(new URL("modulated-sine/faustwasm/index.js", generated));
const directory = new URL("modulated-sine/", generated);
const module = await WebAssembly.compile(await readFile(new URL("dsp-module.wasm", directory)));
const json = await readFile(new URL("dsp-meta.json", directory), "utf8");

async function createProcessor(sampleRate, settings) {
    const processor = await new FaustMonoDspGenerator().createOfflineProcessor(
        sampleRate, 128, { module, json, soundfiles: {} });
    assert.ok(processor);
    for (const [name, value] of Object.entries(settings)) {
        const path = processor.getParams().find(candidate => candidate.endsWith("/" + name));
        assert.ok(path, "Missing DSP parameter " + name);
        processor.setParamValue(path, value);
    }
    return processor;
}

// Estimate local frequency from positive zero crossings; interpolating the
// crossing positions gives a fractional sample estimate even at 44.1 kHz.
function estimateFrequency(samples, sampleRate, startSeconds, endSeconds) {
    let first;
    let last;
    let crossings = 0;
    const start = Math.max(1, Math.floor(startSeconds * sampleRate));
    const end = Math.min(samples.length, Math.floor(endSeconds * sampleRate));
    for (let index = start; index < end; index++) {
        if (samples[index - 1] <= 0 && samples[index] > 0) {
            const crossing = index - 1 + (-samples[index - 1]) /
                (samples[index] - samples[index - 1]);
            first ??= crossing;
            last = crossing;
            crossings++;
        }
    }
    assert.ok(crossings > 2, "Expected audible oscillation");
    return (crossings - 1) * sampleRate / (last - first);
}

function assertContinuous(samples, sampleRate) {
    let maximumStep = 0;
    for (let index = 0; index < samples.length; index++) {
        assert.ok(Number.isFinite(samples[index]));
        assert.ok(Math.abs(samples[index]) <= 1.001);
        if (index > 0)
            maximumStep = Math.max(maximumStep, Math.abs(samples[index] - samples[index - 1]));
    }
    // A 540 Hz sine should not make discontinuous jumps at these sample rates.
    assert.ok(maximumStep < 0.15, "Unexpected waveform step " + maximumStep + " at " + sampleRate);
}

for (const sampleRate of [44100, 48000]) {
    test("compiled Faust produces the expected vibrato at " + sampleRate + " Hz", async () => {
        const processor = await createProcessor(sampleRate, { baseFrequency: 440, lfoRate: 0.5, depthHz: 100 });
        const [samples] = processor.render([], sampleRate * 4);
        assert.equal(samples.length, sampleRate * 4);
        assertContinuous(samples, sampleRate);
        const frequencies = [];
        for (let start = 0.25; start <= 3.75; start += 0.1)
            frequencies.push(estimateFrequency(samples, sampleRate, start, start + 0.1));
        const minimum = Math.min(...frequencies);
        const maximum = Math.max(...frequencies);
        assert.ok(minimum > 320 && minimum < 370, "Unexpected minimum " + minimum);
        assert.ok(maximum > 510 && maximum < 560, "Unexpected maximum " + maximum);
    });

    test("zero depth leaves the base oscillator frequency unchanged at " + sampleRate + " Hz", async () => {
        const processor = await createProcessor(sampleRate, { baseFrequency: 440, lfoRate: 0.5, depthHz: 0 });
        const [samples] = processor.render([], sampleRate * 2);
        assertContinuous(samples, sampleRate);
        const frequency = estimateFrequency(samples, sampleRate, 0.5, 1.5);
        assert.ok(Math.abs(frequency - 440) < 1, "Unexpected frequency " + frequency);
    });
}

import test from "node:test";
import assert from "node:assert/strict";
import { measureLevels, toDecibels, scopeWindow, MinMaxHistory } from "../../src/Orbit.UI/wwwroot/js/scopeAnalysis.js";

for (const rate of [44100, 48000]) {
    test(`sine peak/RMS and trigger at ${rate} Hz`, () => {
        const samples = Float32Array.from({ length: rate }, (_, i) => Math.sin(2 * Math.PI * 100 * i / rate));
        const stats = measureLevels(samples);
        assert.ok(Math.abs(stats.peak - 1) < 0.0001);
        assert.ok(Math.abs(stats.rms - Math.SQRT1_2) < 0.0001);
        assert.ok(Math.abs(toDecibels(stats.rms) + 3.0103) < 0.0001);
        for (const trigger of ["rising", "falling"]) {
            const view = scopeWindow(samples, rate, 0.02, trigger);
            assert.equal(view.samples.length, rate * 0.02);
            assert.equal(view.triggered, true);
            assert.ok(Math.abs(view.samples[0]) < 0.02);
            assert.ok(trigger === "rising" ? view.samples[1] > view.samples[0] : view.samples[1] < view.samples[0]);
        }
        assert.equal(scopeWindow(samples, rate, 0.02, "free").triggered, false);
    });
}

test("clipping and over-range values are reported honestly; silence is negative infinity", () => {
    const clipped = measureLevels(Float32Array.of(-1, 1, -1, 1));
    assert.deepEqual(clipped, { peak: 1, rms: 1 });
    assert.equal(toDecibels(clipped.peak), 0);
    assert.ok(toDecibels(measureLevels(Float32Array.of(1.5)).peak) > 0);
    assert.equal(toDecibels(measureLevels(new Float32Array(12)).rms), -Infinity);
    assert.equal(scopeWindow(new Float32Array(100), 48000, 0.02, "rising").triggered, false);
});

test("slow history keeps true extrema, drops overlapping samples and leaves gaps", () => {
    const history = new MinMaxHistory();
    const collect = seconds => {
        const values = [];
        history.visit(seconds, (_, min, max, key) => values.push({ min, max, key }));
        return values;
    };
    // 1 kHz deterministic fixture: ten samples per bucket.
    const first = new Float32Array(20); first[2] = -1; first[3] = 0.75;
    history.append(first, 1000, 0.02);
    assert.deepEqual(collect(1), [{ min: -1, max: 0.75, key: 0 }, { min: 0, max: 0, key: 1 }]);
    const overlap = new Float32Array(20).fill(0.5); overlap[0] = 9;
    history.append(overlap, 1000, 0.03);
    assert.deepEqual(collect(1).at(-1), { min: 0.5, max: 0.5, key: 2 });
    history.append(new Float32Array(10).fill(-0.5), 1000, 0.06);
    assert.deepEqual(collect(1).map(bin => bin.key), [0, 1, 2, 5]);
    history.clear();
    assert.equal(collect(10).length, 0);
});

test("low-rate modulation retains peaks and memory stays bounded after wrap", () => {
    const history = new MinMaxHistory();
    const rate = 1000, length = rate * 102;
    const samples = Float32Array.from({ length }, (_, i) => Math.sin(2 * Math.PI * 0.5 * i / rate));
    for (let i = 0; i < length; i += 50) history.append(samples.subarray(i, i + 50), rate, (i + 50) / rate);
    const bins = [];
    history.visit(100, (_, min, max, key) => bins.push({ min, max, key }));
    assert.equal(bins.length, 10000);
    assert.equal(bins[0].key, 200);
    assert.equal(bins.at(-1).key, 10199);
    assert.ok(Math.min(...bins.map(bin => bin.min)) < -0.999);
    assert.ok(Math.max(...bins.map(bin => bin.max)) > 0.999);
});

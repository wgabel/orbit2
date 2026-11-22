import test from "node:test";
import assert from "node:assert/strict";
import { summarize, throughput, inspectOutput } from "../../scripts/benchmark-metrics.mjs";

test("summaries preserve repeats and use nearest-rank p95 and an actual median", () => {
    assert.deepEqual(summarize([5, 1, 3, 2]), { count: 4, min: 1, median: 2.5, p95: 5, max: 5, samples: [5, 1, 3, 2] });
    assert.throws(() => summarize([]));
    assert.throws(() => summarize([NaN]));
});
test("offline ratio and inverse have explicit units, with no CPU load inference", () => {
    assert.deepEqual(throughput(480000, 48000, 500), { audioSeconds: 10, offlineRealtimeFactor: .05, throughputTimesRealtime: 20, framesPerWallSecond: 960000 });
    assert.throws(() => throughput(480000, 48000, 0));
});
test("output validity checks bounds, active taps and inactive taps", () => {
    const master = Float32Array.of(.125, -.125);
    const tap = Float32Array.of(.5, -.5);
    const idle = new Float32Array(2);
    assert.deepEqual(inspectOutput([master, tap, idle], 2, 3, .55, 1), [{ peak: .125, rms: .125 }, { peak: .5, rms: .5 }, { peak: 0, rms: 0 }]);
    assert.throws(() => inspectOutput([master, idle], 2, 2, .55, 1));
    assert.throws(() => inspectOutput([master, tap], 2, 2, .55, 0));
    assert.throws(() => inspectOutput([idle], 2, 1, .55, 0));
    assert.throws(() => inspectOutput([Float32Array.of(1, -1)], 2, 1, .55, 0));
    assert.throws(() => inspectOutput([Float32Array.of(NaN, 0)], 2, 1, .55, 0));
    assert.throws(() => inspectOutput([master], 1, 1, .55, 0));
});

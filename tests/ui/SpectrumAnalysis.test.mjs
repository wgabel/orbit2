import test from "node:test";
import assert from "node:assert/strict";
import { SpectrumAnalyzer, spectrumFrequencyPosition, spectrumTicks } from "../../src/Orbit.UI/wwwroot/js/spectrumAnalysis.js";

const tone = (size, bin, amplitude = 1) =>
    Float32Array.from({ length: size }, (_, i) => amplitude * Math.sin(2 * Math.PI * bin * i / size));
const near = (actual, expected, tolerance = 1e-5) => assert.ok(Math.abs(actual - expected) <= tolerance,
    `Expected ${actual} within ${tolerance} of ${expected}`);

for (const rate of [44100, 48000]) {
    for (const window of ["hann", "rectangular"]) {
        test(`known fundamental and calibrated harmonic amplitudes at ${rate} Hz with ${window}`, () => {
            const size = 8192, fundamental = 75;
            const samples = Float32Array.from({ length: size }, (_, i) => {
                const phase = 2 * Math.PI * fundamental * i / size;
                return 0.8 * Math.sin(phase) + 0.2 * Math.sin(2 * phase) + 0.1 * Math.sin(3 * phase);
            });
            const input = samples.slice();
            const result = new SpectrumAnalyzer(size, window).analyze(samples, rate, 1);
            assert.equal(result.peakBin, fundamental);
            near(result.binWidth, rate / size);
            near(result.peakDecibels, 20 * Math.log10(0.8));
            near(result.decibels[2 * fundamental], 20 * Math.log10(0.2));
            near(result.decibels[3 * fundamental], -20);
            assert.deepEqual(samples, input);
        });
    }
    test(`off-bin 440 Hz peak is resolved within one bin at ${rate} Hz`, () => {
        const size = 8192;
        const samples = Float32Array.from({ length: size }, (_, i) => Math.sin(2 * Math.PI * 440 * i / rate));
        const result = new SpectrumAnalyzer(size).analyze(samples, rate, 1);
        near(result.peakBin * result.binWidth, 440, result.binWidth / 2);
        assert.ok(result.peakDecibels > -1.5 && result.peakDecibels <= 0.01);
    });
}

test("Hann window reduces distant off-bin spectral leakage", () => {
    const size = 4096;
    const samples = tone(size, 70.35);
    const hann = new SpectrumAnalyzer(size, "hann").analyze(samples, 48000, 1);
    const rectangular = new SpectrumAnalyzer(size, "rectangular").analyze(samples, 48000, 1);
    assert.ok(hann.decibels[90] < rectangular.decibels[90] - 30);
});

test("resolution is bounded and uses the latest complete window", () => {
    const samples = new Float32Array(8192);
    for (const size of SpectrumAnalyzer.Sizes) {
        samples.fill(0);
        samples.set(tone(size, 16), samples.length - size);
        const analyzer = new SpectrumAnalyzer(size);
        const frame = analyzer.analyze(samples, 48000, 1);
        assert.equal(frame.peakBin, 16);
        assert.equal(frame.decibels.length, size / 2 + 1);
        near(frame.peakDecibels, 0);
        const next = analyzer.analyze(samples, 48000, 2);
        assert.equal(next.decibels, frame.decibels);
        assert.equal(analyzer.analyze(samples, 48000, 2), next);
    }
    assert.throws(() => new SpectrumAnalyzer(16384), RangeError);
    assert.throws(() => new SpectrumAnalyzer(1024).analyze(new Float32Array(512), 48000, 1), RangeError);
});

test("DC removal, Nyquist amplitude, silence and over-range levels", () => {
    const size = 1024, analyzer = new SpectrumAnalyzer(size, "rectangular");
    const dc = new SpectrumAnalyzer(size).analyze(new Float32Array(size).fill(0.5), 48000, 1);
    assert.equal(dc.mean, 0.5);
    assert.equal(dc.peakBin, 0);
    assert.equal(dc.peakDecibels, -Infinity);
    const nyquist = analyzer.analyze(Float32Array.from({ length: size }, (_, i) => i % 2 ? -1 : 1), 48000, 1);
    assert.equal(nyquist.peakBin, size / 2);
    near(nyquist.peakDecibels, 0);
    near(analyzer.analyze(tone(size, 32, 2), 48000, 2).peakDecibels, 20 * Math.log10(2));
    assert.equal(analyzer.analyze(new Float32Array(size), 48000, 3).peakDecibels, -Infinity);
});

test("power smoothing uses elapsed audio time and resets on new sessions or rate", () => {
    const size = 1024, analyzer = new SpectrumAnalyzer(size, "hann", 0.15);
    const samples = tone(size, 32, 0.5), silence = new Float32Array(size);
    near(analyzer.analyze(samples, 48000, 1).peakDecibels, 20 * Math.log10(0.5));
    const smoothed = analyzer.analyze(silence, 48000, 1.15);
    near(smoothed.peakDecibels, 20 * Math.log10(0.5) - 10 / Math.LN10);
    assert.equal(analyzer.analyze(silence, 44100, 1.2).peakDecibels, -Infinity);
    analyzer.analyze(samples, 44100, 1.3);
    analyzer.reset();
    assert.equal(analyzer.analyze(silence, 44100, 1.4).peakDecibels, -Infinity);
});

test("deterministic white noise distributes energy across frequency bands", () => {
    const size = 8192;
    let seed = 123456789;
    const samples = Float32Array.from({ length: size }, () => {
        seed ^= seed << 13; seed ^= seed >>> 17; seed ^= seed << 5;
        return (seed >>> 0) / 2147483648 - 1;
    });
    const result = new SpectrumAnalyzer(size).analyze(samples, 48000, 1);
    const powers = [];
    for (let band = 0; band < 4; band++) {
        let sum = 0;
        for (let bin = 1 + band * 1024; bin <= (band + 1) * 1024; bin++)
            sum += 10 ** (result.decibels[bin] / 10);
        powers.push(sum / 1024);
    }
    assert.ok(Math.max(...powers) / Math.min(...powers) < 1.4);
    assert.ok(result.peakDecibels < -20);
});

test("frequency axis uses logarithmic spacing, including low-frequency bins", () => {
    near(spectrumFrequencyPosition(10, 10, 10000), 0);
    near(spectrumFrequencyPosition(100, 10, 10000), 1 / 3);
    near(spectrumFrequencyPosition(1000, 10, 10000), 2 / 3);
    near(spectrumFrequencyPosition(10000, 10, 10000), 1);
    assert.deepEqual(spectrumTicks(5.86, 24000),
        [10, 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000]);
});

test("Hann DC removal does not invent sub-bass for off-bin audio tones", () => {
    for (const rate of [44100, 48000]) {
        for (const size of SpectrumAnalyzer.Sizes) {
            const frequencies = size === 8192 ? [220, 440, 1000] : [70.35 * rate / size];
            for (const frequency of frequencies) {
                for (const phase of [0, Math.PI / 3, Math.PI / 2, 3 * Math.PI / 2]) {
                    const samples = Float32Array.from({ length: size }, (_, i) =>
                        Math.sin(2 * Math.PI * frequency * i / rate + phase));
                    const result = new SpectrumAnalyzer(size, "hann").analyze(samples, rate, 1);
                    const limit = frequency === 440 ? -110 : -90;
                    assert.ok(result.decibels[1] < limit,
                        `Unexpected first bin ${result.decibels[1]} dBFS at ${frequency} Hz, ${rate} Hz, size ${size}, phase ${phase}`);
                    near(result.peakBin * result.binWidth, frequency, result.binWidth / 2);
                    assert.ok(result.peakDecibels > -1.5 && result.peakDecibels <= 0.01);
                }
            }
        }
    }
});

test("DC removal retains genuine positive low-frequency content and rejects constant signals", () => {
    const size = 8192, rate = 48000;
    for (const window of ["hann", "rectangular"]) {
        const analyzer = new SpectrumAnalyzer(size, window);
        const constant = new Float32Array(size).fill(0.3);
        const dc = analyzer.analyze(constant, rate, 1);
        near(dc.mean, constant[0]);
        assert.equal(dc.peakBin, 0);
        assert.equal(dc.peakDecibels, -Infinity);

        // A real 11.72 Hz tone is below 20 Hz and must remain measurable.
        const low = Float32Array.from({ length: size }, (_, i) =>
            0.5 * Math.sin(2 * Math.PI * 2 * i / size + Math.PI / 3));
        const lowResult = analyzer.analyze(low, rate, 2);
        assert.equal(lowResult.peakBin, 2);
        near(lowResult.peakDecibels, 20 * Math.log10(0.5));
        const shifted = Float32Array.from(low, sample => sample + 0.3);
        const shiftedResult = analyzer.analyze(shifted, rate, 3);
        assert.equal(shiftedResult.peakBin, 2);
        near(shiftedResult.peakDecibels, 20 * Math.log10(0.5));
        near(shiftedResult.mean, 0.3);

        // Do not hide the first positive bin: a real tone there must survive.
        const first = tone(size, 1, 0.5);
        const firstResult = analyzer.analyze(first, rate, 4);
        assert.equal(firstResult.peakBin, 1);
        near(firstResult.peakDecibels, 20 * Math.log10(0.5));
    }
});

test("Hann weighted detrending keeps the reported arithmetic sample mean", () => {
    const size = 8192, rate = 48000;
    const samples = Float32Array.from({ length: size }, (_, i) =>
        Math.sin(2 * Math.PI * 440 * i / rate + Math.PI / 2));
    const arithmeticMean = samples.reduce((sum, sample) => sum + sample, 0) / size;
    const result = new SpectrumAnalyzer(size, "hann").analyze(samples, rate, 1);
    near(result.mean, arithmeticMean, 1e-12);
    assert.ok(Math.abs(result.mean) > 0.001);
});

// Headless integration test: actual browser worker, actual generated Faust and AudioWorklet.
// The runner supplies a pinned Playwright install and Chrome executable.
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { resolve, extname, sep } from "node:path";
import { pathToFileURL } from "node:url";
import assert from "node:assert/strict";

const directory = process.env.ORBIT_GRAPH_PROBE_DIR;
const playwrightDirectory = process.env.ORBIT_PLAYWRIGHT_DIR;
assert.ok(directory && playwrightDirectory && process.env.ORBIT_CHROME);
const { chromium } = await import(pathToFileURL(resolve(playwrightDirectory, "index.mjs")));
const source = await readFile(resolve(directory, "program.dsp"), "utf8");
const manifest = JSON.parse(await readFile(resolve(directory, "controls.json"), "utf8"));
const tempoSource = await readFile(resolve(directory, "tempo.dsp"), "utf8");
const tempoControls = JSON.parse(await readFile(resolve(directory, "tempo-controls.json"), "utf8"));
const waveforms = JSON.parse(await readFile(resolve(directory, "waveforms.json"), "utf8"));
const noises = JSON.parse(await readFile(resolve(directory, "noise.json"), "utf8"));
const layered = JSON.parse(await readFile(resolve(directory, "layered.json"), "utf8"));
const root = resolve("src/Orbit.Infrastructure/wwwroot/audio");
const server = createServer(async (request, response) => {
    try {
        const path = new URL(request.url, "http://localhost").pathname;
        if (path === "/") {
            response.writeHead(200, { "Content-Type": "text/html" });
            response.end("<!doctype html><title>Orbit compiler smoke</title>");
            return;
        }
        const file = resolve(root, "." + path);
        if (!file.startsWith(root + sep)) throw new Error("Outside static assets");
        const bytes = await readFile(file);
        response.writeHead(200, { "Content-Type": extname(file) === ".js" ? "text/javascript" : "application/octet-stream" });
        response.end(bytes);
    } catch (error) { response.writeHead(404); response.end(String(error)); }
});
await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
let browser;
try {
    browser = await chromium.launch({ executablePath: process.env.ORBIT_CHROME, headless: true,
        args: ["--no-sandbox", "--autoplay-policy=no-user-gesture-required", "--mute-audio"] });
    const page = await browser.newPage();
    page.on("console", message => console.log("browser", message.type(), message.text()));
    page.on("pageerror", error => console.log("browser page error", error.message));
    await page.goto("http://127.0.0.1:" + server.address().port);
    const result = await page.evaluate(async ({ source, manifest }) => {
        const { FaustCompilerClient } = await import("/Faust/FaustCompilerClient.js");
        const { FaustNodeFactory } = await import("/Faust/FaustNodeFactory.js");
        const { FaustOutputMonitor } = await import("/Faust/FaustOutputMonitor.js");
        const { FaustMonoDspGenerator } = await import("/generated/modulated-sine/faustwasm/index.js");
        const { AudioEngine } = await import("/AudioEngine.js");
        const { AudioVoice } = await import("/Components/AudioVoice.js");
        const { AudioEnvelope } = await import("/Components/AudioEnvelope.js");
        const compiler = new FaustCompilerClient(() => new Worker("/Faust/FaustCompilerWorker.js", { type: "module" }));
        const context = new AudioContext();
        try {
            await context.resume();
            const factory = new FaustNodeFactory(null, new Map(), () => new FaustMonoDspGenerator(), compiler);
            const node = await factory.create(context, "OrbitGraph", manifest.values, source);
            const monitor = new FaustOutputMonitor(node);
            const analyser = context.createAnalyser();
            analyser.fftSize = 2048;
            node.connect(analyser);
            analyser.connect(context.destination);
            const samples = new Float32Array(analyser.fftSize);
            // Wait for the worklet to produce audible samples, not a fixed startup delay.
            async function waitForPeak(predicate) {
                const end = performance.now() + 5000;
                let peak = 0;
                do {
                    await new Promise(resolve => setTimeout(resolve, 100));
                    analyser.getFloatTimeDomainData(samples);
                    peak = Math.max(...samples.map(Math.abs));
                } while (!predicate(peak) && performance.now() < end);
                return peak;
            }
            const peak = await waitForPeak(value => value > 0.035);
            const readings = [];
            for (let i = 0; i < 24; i++) {
                await new Promise(resolve => setTimeout(resolve, 100));
                const entry = Object.entries(monitor.getSnapshot()).find(([path]) => path.endsWith("/" + manifest.frequencyMeter));
                if (entry) readings.push(entry[1]);
            }
            const meterMinimum = Math.min(...readings), meterMaximum = Math.max(...readings);
            // The same connected worklet must respond to live base edits.
            // Mute the carrier amplitude and then restore it on the same node.
            factory.setParameters(node, { [manifest.amplitude]: 0 });
            const mutedPeak = await waitForPeak(value => value < 0.001);
            factory.setParameters(node, { [manifest.amplitude]: manifest.values[manifest.amplitude] });
            const restoredPeak = await waitForPeak(value => value > 0.035);
            monitor.dispose();
            node.disconnect();
            node.destroy();
            // Use the production engine, real Faust worklets and voice envelopes,
            // with the analyser as a test output rather than the speaker.
            const envelope = new AudioEnvelope();
            const output = { createVoice: (context, source) => {
                const gain = context.createGain();
                gain.gain.value = 0;
                source.connect(gain);
                gain.connect(analyser);
                return new AudioVoice(context, source, gain, envelope);
            }};
            const engine = new AudioEngine({ getRunningContext: async () => context },
                factory, output, () => ({ dispose() {}, getSnapshot: () => ({}) }));
            const request = { soundId: "swap-probe", processor: "OrbitGraph",
                volume: 1, parameters: manifest.values, source };
            const playing = engine.play(request);
            let beforeSwap, silentSwap, restoredSwap;
            try {
                beforeSwap = await waitForPeak(value => value > 0.035);
                const silent = { ...request, parameters: {}, source: "process = 0.0;" };
                if (!await engine.replace(silent, 2)) throw new Error("Replacement was not accepted");
                silentSwap = await waitForPeak(value => value < 0.001);
                if (!await engine.replace(request, 3)) throw new Error("Restoration was not accepted");
                restoredSwap = await waitForPeak(value => value > 0.035);
                if (engine.getPlaybackState(request.soundId) !== "playing") throw new Error("Playback ended during swap");
            } finally { engine.stop(request.soundId); await playing; }
            return { peak, mutedPeak, restoredPeak, beforeSwap, silentSwap, restoredSwap, meterMinimum, meterMaximum, sampleRate: context.sampleRate };
        } finally { compiler.dispose(); await context.close(); }
    }, { source, manifest });
    console.log("Generated graph produced real worklet audio:", result);
    assert.ok(result.peak > 0.035 && result.peak <= 0.151);
    assert.ok(result.meterMinimum >= 339 && result.meterMinimum < 360, "Incorrect live frequency minimum");
    assert.ok(result.meterMaximum > 520 && result.meterMaximum <= 541, "Incorrect live frequency maximum");
    assert.ok(result.mutedPeak < 0.001, "Live values did not mute the existing worklet");
    assert.ok(result.restoredPeak > 0.035 && result.restoredPeak <= 0.151, "Live values did not restore audio");
    assert.ok(result.beforeSwap > 0.035 && result.beforeSwap <= 0.151, "The initial voice was not audible");
    assert.ok(result.silentSwap < 0.001, "The silent replacement did not take over");
    assert.ok(result.restoredSwap > 0.035 && result.restoredSwap <= 0.151, "The restored worklet was not audible");

    // Exercise each generated shape at both audio and LFO rates. In particular,
    // a slow triangle must really cycle at 2 Hz rather than an audio-rate minimum.
    const renderedWaveforms = await page.evaluate(async waveforms => {
        const { FaustCompilerClient } = await import("/Faust/FaustCompilerClient.js");
        const { FaustMonoDspGenerator } = await import("/generated/modulated-sine/faustwasm/index.js");
        const compiler = new FaustCompilerClient(() => new Worker("/Faust/FaustCompilerWorker.js", { type: "module" }));
        const results = [];
        try {
            for (const wave of waveforms.filter(wave => wave.timing === "Free")) {
                const compiled = await compiler.compile(wave.source);
                for (const rate of [44100, 48000]) {
                    const render = async (frequency, phase = 0) => {
                        const processor = await new FaustMonoDspGenerator().createOfflineProcessor(
                            rate, 128, { ...compiled, soundfiles: {} });
                        const values = { ...wave.values, [wave.rateControl]: frequency, [wave.phase]: phase };
                        for (const [name, value] of Object.entries(values)) {
                            const path = processor.getParams().find(path => path.endsWith("/" + name));
                            if (!path) throw new Error("Missing waveform control " + name);
                            processor.setParamValue(path, value);
                        }
                        return processor.render([], rate * 3)[0].subarray(rate);
                    };
                    for (const frequency of [2, 440]) {
                        const samples = await render(frequency);
                        let peak = 0, squares = 0, sum = 0, previousCrossing, cycleSum = 0, cycles = 0;
                        for (let i = 0; i < samples.length; i++) {
                            const sample = samples[i];
                            if (!Number.isFinite(sample)) throw new Error("Non-finite oscillator sample");
                            peak = Math.max(peak, Math.abs(sample));
                            squares += sample * sample;
                            sum += sample;
                            if (i && samples[i - 1] <= 0 && sample > 0) {
                                if (previousCrossing !== undefined) {
                                    cycleSum += i - previousCrossing;
                                    cycles++;
                                }
                                previousCrossing = i;
                            }
                        }
                        results.push({ waveform: wave.waveform, rate, frequency, peak,
                            mean: sum / samples.length, rms: Math.sqrt(squares / samples.length),
                            measured: rate * cycles / cycleSum });
                    }
                    // 90-degree phase shift must be a quarter-cycle shift of the same shape.
                    const unshifted = await render(100);
                    const shifted = await render(100, 90);
                    const quarter = Math.round(rate / 400);
                    let error = 0;
                    for (let i = 0; i < shifted.length - quarter; i++)
                        error += (shifted[i] - unshifted[i + quarter]) ** 2;
                    results.push({ waveform: wave.waveform, rate, phaseError: Math.sqrt(error / (shifted.length - quarter)) });
                    if (wave.waveform === "Square" || wave.waveform === "Sawtooth") {
                        // The fifth harmonic of 7 kHz folds back below Nyquist.
                        // Measure that alias against an ideal discontinuous waveform.
                        const samples = (await render(7000)).subarray(0, rate);
                        const alias = rate - 35000;
                        let cosine = 0, sine = 0;
                        for (let i = 0; i < samples.length; i++) {
                            const angle = 2 * Math.PI * alias * i / rate;
                            cosine += samples[i] * Math.cos(angle);
                            sine += samples[i] * Math.sin(angle);
                        }
                        results.push({ waveform: wave.waveform, rate, aliasLevel: 2 * Math.hypot(cosine, sine) / samples.length });
                    }
                }
            }
            return results;
        } finally { compiler.dispose(); }
    }, waveforms);
    for (const result of renderedWaveforms) {
        console.log("Oscillator waveform", result);
        if ("phaseError" in result) {
            // At 44.1k, a quarter period is not an integer sample. Permit the
            // resulting sub-sample error, especially for square/saw edges.
            assert.ok(result.phaseError < 0.09, "Incorrect phase offset");
        } else if ("aliasLevel" in result) {
            const ideal = (result.waveform === "Square" ? 4 : 2) / (5 * Math.PI);
            assert.ok(result.aliasLevel < ideal * 0.6, "Fifth harmonic alias was not attenuated");
        } else {
            assert.ok(result.peak > 0.85 && result.peak <= 1.001, "Incorrect amplitude");
            assert.ok(Math.abs(result.mean) < 0.03, "Unexpected DC offset");
            assert.ok(Math.abs(result.measured - result.frequency) / result.frequency < 0.015, "Wrong cycle frequency");
            const expectedRms = { Sine: Math.SQRT1_2, Triangle: Math.sqrt(1 / 3), Square: 1, Sawtooth: Math.sqrt(1 / 3) }[result.waveform];
            assert.ok(Math.abs(result.rms - expectedRms) < 0.07, "Incorrect waveform shape");
        }
    }

    const noiseResults = await page.evaluate(async noises => {
        const { FaustCompilerClient } = await import("/Faust/FaustCompilerClient.js");
        const { FaustMonoDspGenerator } = await import("/generated/modulated-sine/faustwasm/index.js");
        const compiler = new FaustCompilerClient(() => new Worker("/Faust/FaustCompilerWorker.js", { type: "module" }));
        const results = [];
        try {
            for (const noise of noises) {
                const compiled = await compiler.compile(noise.source);
                for (const rate of [44100, 48000]) {
                    const processor = await new FaustMonoDspGenerator().createOfflineProcessor(rate, 128, { ...compiled, soundfiles: {} });
                    const path = processor.getParams().find(path => path.endsWith("/" + noise.amplitude));
                    processor.setParamValue(path, 0.5);
                    const [samples] = processor.render([], rate * 3);
                    let power = 0, differencePower = 0, peak = 0;
                    for (let i = rate; i < samples.length; i++) {
                        if (!Number.isFinite(samples[i])) throw new Error("Non-finite noise sample");
                        power += samples[i] ** 2;
                        differencePower += (samples[i] - samples[i - 1]) ** 2;
                        peak = Math.max(peak, Math.abs(samples[i]));
                    }
                    processor.setParamValue(path, 0);
                    const [muted] = processor.render([], rate);
                    results.push({ color: noise.color, rate, peak, rms: Math.sqrt(power / (2 * rate)),
                        roughness: differencePower / power, mutedPeak: Math.max(...muted.slice(rate / 2)) });
                }
            }
            return results;
        } finally { compiler.dispose(); }
    }, noises);
    for (const rate of [44100, 48000]) {
        const [white, pink, brown] = ["White", "Pink", "Brown"].map(color => noiseResults.find(result => result.rate === rate && result.color === color));
        for (const result of [white, pink, brown]) {
            console.log("Noise spectrum", result);
            assert.ok(result.peak <= 0.501 && result.rms > 0.015);
            assert.ok(result.mutedPeak < 0.001);
        }
        assert.ok(white.roughness > pink.roughness && pink.roughness > brown.roughness,
            "Noise colors must progressively emphasize lower frequencies");
    }

    const layeredResults = await page.evaluate(async layered => {
        const { FaustCompilerClient } = await import("/Faust/FaustCompilerClient.js");
        const { FaustMonoDspGenerator } = await import("/generated/modulated-sine/faustwasm/index.js");
        const compiler = new FaustCompilerClient(() => new Worker("/Faust/FaustCompilerWorker.js", { type: "module" }));
        try {
            const compiled = await compiler.compile(layered.source);
            const results = [];
            for (const rate of [44100, 48000]) {
                const processor = await new FaustMonoDspGenerator().createOfflineProcessor(rate, 128, { ...compiled, soundfiles: {} });
                for (const [name, value] of Object.entries(layered.values))
                    processor.setParamValue(processor.getParams().find(path => path.endsWith("/" + name)), value);
                const [audio] = processor.render([], rate * 3);
                let peak = 0;
                for (const sample of audio) {
                    if (!Number.isFinite(sample)) throw new Error("Non-finite layered drone sample");
                    peak = Math.max(peak, Math.abs(sample));
                }
                const tones = [110, 165, 220.7].map(frequency => {
                    let real = 0, imaginary = 0;
                    for (let i = rate; i < audio.length; i++) {
                        const angle = 2 * Math.PI * frequency * i / rate;
                        real += audio[i] * Math.cos(angle);
                        imaginary += audio[i] * Math.sin(angle);
                    }
                    return Math.hypot(real, imaginary) / rate;
                });
                results.push({ rate, peak, tones });
            }
            return results;
        } finally { compiler.dispose(); }
    }, layered);
    for (const result of layeredResults) {
        console.log("Layered drone", result);
        assert.ok(result.peak <= 0.425 && result.peak > 0.1);
        assert.ok(result.tones.every(tone => tone > 0.06), "All three pitched voices must remain audible");
    }

    // All shapes now use the transport in synchronized mode. Supply the same
    // beat stream as the worklet, including a wrap and a phase-continuous tempo edit.
    const lockedWaveforms = await page.evaluate(async waveforms => {
        const { FaustCompilerClient } = await import("/Faust/FaustCompilerClient.js");
        const { FaustMonoDspGenerator } = await import("/generated/modulated-sine/faustwasm/index.js");
        const { beatAtFrame } = await import("/Components/TransportPhase.js");
        const compiler = new FaustCompilerClient(() => new Worker("/Faust/FaustCompilerWorker.js", { type: "module" }));
        const results = [];
        const wrap = value => value - Math.floor(value);
        try {
            for (const wave of waveforms.filter(wave => wave.timing === "Synchronized")) {
                const compiled = await compiler.compile(wave.source);
                for (const rate of [44100, 48000]) {
                    const processor = await new FaustMonoDspGenerator().createOfflineProcessor(
                        rate, 128, { ...compiled, soundfiles: {} });
                    // Dotted quarter = 1.5 beats; test phase with a 90-degree offset.
                    for (const [name, value] of Object.entries({ ...wave.values,
                        [wave.rateControl]: 6, [wave.phase]: 90 })) {
                        const path = processor.getParams().find(path => path.endsWith("/" + name));
                        if (!path) throw new Error("Missing synchronized control " + name);
                        processor.setParamValue(path, value);
                    }
                    const beat = new Float32Array(rate * 2);
                    const first = { beat: 11.25, frame: 0, beatsPerMinute: 120, isPlaying: true };
                    const second = { beat: 13.25, frame: rate, beatsPerMinute: 90, isPlaying: true };
                    for (let i = 0; i < beat.length; i++)
                        beat[i] = beatAtFrame(i < rate ? first : second, i, rate);
                    const [audio] = processor.render([beat], beat.length);
                    let error = 0, count = 0;
                    for (let i = Math.floor(rate * 0.2); i < audio.length; i++) {
                        const p = wrap(beat[i] / 1.5 + 0.25);
                        const expected = wave.waveform === "Sine" ? Math.sin(2 * Math.PI * p) :
                            wave.waveform === "Triangle" ? 1 - 4 * Math.abs(wrap(p + 0.25) - 0.5) :
                            wave.waveform === "Square" ? (p < 0.5 ? 1 : -1) : 2 * p - 1;
                        if (!Number.isFinite(audio[i])) throw new Error("Non-finite synchronized sample");
                        error += (expected - audio[i]) ** 2;
                        count++;
                    }
                    results.push({ waveform: wave.waveform, rate, rms: Math.sqrt(error / count) });
                }
            }
            return results;
        } finally { compiler.dispose(); }
    }, waveforms);
    assert.equal(lockedWaveforms.length, 8);
    for (const result of lockedWaveforms) {
        console.log("Tempo-locked waveform", result);
        assert.ok(result.rms < 0.035, "Waveform does not follow the shared transport");
    }

    // Render two independent Faust worklets receiving the same transport.
    // Each graph itself combines an outer and inner LFO. Compare every sample
    // after the amplitude control settles, including a 12-beat wrap.
    for (const sampleRate of [44100, 48000]) {
        const synchronized = await page.evaluate(async ({ tempoSource, tempoControls, sampleRate }) => {
            const { FaustCompilerClient } = await import("/Faust/FaustCompilerClient.js");
            const { FaustNodeFactory } = await import("/Faust/FaustNodeFactory.js");
            const { FaustMonoDspGenerator } = await import("/generated/modulated-sine/faustwasm/index.js");
            const { TransportSignalSource } = await import("/Components/TransportSignalSource.js");
            const compiler = new FaustCompilerClient(() => new Worker("/Faust/FaustCompilerWorker.js", { type: "module" }));
            const transport = new TransportSignalSource();
            const context = new OfflineAudioContext(1, sampleRate, sampleRate);
            try {
                const factory = new FaustNodeFactory(null, new Map(), () => new FaustMonoDspGenerator(), compiler);
                transport.setSnapshot({ beatsPerMinute: 120, beatPosition: 11.25, isPlaying: true,
                    clock: { currentSample: 0, sampleRate } });
                const first = await factory.create(context, "OrbitTempo", tempoControls, tempoSource);
                const second = await factory.create(context, "OrbitTempo", tempoControls, tempoSource);
                await transport.connect(context, first);
                await transport.connect(context, second);
                const gain = context.createGain();
                gain.gain.value = 0.5;
                first.connect(gain); second.connect(gain); gain.connect(context.destination);
                const rendered = (await context.startRendering()).getChannelData(0);
                let error = 0, count = 0, peak = 0;
                for (let frame = Math.floor(sampleRate * 0.2); frame < rendered.length; frame++) {
                    const expected = Math.sin(2 * Math.PI * (11.25 + frame * 2 / sampleRate));
                    error += (expected - rendered[frame]) ** 2;
                    peak = Math.max(peak, Math.abs(rendered[frame]));
                    count++;
                }
                return { rms: Math.sqrt(error / count), peak, inputs: first.numberOfInputs };
            } finally { transport.dispose(); compiler.dispose(); }
        }, { tempoSource, tempoControls, sampleRate });
        console.log("Shared transport offline render", sampleRate, synchronized);
        assert.equal(synchronized.inputs, 1);
        assert.ok(synchronized.peak > 0.95 && synchronized.rms < 0.035, "Synchronized phase differs from sample-clock oracle");
    }
} finally {
    await browser?.close();
    server.close();
}

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
const filters = JSON.parse(await readFile(resolve(directory, "filters.json"), "utf8"));
const eq = JSON.parse(await readFile(resolve(directory, "eq.json"), "utf8"));
const delays = JSON.parse(await readFile(resolve(directory, "delay.json"), "utf8"));
const reverb = JSON.parse(await readFile(resolve(directory, "reverb.json"), "utf8"));
const chorus = JSON.parse(await readFile(resolve(directory, "chorus.json"), "utf8"));
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

    const filterResults = await page.evaluate(async filters => {
        const { FaustCompilerClient } = await import("/Faust/FaustCompilerClient.js");
        const { FaustMonoDspGenerator } = await import("/generated/modulated-sine/faustwasm/index.js");
        const compiler = new FaustCompilerClient(() => new Worker("/Faust/FaustCompilerWorker.js", { type: "module" }));
        const results = [];
        try {
            for (const filter of filters) {
                const compiled = await compiler.compile(filter.source);
                for (const rate of [44100, 48000]) {
                    const processor = await new FaustMonoDspGenerator().createOfflineProcessor(rate, 128, { ...compiled, soundfiles: {} });
                    const set = (name, value) => processor.setParamValue(processor.getParams().find(path => path.endsWith("/" + name)), value);
                    for (const [name, value] of Object.entries(filter.values)) set(name, value);
                    const response = frequency => {
                        set(filter.frequency, frequency);
                        const [audio] = processor.render([], rate);
                        let power = 0;
                        for (let i = rate / 2; i < audio.length; i++) {
                            if (!Number.isFinite(audio[i])) throw new Error("Non-finite filter output");
                            power += audio[i] ** 2;
                        }
                        return Math.sqrt(power / (rate / 2)) / (0.1 / Math.SQRT2);
                    };
                    const low = response(100), center = response(1000), high = response(8000);
                    set(filter.resonance, 4);
                    const resonant = response(1000);
                    set(filter.modulation, 1);
                    const [stress] = processor.render([], rate * 2);
                    let stressPeak = 0;
                    for (const sample of stress) {
                        if (!Number.isFinite(sample)) throw new Error("Unstable audio-rate modulated filter");
                        stressPeak = Math.max(stressPeak, Math.abs(sample));
                    }
                    results.push({ mode: filter.mode, rate, low, center, high, resonant, stressPeak });
                }
            }
            return results;
        } finally { compiler.dispose(); }
    }, filters);
    for (const r of filterResults) {
        console.log("Filter response", r);
        assert.ok(r.stressPeak < 5, "Extreme modulation must remain finite and bounded in the probe");
        if (r.mode === "LowPass") assert.ok(r.low > 0.95 && r.high < 0.04 && r.resonant > 3.8);
        if (r.mode === "HighPass") assert.ok(r.low < 0.04 && r.high > 0.95 && r.resonant > 3.8);
        if (r.mode === "BandPass") assert.ok(r.low < 0.15 && r.high < 0.15 && r.center > 0.65 && r.resonant > 3.8);
        if (r.mode === "Notch") assert.ok(r.low > 0.95 && r.high > 0.95 && r.center < 0.03);
    }

    const eqResults = await page.evaluate(async eq => {
        const { FaustCompilerClient } = await import("/Faust/FaustCompilerClient.js");
        const { FaustMonoDspGenerator } = await import("/generated/modulated-sine/faustwasm/index.js");
        const compiler = new FaustCompilerClient(() => new Worker("/Faust/FaustCompilerWorker.js", { type: "module" }));
        try {
            const compiled = await compiler.compile(eq.source);
            const results = [];
            for (const rate of [44100, 48000]) {
                const processor = await new FaustMonoDspGenerator().createOfflineProcessor(rate, 128, { ...compiled, soundfiles: {} });
                const set = (name, value) => processor.setParamValue(processor.getParams().find(path => path.endsWith("/" + name)), value);
                for (const [name, value] of Object.entries(eq.values)) set(name, value);
                const response = frequency => {
                    set(eq.carrier, frequency);
                    const [audio] = processor.render([], rate);
                    let power = 0;
                    for (let i = rate / 2; i < audio.length; i++) power += audio[i] ** 2;
                    return 20 * Math.log10(Math.sqrt(power / (rate / 2)) / (0.1 / Math.SQRT2));
                };
                const flat = response(1000);
                set(eq.gain, 12);
                const boost = response(1000), low = response(20), high = response(12000);
                set(eq.q, 0.5);
                const broad = response(1500);
                set(eq.q, 8);
                const narrow = response(1500);
                set(eq.gain, -12);
                const cut = response(1000);
                set(eq.frequency, 2000);
                const moved = response(2000);
                set(eq.gain, 12);
                set(eq.modulation, 1);
                const [stress] = processor.render([], rate * 2);
                let peak = 0;
                for (const sample of stress) {
                    if (!Number.isFinite(sample)) throw new Error("Non-finite modulated EQ output");
                    peak = Math.max(peak, Math.abs(sample));
                }
                results.push({ rate, flat, boost, low, high, broad, narrow, cut, moved, peak });
            }
            return results;
        } finally { compiler.dispose(); }
    }, eq);
    for (const result of eqResults) {
        console.log("Parametric EQ response", result);
        assert.ok(Math.abs(result.flat) < 0.1);
        assert.ok(Math.abs(result.boost - 12) < 0.2 && Math.abs(result.cut + 12) < 0.2);
        assert.ok(Math.abs(result.moved + 12) < 0.2);
        assert.ok(Math.abs(result.low) < 0.2 && Math.abs(result.high) < 0.2);
        assert.ok(result.broad > result.narrow + 3);
        assert.ok(result.peak < 10);
    }

    const delayResults = await page.evaluate(async delays => {
        const { FaustCompilerClient } = await import("/Faust/FaustCompilerClient.js");
        const { FaustMonoDspGenerator } = await import("/generated/modulated-sine/faustwasm/index.js");
        const { TransportSignalSource } = await import("/Components/TransportSignalSource.js");
        const compiler = new FaustCompilerClient(() => new Worker("/Faust/FaustCompilerWorker.js", { type: "module" }));
        const results = [];
        try {
            for (const delay of delays) {
                const compiled = await compiler.compile(delay.source);
                for (const rate of [44100, 48000]) {
                    for (const bpm of delay.mode === "Synchronized" ? [120, 240] : [120]) {
                        const processor = await new FaustMonoDspGenerator().createOfflineProcessor(rate, 128, { ...compiled, soundfiles: {} });
                        const set = (name, value) => processor.setParamValue(processor.getParams().find(path => path.endsWith("/" + name)), value);
                        for (const [name, value] of Object.entries(delay.values)) set(name, value);
                        if (delay.mode === "Synchronized") set("orbit_tempo", bpm);
                        const duration = delay.mode === "Synchronized" ? 60 / bpm : 0.25;
                        // Settle all smoothed controls on silence, then start the carrier.
                        processor.render([], rate);
                        set(delay.amplitude, 0.1);
                        const [audio] = processor.render([], Math.ceil(rate * duration * 3));
                        const rms = (from, to) => {
                            let sum = 0;
                            const start = Math.floor(from * duration * rate), end = Math.floor(to * duration * rate);
                            for (let i = start; i < end; i++) sum += audio[i] ** 2;
                            return Math.sqrt(sum / (end - start));
                        };
                        const before = rms(0.2, 0.8), first = rms(1.4, 1.8), second = rms(2.4, 2.8);
                        set(delay.modulation, 1);
                        const [stress] = processor.render([], rate * 2);
                        let peak = 0;
                        for (const sample of stress) {
                            if (!Number.isFinite(sample)) throw new Error("Non-finite modulated delay output");
                            peak = Math.max(peak, Math.abs(sample));
                        }
                        results.push({ mode: delay.mode, rate, bpm, before, first, second, peak });
                    }
                }
                if (delay.mode === "Synchronized") {
                    const context = new OfflineAudioContext(1, 96000, 48000);
                    const node = await new FaustMonoDspGenerator().createNode(context, "OrbitDelay", { ...compiled, soundfiles: {} }, false);
                    const transport = new TransportSignalSource();
                    try {
                        const writes = [];
                        const originalSet = node.setParamValue.bind(node);
                        node.setParamValue = (path, value) => { writes.push([path, value]); originalSet(path, value); };
                        for (const [name, value] of Object.entries(delay.values))
                            node.setParamValue(node.getParams().find(path => path.endsWith("/" + name)), value);
                        transport.setSnapshot({ beatsPerMinute: 90, beatPosition: 0, isPlaying: false });
                        await transport.connect(context, node);
                        const path = node.getParams().find(path => path.endsWith("/orbit_tempo"));
                        if (!writes.some(([key, value]) => key === path && value === 90)) throw new Error("Initial tempo not applied to delay");
                        transport.setSnapshot({ beatsPerMinute: 180, beatPosition: 0, isPlaying: false });
                        if (!writes.some(([key, value]) => key === path && value === 180)) throw new Error("Live tempo not applied to delay");
                        node.connect(context.destination);
                        const suspended = context.suspend(0.5);
                        const rendering = context.startRendering();
                        await suspended;
                        // AudioParam scheduling sets the onset at a known audio frame.
                        const amplitudePath = node.getParams().find(path => path.endsWith("/" + delay.amplitude));
                        node.setParamValue(amplitudePath, 0.1);
                        await context.resume();
                        const audio = (await rendering).getChannelData(0);
                        const peak = (start, end) => audio.subarray(Math.floor(start * 48000), Math.floor(end * 48000))
                            .reduce((max, sample) => Math.max(max, Math.abs(sample)), 0);
                        if (peak(0.55, 0.78) > 0.001 || peak(0.92, 1.05) < 0.07)
                            throw new Error("Worklet echo did not follow 180 BPM");
                    } finally { transport.dispose(); node.disconnect(); node.destroy?.(); }
                }
            }
            return results;
        } finally { compiler.dispose(); }
    }, delays);
    for (const result of delayResults) {
        console.log("Delay response", result);
        assert.ok(result.before < 0.001, "Wet output must be silent before the delay time");
        assert.ok(Math.abs(result.first - 0.1 / Math.SQRT2) < 0.008);
        assert.ok(Math.abs(result.second / result.first - 1.5) < 0.1, "Feedback should add the first echo in phase");
        assert.ok(result.peak < 2);
    }

    const reverbResults = await page.evaluate(async reverb => {
        const { FaustCompilerClient } = await import("/Faust/FaustCompilerClient.js");
        const { FaustMonoDspGenerator } = await import("/generated/modulated-sine/faustwasm/index.js");
        const compiler = new FaustCompilerClient(() => new Worker("/Faust/FaustCompilerWorker.js", { type: "module" }));
        try {
            const compiled = await compiler.compile(reverb.source);
            const results = [];
            for (const rate of [44100, 48000]) {
                const create = async (decay, damping, mix = 1) => {
                    const processor = await new FaustMonoDspGenerator().createOfflineProcessor(rate, 128, { ...compiled, soundfiles: {} });
                    const set = (name, value) => processor.setParamValue(processor.getParams().find(path => path.endsWith("/" + name)), value);
                    for (const [name, value] of Object.entries(reverb.values)) set(name, value);
                    set(reverb.decay, decay); set(reverb.damping, damping); set(reverb.mix, mix);
                    processor.render([], rate);
                    return { processor, set };
                };
                const burst = async (decay, damping, mix = 1) => {
                    const { processor, set } = await create(decay, damping, mix);
                    set(reverb.amplitude, 0.2);
                    const [excitation] = processor.render([], rate);
                    set(reverb.amplitude, 0);
                    const [tail] = processor.render([], rate * 4);
                    const measure = (audio, from, to) => {
                        let power = 0, difference = 0;
                        const start = Math.floor(from * rate), end = Math.floor(to * rate);
                        for (let i = start; i < end; i++) {
                            if (!Number.isFinite(audio[i])) throw new Error("Non-finite reverb response");
                            power += audio[i] ** 2;
                            difference += (audio[i] - audio[i - 1]) ** 2;
                        }
                        return { rms: Math.sqrt(power / (end - start)), roughness: difference / Math.max(power, 1e-30) };
                    };
                    return { input: measure(excitation, 0.5, 0.9), early: measure(tail, 0.1, 0.3), late: measure(tail, 0.8, 1.2), end: measure(tail, 3, 3.8) };
                };
                const short = await burst(0.4, 0.2), long = await burst(3, 0.2), dark = await burst(3, 1), dry = await burst(3, 0.2, 0);
                const { processor, set } = await create(12, 0, 1);
                set(reverb.amplitude, 0.2); set(reverb.modulation, 1);
                const [stress] = processor.render([], rate * 3);
                let peak = 0;
                for (const sample of stress) {
                    if (!Number.isFinite(sample)) throw new Error("Unstable modulated reverb");
                    peak = Math.max(peak, Math.abs(sample));
                }
                results.push({ rate, short, long, dark, dry, peak });
            }
            return results;
        } finally { compiler.dispose(); }
    }, reverb);
    for (const result of reverbResults) {
        console.log("Reverb response", result);
        assert.ok(result.long.early.rms > 0.0005, "Wet signal must retain a tail after the source stops");
        assert.ok(result.long.late.rms > result.short.late.rms * 5, "Long decay must sustain more tail energy");
        assert.ok(result.long.end.rms < result.long.early.rms * 0.1, "Tail should decay");
        // The early window includes the smoothed source's release and initial
        // reflections. Measure absorption and bypass on the established tail.
        assert.ok(result.dark.late.roughness < result.long.late.roughness * 0.5, "Damping must reduce late high-frequency energy");
        assert.ok(result.dry.late.rms < 1e-5, "Zero wet mix must remove the reverb tail");
        assert.ok(result.peak < 2);
    }

    const chorusResults = await page.evaluate(async chorus => {
        const { FaustCompilerClient } = await import("/Faust/FaustCompilerClient.js");
        const { FaustMonoDspGenerator } = await import("/generated/modulated-sine/faustwasm/index.js");
        const compiler = new FaustCompilerClient(() => new Worker("/Faust/FaustCompilerWorker.js", { type: "module" }));
        try {
            const compiled = await compiler.compile(chorus.source);
            const results = [];
            for (const sampleRate of [44100, 48000]) {
                const render = async (rate, depth, mix, modulation = 0) => {
                    const processor = await new FaustMonoDspGenerator().createOfflineProcessor(sampleRate, 128, { ...compiled, soundfiles: {} });
                    const set = (name, value) => processor.setParamValue(processor.getParams().find(path => path.endsWith("/" + name)), value);
                    for (const [name, value] of Object.entries(chorus.values)) set(name, value);
                    set(chorus.rate, rate); set(chorus.depth, depth); set(chorus.delay, 13); set(chorus.mix, mix);
                    set(chorus.modulation, modulation);
                    processor.render([], sampleRate);
                    return processor.render([], sampleRate * 4)[0];
                };
                const dry = await render(1, 3, 0), delayed = await render(1, 0, 1);
                const moving = await render(1, 0.5, 1), faster = await render(2, 0.5, 1);
                const stress = await render(5, 10, 1, 1);
                const tone = (audio, frequency) => {
                    let real = 0, imaginary = 0;
                    for (let i = 0; i < audio.length; i++) {
                        const angle = 2 * Math.PI * frequency * i / sampleRate;
                        real += audio[i] * Math.cos(angle); imaginary += audio[i] * Math.sin(angle);
                    }
                    return 2 * Math.hypot(real, imaginary) / audio.length;
                };
                const lag = 0.013 * sampleRate;
                let errors = 0, count = 0;
                for (let i = Math.ceil(lag) + 1; i < delayed.length; i++) {
                    const index = i - lag, start = Math.floor(index), fraction = index - start;
                    const expected = (1 - fraction) * dry[start] + fraction * dry[start + 1];
                    errors += (expected - delayed[i]) ** 2; count++;
                }
                let peak = 0;
                for (const sample of stress) {
                    if (!Number.isFinite(sample)) throw new Error("Non-finite modulated chorus");
                    peak = Math.max(peak, Math.abs(sample));
                }
                // RMS is independent of the oscillator's small sample-rate-dependent
                // frequency error; compare sidebands against its measured leakage.
                const dryAmplitude = Math.sqrt(2 * dry.reduce((sum, sample) => sum + sample * sample, 0) / dry.length);
                results.push({ sampleRate, dry: dryAmplitude, delayError: Math.sqrt(errors / count),
                    staticSideband: tone(delayed, 441), movingSideband: tone(moving, 441),
                    fasterSideband: tone(faster, 442), fasterOffBand: tone(faster, 441), peak });
            }
            return results;
        } finally { compiler.dispose(); }
    }, chorus);
    for (const result of chorusResults) {
        console.log("Chorus response", result);
        assert.ok(Math.abs(result.dry - 0.1) < 0.001, "Dry mix preserves carrier amplitude");
        assert.ok(result.delayError < 0.002, "Zero depth is a fixed fractional delay");
        assert.ok(result.movingSideband > 0.005 && result.movingSideband > 5 * result.staticSideband, "Modulation produces pitch sidebands");
        assert.ok(result.fasterSideband > 0.005 && result.fasterSideband > 5 * result.fasterOffBand, "Rate controls sideband spacing");
        assert.ok(result.peak <= 0.101, "Convex delay blend remains within input amplitude bounds");
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

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
} finally {
    await browser?.close();
    server.close();
}

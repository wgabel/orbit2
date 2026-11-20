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
        const { FaustMonoDspGenerator } = await import("/generated/modulated-sine/faustwasm/index.js");
        const compiler = new FaustCompilerClient(() => new Worker("/Faust/FaustCompilerWorker.js", { type: "module" }));
        const context = new AudioContext();
        try {
            await context.resume();
            const factory = new FaustNodeFactory(null, new Map(), () => new FaustMonoDspGenerator(), compiler);
            const node = await factory.create(context, "OrbitGraph", manifest.values, source);
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
            // The same connected worklet must respond to live base edits.
            // Mute the carrier amplitude and then restore it on the same node.
            factory.setParameters(node, { [manifest.amplitude]: 0 });
            const mutedPeak = await waitForPeak(value => value < 0.001);
            factory.setParameters(node, { [manifest.amplitude]: manifest.values[manifest.amplitude] });
            const restoredPeak = await waitForPeak(value => value > 0.035);
            node.disconnect();
            node.destroy();
            // Distinct topology must use a distinct worklet processor name.
            const second = await factory.create(context, "OrbitGraph", {}, "process = 0.0;");
            second.disconnect();
            second.destroy();
            return { peak, mutedPeak, restoredPeak, sampleRate: context.sampleRate };
        } finally { compiler.dispose(); await context.close(); }
    }, { source, manifest });
    console.log("Generated graph produced real worklet audio:", result);
    assert.ok(result.peak > 0.035 && result.peak <= 0.151);
    assert.ok(result.mutedPeak < 0.001, "Live values did not mute the existing worklet");
    assert.ok(result.restoredPeak > 0.035 && result.restoredPeak <= 0.151, "Live values did not restore audio");
} finally {
    await browser?.close();
    server.close();
}

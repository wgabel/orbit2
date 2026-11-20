// Browser audio adapter: bridges C# interop to Faust's generated WASM.
// This module contains no oscillator algorithm: Tone.dsp supplies the samples.
import { FaustMonoDspGenerator } from "./generated/faustwasm/index.js";

let audioContext;
let outputGain;
let faustNode;

// Load the precompiled Faust program relative to this module, not the document,
// so resources work both on localhost and under /orbit2/ on GitHub Pages.
async function createFaustGraph() {
    const [wasmResponse, metadataResponse] = await Promise.all([
        fetch(new URL("./generated/dsp-module.wasm", import.meta.url)),
        fetch(new URL("./generated/dsp-meta.json", import.meta.url))
    ]);

    if (!wasmResponse.ok || !metadataResponse.ok) {
        throw new Error("Faust build assets are missing. Compile Tone.dsp first.");
    }

    const factory = {
        module: await WebAssembly.compile(await wasmResponse.arrayBuffer()),
        json: await metadataResponse.text(),
        soundfiles: {}
    };

    const generator = new FaustMonoDspGenerator();
    // false selects AudioWorklet (real-time render thread), not ScriptProcessor.
    const node = await generator.createNode(audioContext, "OrbitTone", factory, false);

    if (!node) {
        throw new Error("Faust failed to create an AudioWorklet node.");
    }

    outputGain = audioContext.createGain();
    outputGain.gain.value = 0;
    node.connect(outputGain);
    outputGain.connect(audioContext.destination);
    faustNode = node;
}

// Called by BrowserAudioService only after a user clicks the Blazor button.
export async function playTestTone() {
    const AudioContextType = window.AudioContext || window.webkitAudioContext;
    if (!AudioContextType) {
        throw new Error("This browser does not support Web Audio.");
    }

    // Create and resume the context directly from the click to satisfy browser
    // autoplay restrictions. Reuse one graph for all subsequent clicks.
    audioContext ??= new AudioContextType();

    if (audioContext.state !== "running") {
        await audioContext.resume();
    }

    if (!faustNode) {
        await createFaustGraph();
    }

    // The oscillator is continuous; a short, quiet gain envelope makes it
    // audible for 400 ms without abrupt sample discontinuities or clipping.
    const start = audioContext.currentTime + 0.01;
    const gain = outputGain.gain;
    gain.cancelScheduledValues(start);
    gain.setValueAtTime(0, start);
    gain.linearRampToValueAtTime(0.1, start + 0.02);
    gain.setValueAtTime(0.1, start + 0.36);
    gain.linearRampToValueAtTime(0, start + 0.40);

    // This timer only keeps the UI button busy; the AudioContext clock
    // schedules the actual sound and fades independently of UI timing.
    await new Promise(resolve => setTimeout(resolve, 450));
}

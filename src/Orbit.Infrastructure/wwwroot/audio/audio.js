// JavaScript composition root and interop entry point. Only this module
// assembles dependencies; individual components can be tested independently.
import { FaustMonoDspGenerator } from "./generated/modulated-sine/faustwasm/index.js";
import { AudioEngine } from "./AudioEngine.js";
import { AudioContextManager } from "./Components/AudioContextManager.js";
import { AudioClock } from "./Components/AudioClock.js";
import { AudioEnvelope } from "./Components/AudioEnvelope.js";
import { AudioOutput } from "./Components/AudioOutput.js";
import { FaustModuleLoader } from "./Faust/FaustModuleLoader.js";
import { FaustNodeFactory } from "./Faust/FaustNodeFactory.js";
import { FaustCompilerClient } from "./Faust/FaustCompilerClient.js";

import { FaustOutputMonitor } from "./Faust/FaustOutputMonitor.js";

const processors = new Map([
    ["ModulatedSine", { name: "OrbitModulatedSine", baseUrl: new URL("./generated/modulated-sine/", import.meta.url) }]
]);

const contextManager = new AudioContextManager(() => {
    const Context = globalThis.AudioContext || globalThis.webkitAudioContext;
    if (!Context) throw new Error("Web Audio is unavailable.");
    return new Context();
});
const audioClock = new AudioClock(contextManager);
const moduleLoader = new FaustModuleLoader(
    url => fetch(url),
    bytes => WebAssembly.compile(bytes));
const compiler = new FaustCompilerClient(() =>
    new Worker(new URL("./Faust/FaustCompilerWorker.js", import.meta.url), { type: "module" }));
const nodeFactory = new FaustNodeFactory(
    moduleLoader, processors, () => new FaustMonoDspGenerator(), compiler);
const output = new AudioOutput(new AudioEnvelope());
const engine = new AudioEngine(contextManager, nodeFactory, output, node => new FaustOutputMonitor(node));

export const play = request => engine.play(request);
export const stop = soundId => engine.stop(soundId);
export const getClockSnapshot = () => audioClock.getSnapshot();
export const getPlaybackState = soundId => engine.getPlaybackState(soundId);
export const setParameters = (soundId, parameters) => engine.setParameters(soundId, parameters);
export const markRevision = (soundId, revision) => engine.markRevision(soundId, revision);
export const replace = (request, revision) => engine.replace(request, revision);
export const getOutputValues = soundId => engine.getOutputValues(soundId);
export async function dispose() {
    try { await engine.dispose(); }
    finally { compiler.dispose(); }
}

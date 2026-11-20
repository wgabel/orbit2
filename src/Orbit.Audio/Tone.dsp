// Faust DSP source: a monophonic A4 sine oscillator.
// The browser audio adapter controls playback duration and output gain.
// Compiled to a WebAssembly AudioWorklet by the faustwasm CLI at build time.
import("stdfaust.lib");

process = os.osc(440);

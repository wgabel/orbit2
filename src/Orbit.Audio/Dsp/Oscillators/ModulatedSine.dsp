// Phase 2's fixed signal graph. The reusable oscillator accepts any
// signal-valued frequency; neither oscillator knows about a canvas or UI.
import("stdfaust.lib");
declare name "ModulatedSine";

sineGenerator(frequency) = os.osc(frequency);

// Only base settings enter from the host. Smooth control changes before
// combining them; do not smooth the audio-rate LFO waveform itself.
baseFrequency = hslider("baseFrequency", 440, 100, 1000, 1) : si.smoo;
lfoRate = hslider("lfoRate", 0.5, 0.05, 5, 0.01) : si.smoo;
depthHz = hslider("depthHz", 100, 0, 200, 1) : si.smoo;

lfo = sineGenerator(lfoRate);
effectiveFrequency = max(20, min(0.49 * ma.SR, baseFrequency + depthHz * lfo));

// os.osc advances its phase using the actual DSP sample rate; modulation
// changes the increment without resetting the phase. The browser's existing
// voice gain applies the start/stop envelope and output level.
process = sineGenerator(effectiveFrequency);

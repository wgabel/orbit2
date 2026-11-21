# References and glossary

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

## 16. References and glossary

### Technical references

- [Orbit 2 repository](https://github.com/wgabel/orbit2) — existing proof-of-concept project.
- [Faust documentation](https://faustdoc.grame.fr/) — DSP language and architecture.
- [Faust architecture files and sample-accurate control](https://faustdoc.grame.fr/manual/architectures/) — sample-rate processing and host control timing.
- [Faust optimization and control-rate behavior](https://faustdoc.grame.fr/manual/optimizing/) — explains block-level control reads versus sample-rate processing.
- [Faust signal API](https://faustdoc.grame.fr/tutorials/signal-api/) — programmatic creation/compilation of signal expressions.
- [FaustWasm](https://github.com/grame-cncm/faustwasm) — browser-compatible Faust compiler and Web Audio bindings.
- [Faust soundfile support](https://faustdoc.grame.fr/manual/soundfiles/) — indexed access to externally supplied audio data.
- [Web Audio decoding](https://developer.mozilla.org/en-US/docs/Web/API/BaseAudioContext/decodeAudioData) — asynchronous decoding and sample-rate conversion.
- [Web Audio API specification](https://www.w3.org/TR/webaudio/) — AudioContext, AudioWorklet, sample rate, current frame/time and render quanta.
- [Faust parallel composition](https://faustdoc.grame.fr/manual/syntax/) — returning multiple signals from a compiled program.
- [Web Audio AnalyserNode](https://www.w3.org/TR/webaudio-1.0/#AnalyserNode) — time-domain and frequency-domain observation.

### Glossary

- **DSP:** Digital signal processing.
- **LFO:** Low-frequency oscillator; a generator used at low frequency, commonly as modulation.
- **FM:** Frequency modulation.
- **AM:** Amplitude modulation.
- **Hz:** Cycles per second.
- **Cent:** 1/100 of a semitone; 1,200 cents = one octave.
- **BPM:** Beats per minute.
- **Sample rate:** Number of sample frames processed per second.
- **Render quantum:** A block of frames delivered for audio processing.
- **Phase:** Position within a repeating oscillator cycle.
- **Graph topology:** The pattern of nodes and connections independent of canvas positions.
- **Composite:** A node encapsulating a nested graph.
- **Boundary port:** A public input/output of a composite represented internally by a rack socket.
- **Domain event:** A notification describing a successfully committed domain change.
- **Signal tap:** A passive observation of an existing DSP signal, not an audible routing edge or an independent DSP instance.
- **FFT:** Fast Fourier transform, used to estimate frequency content from a finite time-domain buffer.
- **Frequency response:** Gain versus frequency of a system with fixed coefficients; distinct from the spectrum of a particular input/output signal.
- **Envelope:** A shaped control trajectory over time, typically attack/decay or attack/decay/sustain/release.
- **Trigger:** A discrete event that starts or retriggers an operation; a gate is a held activation level and a clock supplies rhythmic events.

---

**Summary:** The C# graph describes the instrument, Faust executes it, and Blazor visualizes and edits it. Every drone is a graph that can be opened, connected, saved and reused. Every appropriate parameter can receive signals. All real-time computations share audio timing; application events coordinate edits without becoming part of the real-time processing loop.

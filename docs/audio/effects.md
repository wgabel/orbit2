# Audio processors and effects

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [dsp engine](dsp-engine.md) · [parameters and modulation](../core/parameters-and-modulation.md) · [visualizations](../editor/visualizations.md).

### Audio processors and effects

#### Resonant filter implementation (Phase 6.7 — implemented)

`FilterProcessor` is a mono processor with a structural mode choice and two continuously modulatable parameters, cutoff in Hz and resonance Q. `FilterFaustEmitter` selects a response from one topology-preserving state-variable filter. `FaustStateVariableFilter` calculates trapezoidal integrator updates with audio-rate coefficients derived from cutoff and Q; two delayed feedback states remain internal to DSP, not editable graph cycles. The cutoff is limited to 0.45 times the actual sample rate and Q is constrained to 0.5–8. Mode changes preserve model identities and crossfade replacement DSP; base cutoff/Q changes preserve runtime state. Output bounds are deliberately unknown given resonance and time variation.

~~~mermaid
flowchart TD
    A[Audio input] --> F[Two-state filter]
    C[Cutoff and Q signals] --> F
    F --> S[Selected response]
    S --> O[Audio output]
~~~

<a id="parametric-eq-phase-612"></a>

#### Parametric EQ (Phase 6.12 — implemented)

`ParametricEqProcessor` is a mono single-band bell equalizer. It registers through the ordinary node and Faust emitter catalogs; generic parameter UI, modulation, persistence, history and nested graph compilation apply without special cases. Frequency (20–20,000 Hz), gain (−18 to +18 dB) and Q (0.2–10) are all continuous additive modulation targets in Hz, dB and ratio respectively. Defaults are 1 kHz, 0 dB and Q 1. The emitter bounds frequency at 0.45 times the actual sample rate.

The filter and EQ share `FaustStateVariableFilter.EmitCore`, which implements a two-state trapezoidal integrator. With `A = 10^(gain/40)` and damping `k = 1/(Q*A)`, the bell response is `x + k*(A*A-1)*band`. This gives unity at zero gain, reciprocal static boost/cut and audio-rate coefficient updates without resetting DSP state. Output range remains unknown for time-varying EQ; use gain stages for headroom. Offline tests measure gain and width at 44.1/48 kHz and stress all modulation inputs.

<a id="feedback-delay-phase-613"></a>

#### Feedback delay (Phase 6.13 — implemented)

`DelayProcessor` is a mono processor with one audio input/output, free delay time (0.01–2 s), feedback (0–0.9) and wet mix (0–1). The continuous parameters use the shared modulation system. Timing mode is structural; synchronized mode activates the shared musical-division definition and deactivates free-delay time and its incoming wires without deleting them. Musical divisions and their Faust beat conversion are shared with the oscillator. Delay time is `60 * beats / BPM` in synchronized mode, with smoothed transitions; the transport's phase-reset semantics do not reset an echo buffer.

The emitter uses one bounded `de.fdelay` buffer, linear fractional-sample interpolation and internal sample-delayed feedback; the additional `mem` aligns the first echo and subsequent repeat spacing with the requested time. Maximum allocation is mode-specific for up to 192 kHz (2 s free or 12 s synchronized at 20 BPM). The dry/wet mix is linear. Feedback is capped below unity, but returned output bounds remain unknown for arbitrary modulated inputs. Changing delay time can pitch-shift existing echoes; structural replacements crossfade and may lose the previous echo tail. Do not model internal feedback as an editable graph cycle.

Transport-aware DSP requests distinguish beat phase and absolute BPM. `FaustNodeContext.TransportTempo` requests one reserved host control `orbit_tempo`, excluded from editable parameter manifests. Existing beat consumers still use a single audio-rate input. `TransportSignalSource` initializes the BPM control from its current anchor and updates active and retiring DSP nodes on tempo changes, without deriving tempo from a quantized wrapped phase or requiring a phase worklet for tempo-only graphs. Unregister controls on voice disposal. Offline waveform tests verify timing and feedback at 44.1/48 kHz; live host control tests verify current tempo and subsequent edits.

<a id="algorithmic-reverb-phase-614"></a>

#### Algorithmic reverb (Phase 6.14 — implemented)

`ReverbProcessor` is mono with continuous decay (RT60, 0.1–12 s), damping (0–1) and wet mix (0–1) controls. `ReverbFaustEmitter` reuses the standard parameter/modulation context and delegates to `FaustSchroederReverb`. Eight parallel feedback combs with distinct 25–37 ms delay lengths and four serial Schroeder all-pass diffusers form the late reverberation. Delay lengths are fixed in seconds, converted at the actual sample rate, and bounded by compile-time allocations sized up to 192 kHz. Each comb uses feedback `10^(-3 * delaySeconds / decay)` and a one-pole damping filter whose cutoff moves logarithmically from 20 kHz toward 500 Hz. Coefficients remain bounded below unity even under audio-rate modulation. RT60 is an approximate low-frequency decay target, not an exact broadband measurement. Wet summing has attenuation for headroom, not clipping or adaptive normalization. Output range remains unknown for arbitrary input and time-varying parameters.

Base edits and modulation retain the active delay state; dry/wet mix is linear (zero wet hides the tail but does not stop its computation). Stop and structural graph swaps use existing envelope/crossfade behavior and do not promise preservation of long reverb tails. This first implementation uses fixed geometry and mono routing; room size, stereo and alternative algorithms are later extensions. Offline tests use noise bursts to measure tail energy, decay, damping and dry bypass, with extreme modulation at 44.1/48 kHz.

<a id="chorus-phase-615"></a>

#### Chorus (Phase 6.15 — implemented)

`ChorusProcessor` is a mono effect with rate (0.05–5 Hz), depth (0–10 ms), base delay (5–40 ms) and wet mix (0–1), all audio-rate modulatable. Milliseconds are a new shared parameter unit; append it to the unit enum and render it through the common unit formatter. Defaults are 0.6 Hz, 3 ms, 15 ms and 0.5. `ChorusFaustEmitter` uses an integrated free-running phase and two sine-modulated fractional delay taps separated by one-third of a cycle. The time excursion is limited to `min(depth, baseDelay - 1 ms)`; taps use actual sample rate and fixed buffers bounded to 50 ms at 192 kHz. Linear interpolation and equal averaging form the wet signal, which is blended linearly with dry input. No feedback, new audio clock, structural constants or special UI are required.

The conservative signal range is the input range expanded to include zero (initially empty delay buffers). All combinations are convex weighted sums, so no extra gain occurs. Base edits retain the phase and delay states; high-speed modulation may deliberately pitch-shift the wet signal. Stereo widening, alternate voice counts and tempo-locked chorus are future extensions. Offline tests verify dry amplitude, fixed fractional delay at zero depth, sideband spacing controlled by rate, and bounded output under extreme modulation at 44.1/48 kHz.

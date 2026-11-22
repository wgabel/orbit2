# Resonators and modal synthesis

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [envelopes](../audio/envelopes.md) · [pitch control](pitch-control.md) · [polyphony](polyphony.md) · [instrument recipes](../examples/instrument-recipes.md).

For the expansion beyond these existing resonator-based approximations, see [Faust-first realistic instrument emulation](realistic-instrument-emulation.md) (7.2.7): audit exact dependency licenses, evaluate upstream waveguide/excitation/models, add continuous expression and validate sound and performance. The current resonator remains an independent primitive.

For practical wiring, playing and retuning examples, see [built-in instruments and resonators](../usage/sound-and-instruments.md#3-built-in-instruments-and-resonators).

##### Resonator processor and modal synthesis

A resonator is an **audio-input processor**, not an oscillator that always generates a sound. An impulse or noise burst supplies excitation; the internal resonant state rings at its tuned frequency even after input returns to zero. A sustained input may continue exciting it. This makes body resonances and struck instruments possible without a recording. Begin with one `processor.resonator` using the existing node registry, common parameter UI, modulation, bypass/dry contract where appropriate and shared visualization. It is useful by itself for ringing textures and as a component of modal synthesis.

| Initial parameter | Meaning and implementation |
|---|---|
| Frequency (Hz) | Resonant center frequency with the normal non-linear display scale and optional shared pitch tracking; bounded below Nyquist. |
| Decay (seconds, T60) | Time for free-decaying amplitude to fall by 60 dB; positive, finite, bounded (initial proposal 0.02–20 s, tuned through profiling). |
| Excitation gain | Linear control of input energy into the resonator; bounded and modulatable. |
| Output gain / mix | Explicit level/headroom and optional dry/wet audition using existing effect contracts; no undocumented automatic loudness normalization. |

In `Orbit.Audio`, implement a reusable sample-rate-aware damped second-order mode; one candidate is the complex state recurrence `z[n] = r * exp(i*theta) * z[n-1] + g*x[n]`, with real or imaginary output explicitly chosen and documented, `theta = 2*pi*f/Fs`, and `r = exp(-ln(1000)/(T60*Fs))`. For fixed coefficients a free amplitude decays to 0.001 after T60; poles stay inside the unit circle for finite positive decay. Derive a real-valued equivalent for Faust, share coefficients with any pure response provider and compare against compiled DSP. A selected normalization convention must explicitly distinguish impulse response from sustained-input resonant gain; longer decay can increase steady-state level, so bound excitation and document headroom. Do not substitute an arbitrary high-Q low-pass or tune Q as if it were independently specified decay. Verify stable coefficient changes (frequency, decay), finite ranges and smoothing under audio-rate modulation; the rotation-state form with bounded radius preserves a useful stability invariant even with time-varying coefficients. Keep all inner feedback sample-delayed inside the emitter, not a cycle in the editable graph. Initial state is zero; silence alone does not self-oscillate. Retain resonant state for ordinary live base edits, and document possible tail loss during topology replacement. Use conservative prediction only when provable; otherwise mark output range unknown. No fake acoustic output should enter signal analysis.

### Phase 7.2.4 — Resonator (implemented)

Implementation: [PR #34](https://github.com/wgabel/orbit2/pull/34).

Add **Resonator** from the normal node catalog. It accepts mono audio, emits mono audio, and supports the standard mute/bypass controls, presets, scope/spectrum and graph persistence. It does not generate a tone from silence. All six parameters are live and continuously modulatable; no new runtime or special editor is required.

| Parameter | Default | Range / meaning |
|---|---:|---|
| Frequency | 220 Hz | 20–20,000 Hz, additionally capped at 0.49 × sample rate |
| Pitch offset | 0 st | −60…+60 st; shared semitone tracking is always active; additive Hz modulation follows pitch scaling |
| Decay (T60) | 1 s | 0.02–20 s, independent of frequency |
| Excitation gain | 2 | 0–32 linear ratio; use small values for sustained input, higher values only with appropriate input headroom |
| Output gain | 0.5 | 0–1 wet signal trim |
| Wet mix | 1 | 0–1 dry/wet linear blend; state continues ringing when wet is hidden |

`FaustDampedMode` implements a real two-state rotation and contraction with radius `r = exp(log(0.001)/(T60*Fs))`. The output is the real state. Input drive is `(1-r) * excitationGain * x[n]`: with bounded input and drive, the state magnitude cannot exceed their product (apart from floating-point roundoff), even when frequency and decay change at audio rate. This deliberately normalizes the worst-case sustained gain, not the impulse peak or perceived loudness; a single-sample impulse produces a quieter initial ring for long decay, so use a longer excitation burst or increase drive carefully. The dry path is unaffected by output gain. No hidden clipping or automatic loudness correction is applied. The graph range estimator tracks effective Hz using the same pitch convention; output range remains conservatively unknown. Ordinary edits retain state; structural recompilation may lose tails. A dedicated exact resonator response curve is deferred; measure the output with scope/spectrum.

**Try a strike:** connect a Clock to an AD Envelope (short attack, decay around 40–100 ms), connect Envelope to amplitude of a quiet Noise or tuned oscillator (base amplitude zero), then send that audio to Resonator, followed by Gain and Master output. Start with the Wood strike preset. Do not send a typed Trigger directly as audio. For longer metal-like rings, increase T60 and carefully increase excitation while listening; avoid using high drive settings on a sustained in-tune carrier. To retune the resonator and excitation oscillator together, wire Pitch Control to both pitch inputs at amount 1. Five built-in presets provide tonal body, wood/metal strike and atmospheric starting points. Multiple resonators in parallel form a modal instrument (7.2.5).

Offline WASM regression tests compare the impulse to its analytic damped cosine and −60 dB decay, stress sample-rate modulation at 44.1/48 kHz, verify silence/dry path and check exact shared-pitch transposition through a real registered graph. Domain tests cover independent parameter identities, live controls, effective frequency and JSON round-trip.

<a id="phase-725--modal-instrument-composites-in-review"></a>

### Phase 7.2.5 — Modal instrument composites (implemented)

Implementation: [PR #37](https://github.com/wgabel/orbit2/pull/37).

For direct keyboard velocity use the [Playable modal variants](#phase-726b--playable-modal-instruments-in-review); for adapting original composites, see [the Note Source wiring example](pitch-control.md#phase-723--note-source-in-review).

**Modal synthesis** models a vibrating object as several independently tuned decaying modes. Add a **Modal — …** composite from the node catalog, connect its Audio output to Master output (or a mixer), start playback, and connect Manual Trigger or Clock to Trigger. Fire Manual Trigger to strike the body; the resonators ring and decay after the internal noise excitation stops. Optional Pitch Control or Note Sequencer connected to Pitch (st) at amount 1 transposes all modes together, without changing their decay. There is no sound without a strike or external excitation.

| Preset | Base frequency | Mode ratios | T60 in seconds |
|---|---:|---|---|
| Handpan | 220 Hz | 1, 2, 3, 4.12 | 1.8, 1.2, .65, .32 |
| Bell | 330 Hz | 1, 2.01, 2.4, 3.76, 5.21 | 2.8, 2.2, 1.6, .82, .38 |
| Gong | 83 Hz | 1, 1.51, 2.17, 3.42, 5.29, 8.13 | 3.5, 2.6, 2.1, 1.5, .8, .4 |
| Wood percussion | 294 Hz | 1, 2.76, 5.4 | .32, .16, .075 |

All four expose typed Trigger (accepts Clock), Pitch (st), Strength offset and Excitation (audio). Strength offset modulates the shared pre-bank Gain, base .75, clamped to 0–1. Offset −.75 prevents new excitation, not existing tails; −.375 halves the default strike level. This is continuous excitation strength, not yet latched MIDI/note velocity. External audio is optional and attenuated to .03 internally before that same gain; keep levels conservative, especially sustained in-tune excitation at high resonator drive. Open the composite to modify the AD burst, each mode's frequency, decay and output gain or change the excitation routing. Frequencies are stored as editable Hz values; ratios above describe the presets, not a separate mode parameter. The output mixer sums wet resonators only (no unfiltered noise or dry external path), with a final gain .8. Add reverb or other effects outside the composite as desired.

Graph tests verify independent instances, typed clock routing, mode tuning, editable state and JSON round trips. Compiled Faust/WASM tests at 44.1/48 kHz exercise manual strikes, silence before excitation, repeated hits, decay, strength scaling, actual spectral ring plus octave tracking of every mode, external audio and finite output. Default tested strike peaks span about .043–.150 of full scale; these are conservative synthetic starting points, not measured acoustic models or listening-validated timbres. Use the existing scope/spectrum on individual modes or summed output to inspect the ring.

### Phase 7.2.6b — Playable modal instruments (in review)

Implementation: [PR #46](https://github.com/wgabel/orbit2/pull/46).

Four **Playable — Handpan**, **Playable — Bell**, **Playable — Gong** and **Playable — Wood percussion** variants reuse the exact mode ratios, decay times, AD noise bursts and output trim of 7.2.5. They expose Trigger, Pitch (st), Velocity and optional Excitation. Wire Note Source Trigger, Pitch and Velocity at unity, then Audio to Master or Mixer; no Gate cable is required. Velocity directly scales the internal noise burst via a zero-based Gain before the resonator bank, clamped to 0–1. Velocity 0 or an unconnected Velocity port suppresses the internal strike. Unlike Strength offset in the original templates, this gain does not scale optional external audio: the independent Excitation input still feeds the bank at ×0.03, even with velocity 0. An external audio source can ring without a trigger. Existing Modal entries and saved graphs do not change.

| Instrument | Reference note | Native Hz | Strike attack / decay (ms) |
| --- | --- | ---: | --- |
| Handpan | A3 | 220 | 1 / 38 |
| Bell | E4 (approx.) | 330 | 1 / 30 |
| Gong | E2 (approx.) | 83 | 5 / 100 |
| Wood percussion | D4 (approx.) | 294 | 1 / 12 |

Reference sets zero semitone offset, not a new absolute frequency; the last three native tunings are approximate, so edit individual mode frequencies proportionally for exact concert pitch if desired. Each note-on fires the AD burst, even on overlapping notes; release does not cut the strike or ring, and fallback does not retrigger. This is one shared, continuously tuned bank, not polyphonic: new pitch or fallback retunes existing tails; changes in velocity during a burst also affect its remaining excitation (no independent per-hit velocity latch). The resonator states are never multiplied by velocity or reset on retrigger. Keep external drive conservative. See [keyboard workflow](../usage/sound-and-instruments.md#playable-modal-percussion).

The offline graph tests exercise the actual Note Source action queue → composite → master at 44.1/48 kHz: initial silence, zero/full/quarter velocity, repeated key strikes, octave tuning, release-independent tails, finite headroom, and external drive independent of zero-velocity strikes. Domain regression covers typed inputs, route topology, independent instances and JSON round-trip. These measurements do not certify acoustic realism.

**Possible later compact bank.** A reusable `ModalResonatorBank` processor can follow when the composite becomes cumbersome or profiling justifies sharing computation. It reuses the same mode algorithm, not a second synth engine. Its bounded immutable mode definitions contain stable per-mode IDs, frequency ratio (or fixed Hz), decay/T60 and relative gain. A common base pitch tunes the whole bank, with explicit brightness/decay/intensity macros where meaningful. Bound mode count (initially 4–16), define fixed versus tracking mode and safe frequency limits, preserve identity on edits, and validate mode payloads through graph commands, snapshots, JSON and history. Structural changes such as mode count or definitions may recompile/crossfade; common continuous controls use existing live parameter updates. Extend presets with a separately versioned validated mode payload only if needed; never silently drop it from scalar presets. A dedicated response curve and specialized mode-table editor remain possible follow-ups.

~~~mermaid
flowchart TD
    T[Trigger or note velocity] --> E[Short AD envelope]
    N[Noise burst] --> X[Excitation gain]
    E --> X
    X --> R1[Low mode]
    X --> R2[Middle mode]
    X --> R3[High mode]
    R1 --> M[Weighted mixer]
    R2 --> M
    R3 --> M
    M --> O[Audio output]
~~~

Use a very short AD-controlled white noise burst or a bounded pulse from the rhythmic infrastructure for a strike. The latter needs an explicit audio-excitation adapter rather than an implicit Trigger-to-Audio wire. Example synthetic handpan-like modes at 220, 440 and 660 Hz with respective T60 values around 1.5, 0.8 and 0.4 s and decreasing gains give a tuned ring that changes naturally over time. These are starting values for an approximation, not a measured handpan model. An inharmonic metal/bell template can use non-integer frequency ratios and longer independent decays; a wood template shorter T60 values and quieter upper modes. Velocity controls energy at the excitation input; multiple strikes superpose in the resonator states according to their excitation and headroom. Resonators must not be reset for every strike. Consider bowed/plucked-string waveguides, tube models and formant banks only after the simpler resonators have been measured; advanced models need independent stability and performance criteria.

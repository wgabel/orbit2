# Resonators and modal synthesis

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [envelopes](../audio/envelopes.md) · [pitch control](pitch-control.md) · [polyphony](polyphony.md) · [instrument recipes](../examples/instrument-recipes.md).

##### Resonator processor and modal synthesis

A resonator is an **audio-input processor**, not an oscillator that always generates a sound. An impulse or noise burst supplies excitation; the internal resonant state rings at its tuned frequency even after input returns to zero. A sustained input may continue exciting it. This makes body resonances and struck instruments possible without a recording. Begin with one `processor.resonator` using the existing node registry, common parameter UI, modulation, bypass/dry contract where appropriate and shared visualization. It is useful by itself for ringing textures and as a component of modal synthesis.

| Initial parameter | Meaning and implementation |
|---|---|
| Frequency (Hz) | Resonant center frequency with the normal non-linear display scale and optional shared pitch tracking; bounded below Nyquist. |
| Decay (seconds, T60) | Time for free-decaying amplitude to fall by 60 dB; positive, finite, bounded (initial proposal 0.02–20 s, tuned through profiling). |
| Excitation gain | Linear control of input energy into the resonator; bounded and modulatable. |
| Output gain / mix | Explicit level/headroom and optional dry/wet audition using existing effect contracts; no undocumented automatic loudness normalization. |

In `Orbit.Audio`, implement a reusable sample-rate-aware damped second-order mode; one candidate is the complex state recurrence `z[n] = r * exp(i*theta) * z[n-1] + g*x[n]`, with real or imaginary output explicitly chosen and documented, `theta = 2*pi*f/Fs`, and `r = exp(-ln(1000)/(T60*Fs))`. For fixed coefficients a free amplitude decays to 0.001 after T60; poles stay inside the unit circle for finite positive decay. Derive a real-valued equivalent for Faust, share coefficients with any pure response provider and compare against compiled DSP. A selected normalization convention must explicitly distinguish impulse response from sustained-input resonant gain; longer decay can increase steady-state level, so bound excitation and document headroom. Do not substitute an arbitrary high-Q low-pass or tune Q as if it were independently specified decay. Verify stable coefficient changes (frequency, decay), finite ranges and smoothing under audio-rate modulation; the rotation-state form with bounded radius preserves a useful stability invariant even with time-varying coefficients. Keep all inner feedback sample-delayed inside the emitter, not a cycle in the editable graph. Initial state is zero; silence alone does not self-oscillate. Retain resonant state for ordinary live base edits, and document possible tail loss during topology replacement. Use conservative prediction only when provable; otherwise mark output range unknown. No fake acoustic output should enter signal analysis.

**Modal synthesis** models a vibrating object as several independently tuned decaying modes. First build an editable composite `Modal Instrument` with regular individual resonators connected in parallel to one excitation input and summed with per-mode weights. This proves the model and uses existing graph/rack/mixer infrastructure. A reusable compact `ModalResonatorBank` processor can follow when the composite becomes cumbersome or profiling justifies sharing computation. It reuses the same mode algorithm, not a second synth engine. Its bounded immutable mode definitions contain stable per-mode IDs, frequency ratio (or fixed Hz), decay/T60 and relative gain. A common base pitch tunes the whole bank, with explicit brightness/decay/intensity macros where meaningful. Bound mode count (initially 4–16), define fixed versus tracking mode and safe frequency limits, preserve identity on edits, and validate mode payloads through graph commands, snapshots, JSON and history. Structural changes such as mode count or definitions may recompile/crossfade; common continuous controls use existing live parameter updates. Either extend presets with a separately versioned validated mode payload later or store the initial mode combinations as composite templates; never silently drop them from scalar presets. Begin with editable ordinary nodes and reuse the shared response/analysis views for configured versus measured resonances; add a specialized mode-table editor only if generic parameters/composite editing cannot express the data well.

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

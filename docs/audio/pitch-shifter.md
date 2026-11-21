# Post-mixer pitch shifting

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [effects](effects.md) · [dsp engine](dsp-engine.md) · [pitch control](../instruments/pitch-control.md).

<a id="pitch-shifter-phase-620-planned"></a>

#### Pitch shifter (Phase 6.20 — planned)

`PitchShifterProcessor` transposes an incoming mono audio signal, including a complete chord or layered drone after a mixer, without intentionally changing the duration or playback tempo. It is distinct from a frequency shifter (which adds a fixed number of Hz and changes harmonic relationships), a delay-time sweep (which only shifts pitch while the delay changes), and Phase 7.2.2 shared oscillator pitch control (which changes the frequencies at their sources without resampling existing audio).

~~~mermaid
flowchart LR
    A[Oscillator 1] --> M[Mixer]
    B[Oscillator 2] --> M
    C[Oscillator 3] --> M
    M --> P[Pitch Shifter]
    P --> O[Output]
~~~

**Node contract.** Follow `AudioEffectProcessor` with one mono audio input/output, normal mute/bypass, and generic parameter editors, presets, history, nested routing and persistence. Start with continuously editable transpose (−12 to +12 semitones, default 0), fine tune (−100 to +100 cents, default 0) and wet mix (0–1, default 1). Coarse and fine pitch use the same shared unit conversion planned in Phase 7.2.2: `ratio = 2^((semitones + cents/100)/12)`. A +12-semitone shift doubles every spectral frequency; −12 halves them. Semitone and cent modulation have explicit receiving-port units, bounded totals and smoothed changes to the resampling rate; do not reinterpret the existing additive-Hz oscillator modulation. A known zero-shift setting should be transparent, with a defined, click-safe transition into and out of active transposition. Wet mix zero and bypass must pass the dry input unchanged.

**DSP contract.** Evaluate a bounded overlapping-window variable-delay/granular resampler first: read fractional positions from a fixed-size circular input buffer, advance read heads at the selected ratio, and overlap/crossfade multiple windowed grains at wrap boundaries. Derive read increments, grain lengths and allocation from the actual sample rate, share the existing Faust delay/interpolation primitives where appropriate, and select interpolation/filtering that controls aliasing on upward shifts. Retain DSP state while parameters change; do not create new editable feedback edges, recompile on pitch changes or allocate buffers in the audio callback. Measure and document wet latency, transient smearing, sidebands and tonal quality before choosing default window geometry. A basic granular algorithm can introduce modulation artifacts, particularly on polyphonic input; the goal is usable musical transposition, not a guarantee of lossless pitch conversion. If quality is insufficient, evaluate a phase-vocoder or alternative overlap-add approach as a separate measured refinement. Keep CPU and memory bounded, declare unsupported high-rate behavior, and preserve silence and finite output at all valid settings.

**Mixing and presentation.** Distinguish delayed wet audio from the undelayed dry path: intermediate mix settings and parallel paths may cause comb filtering; show the measured/nominal processing latency and document this limitation rather than adding implicit graph-wide latency compensation. The existing scope/spectrum can compare input and output; no stationary transfer-function response curve applies to a pitch-changing processor. Keep range estimation conservative/unknown until a demonstrably safe bound exists; no automatic normalization or clipping. Add presets for octave up/down, fifth up/down and fine detune, plus a mixed-oscillator example. Verify clean bypass/mute and unchanged graph topology on live edits, including nested composites and state continuity during playback.

**Verification.** At 44.1/48 kHz render single sine tones, a non-harmonically related two-tone mixture, a harmonic chord, transients and silence. Check each expected shifted spectral peak (including upward/downward octave, fifth and cent offsets), preserved frequency ratios, suppression of unintended aliases and excessive sidebands, bounded output and no cumulative tempo/duration change over sustained input. Measure impulse latency and transition discontinuities at zero shift, modulation extremes, mix endpoints and audition changes; test multiple independent instances and benchmark CPU/memory. No new external audio dependency or hardware integration is required for the first version.

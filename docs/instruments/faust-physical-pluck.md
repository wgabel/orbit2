# Physical Pluck — calibrated expressive prototype

[Faust-first design](realistic-instrument-emulation.md) · [Initial sound probes](faust-physical-model-probes.md) · [Dependency audit](faust-dependency-audit.md) · [Physical modelling primitives](physical-modelling.md)

**Status:** Phase 7.2.7b research and calibrated prototype; production node integration in review in [PR #81](https://github.com/wgabel/orbit2/pull/81). See [actual graph implementation and usage](faust-instrument-nodes.md). The historical research results below remain useful; no user listening approval or final compiler-distribution compliance is claimed.

## 1. Why this wrapper

The initial probe established that `pm.ks` is a competent plucked-string waveguide, but frequency error varied by sample rate and fixed damping made high notes decay much faster. An expressive instrument also needs a repeatable strike with velocity, pitch held for the active string and optional external excitation; simply scaling the resonator's output by continuous note velocity is wrong because it alters already-ringing sound.

The [research DSP](../../scripts/research/pluck-wrapper.dsp) uses the exact audited Faust 2.90.0, physmodels 1.2.1. It is a local, original wrapper around the library, not a copy of its model source.

### Tuning and decay

The pinned model internally subtracts a hand-tuned 0.05 m from its length. Its round-trip delay also includes approximately 6.5 fixed processing samples. For target frequency f and actual sample rate Fs, supply length in metres:

```
L = 340 / f + 0.05 - 6.5 * 340 / Fs
```

The 6.5-sample term is an inferred effective delay, verified by the tests below; it is not a stable public API or a universal formula for other models or library versions. Re-evaluate after dependencies change. The model's default maximum length is 3 m. Restrict the pitch to −21…+15 semitones relative to A4 (C3–C6, approximately 130.81–1046.50 Hz); do not advertise the normal oscillator's full frequency range.

The pin's loop reflection is approximately `(1 - 0.2 * d) * cos(pi*f/Fs)` at the fundamental, where d is its damping input. For intended T60 (time for fundamental amplitude to fall by 60 dB), desired reflection `g = 0.001^(1/(f*T60))`. Hence:

```
d = clamp((1 - g / cos(pi*f/Fs)) / 0.2, 0, 1)
```

T60 is bounded 0.3–2.0 s in this candidate. This maps model damping to a musical, consistent decay time, not a promise that all partials decay equally; the averaging reflection naturally damps high frequencies faster. The upper and lower T60 limits are deliberately chosen within the validated model domain. A live decay change changes the ringing state decay without resetting the string.

### Trigger and control behavior

| Input | Contract |
|---|---|
| Pitch | Continuous semitone offset from A4; captured on the exact Trigger sample (and first sample for initial external excitation). Clamp to C3–C6. |
| Trigger | Typed one-sample event pulse (Clock compatible). Each pulse starts a 6 ms bandless windowed noise burst. No Gate required; note-off and fallback do not retrigger or change held pitch. |
| Velocity | 0–1, sampled on Trigger, scales only this burst. Zero suppresses the burst; it does not stop an existing tail. |
| Decay | T60 seconds, 0.3–2.0; continuous effective control. |
| Excitation | Optional independent audio input, clipped to ±1 before small loss-dependent drive. Can ring without a trigger at the initial captured pitch. Deliberate retuning requires a trigger; velocity zero may be used with external-only excitation. |
| Audio | Mono ringing string, wet only, fixed .5 output trim in the research wrapper. No dry noise leak or output-normalization limiter. |

Initial state is silent with zero external excitation and no trigger. Pitch and velocity changes between events must not affect the already captured strike pitch or its excitation; changes at a subsequent trigger must take effect that sample. A new strike at a new pitch reuses the same waveguide and may retune residual energy (monophonic); this is not independent string/voice allocation or true polyphony. The new event replaces the burst, not the resonator state. Extremely close adjacent pulses can replace prior burst progress; circuit headroom and click behavior require additional listening and stress tests. External audio is bounded relative to loop loss to reduce overload from sustained excitation; tested peak bounds do not constitute a stability proof for every arbitrary input sequence.

## 2. Repeatable engineering experiment

From repo root (use the pinned package from the existing build or unpacked exact npm tarball):

```sh
node scripts/research/pluck-wrapper-probes.mjs node_modules/@grame/faustwasm /tmp/orbit-pluck
```

The [runner](../../scripts/research/pluck-wrapper-probes.mjs) hashes the exact browser bundle, compiles the research DSP once, records reachable library SHA-256 and factory metadata, then generates `pluck-wrapper-report.json`. The 128-frame offline render covers every chromatic note C3–C6, 37 pitches × 3 decay times (.3, 1, 2 s) × 2 sample rates = 222 independent three-second trials. All use a fixed deterministic Faust noise generator; initial silence is 100 ms. Pitch is normalized autocorrelation with parabolic period interpolation within the expected pitch window. T60 is inferred from a linear fit of log fundamental amplitude measured by seven Hann-windowed projections at the estimated frequency over part of the tail (not broadband room RT60). Repeated render timings include allocation/copying, exclude instance construction and control-vector creation, and do not measure live Worklet deadlines.

| Metric | 44.1 kHz | 48 kHz |
|---|---:|---:|
| Calibrated isolated trials | 111/111 | 111/111 |
| Worst absolute tuning deviation | 0.0972 cents | 0.1177 cents |
| Worst relative T60 estimation error | 0.110% | 0.040% |
| Worst isolated peak (velocity .8) | .1419 | .1472 |
| Sustained clipped in-tune external test peak | .1977 | .1977 |
| Rapid event and extreme input stress peak | .4904 | .4277 |
| Offline median per two seconds (5 repeats) | 7.61 ms | 7.96 ms |

Node v24.22.3, Linux x64, AMD EPYC 9V45. The acceptance assertions are <1 cent absolute pitch error (runner accepts ≤1), ≤3% T60 error, exact zero idle, finite samples and peak below 1 in defined scenarios. No per-note tuning lookup table is used. Timings are indicative only; profiling and graph-level comparison remain open.

Both rates also pass:

- exact sample-for-sample comparison of output when pitch and velocity change during the burst and ringing tail, then return before the next trigger;
- new event at a different pitch/velocity changes the output only on/after that event; new pitch is approximately 659.25 Hz;
- half-velocity isolated strike is sample-for-sample half amplitude; zero velocity produces no sound in an idle instance;
- changing pitch without a strike or external input produces exactly zero; extreme pitch, decay and velocity inputs clamp identically to their declared bounds;
- external-only sine at 130.81 Hz with source amplitude 4 is clipped at input, rings without a trigger, and remains finite; 5 seconds of rapid retriggers (~77/s), out-of-range pitch/velocity/decay and alternating ±3 external drive remain finite and under peak 1.

The research wrapper does not establish that the instrument sounds like an actual guitar, harp or other stringed instrument; it is a calibrated generic string. It has no adjustable pluck position, humanized attack, body resonance or multiple independently ringing strings. Continue level-matched listening within the instrument audition workflow.

## 3. License delta (research only)

The final factory includes the six libraries from the minimal `pm.ks` audit plus the following exact dependencies. All six previous library hashes match the earlier audit. The selected symbols and their reachable implementation sections were inspected in the embedded files, not upstream master.

| New library | Version | SHA-256 | Evidence |
|---|---|---|---|
| basics.lib | 1.23.1 | `aca388dc7a461883d3c9afe0bf49988672758753b34758130e74efe76f4d0029` | `countup`, `sAndH` in GRAME section 2; file LGPL-2.1-or-later with generated-code exception, `sAndH` credited to Romain Michon. Mixed-license file; no other symbol license overrides reported for these calls. |
| noises.lib | 1.6.0 | `f1499112361800cd6689b7cc99e602d354aa4aeade78bd489cee327b92a8eb76` | `noise` and internal noise environment in GRAME section; LGPL-2.1-or-later with generated-code exception. |

Factory JSON reports the two extra libraries and author metadata; no STK filter dependency or sample assets in this wrapper. The generator source files are not redistributed here. The [compiler and library distribution obligations](faust-dependency-audit.md) remain open as a deferred owner-directed follow-up; this research is not a legal clearance claim.

## 4. Historical graph integration proposal

The implementation now follows this proposal with the actual stable keys documented in [Faust instrument nodes](faust-instrument-nodes.md); the compiler release compliance follow-up is deferred separately by the owner. The following describes the historical proposal rather than current UI.

Follow existing node architecture, not a separate audio engine. A future `PhysicalPluckGenerator : SignalNode, IGenerator` should use a stable type key (proposed `generator.physical-pluck`), typed Trigger (ordinary direct Trigger/Clock connection, no Schmitt detector for typed pulses), an optional Audio excitation input, and single Audio output. The nearby [EnvelopeGenerator](../../src/Orbit.Domain/Nodes/Generators/EnvelopeGenerator.cs) demonstrates event input; [NoiseGenerator](../../src/Orbit.Domain/Nodes/Generators/NoiseGenerator.cs) demonstrates audio-output generator style; [ResonatorFaustEmitter](../../src/Orbit.Audio/Compilation/Emitters/ResonatorFaustEmitter.cs) demonstrates an ordinary parameterized physical emitter. Register once in BuiltInNodes and BuiltInFaustEmitters; do not expose a processor bypass-through by default because excitation is not a dry signal. Ordinary mute uses the graph compiler's existing mute contract. All feedback/delay state remains private to this DSP, with no illegal graph cycle.

Use normal parameter definitions and stable parameter IDs: semitones -21 to +15, strike strength 0–1, decay .3–2 s, and optional independent output gain. `FaustNodeContext.Parameter` smooths only base sliders and adds continuous modulation; no smoothing across the sampled incoming Note Source pitch/velocity values. `NoteSourceFaustEmitter` derives its pitch, velocity and trigger from the same action sample. Use `context.Input` for typed Trigger and audio excitation, and `context.Parameter` for bounded numeric controls. Keep state per node, using an inline local Faust function expression or an explicitly managed unique helper definition; no global shared state or recompiles on note events. The graph already imports stdfaust.lib. Handle graph mute, init-at-target, parameter history and persistence like other generators. Do not advertise dynamic continuous pitch bend while pitch is latched. The meter should show actual captured pitch rather than falsely implying that later untriggered modulation retunes sound; until implemented, distinguish input and held pitch in UI. Output range is unknown unless proved, independent of the tested peaks.

Example **after implementation** (not available in current UI): set Note Source reference A4, connect Pitch → Pitch offset (unity), Trigger → Trigger, Velocity → Strike strength (unity with strength base 0), Audio → Mixer/Master. For Manual Trigger or Clock use fixed strength .8 and a fixed pitch or Pitch Control. A separate Resonator/Modal composite after the pluck is the next body/cabinet experiment; use measured or deliberately labelled candidate mode frequencies, decays and weights, not a claimed acoustic-guitar replica. Keep a dry component for direct string radiation, compare filtered body-processed variants at matched level and preserve all previous preset identities.

The first integration adds actual domain/persistence/registry/parameter tests and generated-graph WASM fixtures with actual Note Source events. Further work includes fallback-specific integration coverage, exact dry reference graph A/B, 7.3.4 benchmarking and user audition. Research wrapper tests prove the DSP contract, not all application integrations. Multi-voice allocation is separate 7.2.6c.

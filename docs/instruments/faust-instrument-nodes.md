# Faust instrument nodes — Phase 7.2.7c–f

[Realistic instrument design](realistic-instrument-emulation.md) · [Physical Pluck calibration](faust-physical-pluck.md) · [Usage](../usage/sound-and-instruments.md#faust-instrument-nodes) · [Roadmap](../roadmap/advanced.md#phase-727--faust-first-physical-models-planned)

**Status:** implementation in review, [PR #81](https://github.com/wgabel/orbit2/pull/81). Three new monophonic generator candidates and one reusable effect. These are technically tested candidates, not listening-approved acoustic replicas. Existing presets and node IDs remain unchanged. The existing Faust compiler distribution-compliance work is explicitly deferred by project owner; no new dependency/version or copied upstream model source is introduced, and the [audit](faust-dependency-audit.md) remains open.

## Play the instruments

Add Note Source, choose reference A4 (MIDI 69), then an instrument and Master output. The three new generators live in Audio generators; Rotary Speaker lives in Effects. Start playback and arm the Note Source keyboard. All pitch offsets are semitones from A4, with base Pitch 0 and connection amount 1. Each generator is mono, as is the existing graph. More independent notes require multiple voices or future polyphony.

| Note Source output | Physical Pluck / Physical Marimba | Drawbar Organ |
| --- | --- | --- |
| Pitch | Pitch (A4) | Pitch (A4) |
| Trigger | Trigger | Trigger (optional for individual legato key contact) |
| Gate | Not connected | Gate (required) |
| Velocity | Strike strength (set base to **0**, amount 1) | Not connected; use Swell for expression |

Connect Audio to Master (or Gain/Mixer if layering). Use the corresponding node's preset menu to compare five starting points; with the velocity wire, reset the strike strength to zero after selecting a preset. With fixed pitch and strength, Manual Trigger or Clock can play either percussion voice without a Note Source. For auditioning, connect the same typed signals of [TEST Melody Driver v1](../examples/instrument-design-and-audition.md) and compare at matched level against the original instrument templates.

## Physical Pluck (`generator.physical-pluck`)

Uses Faust `pm.ks`, an internally fed-back physical string waveguide, and the [validated rate-aware calibration](faust-physical-pluck.md). Pitch is bounded C3–C6; trigger captures pitch and strength on the same sample and excites a 6 ms windowed noise burst. Decay 0.3–2 s (fundamental T60), output gain 0–1. Optional independent audio Excitation is clipped and loss-scaled; it can sustain the string even without triggering. No note-off gate truncates the ringing state. A newly captured pitch retunes the single existing waveguide; independent overlapping strings require polyphony. An acoustic guitar or harp body remains a separate resonance-design task.

## Physical Marimba (`generator.physical-marimba`)

Uses the pinned Faust `pm.marimbaBarModel` with its 50-mode scalable bar data and five excitation positions. A mallet impulse is filtered by hardness (cutoff 700–9700 Hz) and feeds the bar. Pitch C3–C6, strength 0–1, hardness 0–1, strike position 0–4 rounded to the nearest position, decay 0.15–1.5 s and output gain 0–1. Pitch, position and hardness capture on a strike; velocity scales that impulse; decay and trim remain live. Gate is not required; tails naturally decay. This is a bar model, not a complete measured instrument or tube/cabinet sound. High modes are masked by the upstream model above Nyquist. Retuning a ringing bar changes its modal frequencies; independent bars require polyphony. The 50 modes cost more than the pluck; device-specific workload benchmarking remains open.

## Drawbar Organ (`generator.drawbar-organ`)

Nine independently adjustable drawbars (0–8), with ratios .5, 1.5, 1, 2, 3, 4, 5, 6, 8 relative to 8′. Default 888000000. Each is a sine partial with a continuous approximate logarithmic gain taper (zero exactly silent, eight maximum); maximum individual partial amplitude .1. Pitch A1–A6, upper partials limited to below 0.45 × sample rate. A 4 ms gate attack and 12 ms release prevent abrupt amplitude discontinuities; held notes sustain. Swell scales the whole sound, independent of velocity. Optional filtered key click (7 ms) and exponentially decaying third-harmonic percussion (350 ms) react to trigger or gate rising edge. Percussion is retriggerable in this mono model, not a replica of a vintage single-trigger organ. No shared tonewheel bank, foldback, leakage, nonlinear contact details, independent voices or measured pipe ranks. This is a tonewheel-style organ, not a pipe organ.

## Rotary Speaker (`processor.rotary-speaker`)

Connect Drawbar Organ Audio → Rotary Speaker Audio → Master. It also works with any other audio source. A complementary first-order 800 Hz crossover computes bass by low-pass and treble as input minus bass, so stationary bands recombine without a crossover hole. Each band uses a separate rotor phase, amplitude pattern and bounded fractional delay; Doppler travel is up to ±0.9 ms bass / ±0.45 ms horn around 2 ms. Speed is continuously modulatable: 0 brake, 1 slow (.72/.77 Hz), 2 fast (6.4/6.9 Hz). Exponential acceleration/deceleration time constants are 2.0/1.17 s bass and .33/.27 s horn (about 95% transition in three time constants). Both start stationary and accelerate to target. Amplitude depth and Doppler depth are independent controls; wet mix 0 is exact dry, and normal Orbit bypass is supported. Brake freezes rotation rather than bypassing cabinet coloration. No stereo microphone simulation or cabinet EQ yet.

## Verification

All nodes use the normal registry, stable type/parameter keys, generic ports and Faust graph emitters; no new audio engine. `tests/compiler/PhysicalInstruments.test.mjs` compiles the exact shared production cores and ordinary Note Source graph fixtures at 44.1/48 kHz. All 14 tests passed locally with the pinned compiler; they check initial silence, pitch/velocity capture, damped ring, footage isolation/held tone/release, real note event playback, marimba pitch/velocity/hardness and rotary dry transparency/neutral crossover/bounded modulation. All 251 domain and 124 presentation tests passed, including registry, independent controls, persistence, live parameter updates, bypass policy and 20 new preset definitions. The pluck's 222-trial research baseline remains separately reproducible. No subjective realism or browser/mobile capacity claim is made: listen against the existing templates and refine before treating any as an approved instrument. The flute remains research-only due to lower-register tuning/mode failures; follow-up investigate stable wind model before naming a playable flute.

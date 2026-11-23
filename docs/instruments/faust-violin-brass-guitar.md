# Faust violin, brass and electric-guitar candidates — Phase 7.2.7f

[Instrument nodes](faust-instrument-nodes.md) · [Faust-first design](realistic-instrument-emulation.md) · [Usage](../usage/sound-and-instruments.md#new-bowed-brass-and-electric-guitar-candidates-pr-86) · [Roadmap](../roadmap/advanced.md#phase-727--faust-first-physical-models-planned)

**Status:** [PR #86](https://github.com/wgabel/orbit2/pull/86) in review. Faust model integration using the existing pinned compiler. The Physical Violin is **experimental**, because prior investigation found octave/register jumps. These candidate models have not been approved as realistic instruments by listening. Compiler distribution compliance remains deferred by owner decision.

## Models and patch wiring

| Node | Faust function | Note Source connections | Character controls |
|---|---|---|---|
| Physical Violin (experimental) | pm.violinModel | Pitch + Gate; optional Velocity to Expression | Bow pressure 0.1–0.9, position 0.1–0.9, attack/release and vibrato |
| Physical Brass | pm.brassModel | Pitch + Gate; optional Velocity to Expression | Lip tension 0.25–0.75, mute opening 0.2–0.85, attack/release and vibrato |
| Physical Electric Guitar | pm.elecGuitarModel | Pitch + Trigger; optional Velocity to Pluck strength | Pluck position 0.05–0.95, string sustain 0.6–1 |

Use Note Source with reference A4 and unity Pitch connection. Set the Expression or Pluck strength base to 0 when using Velocity input at amount 1. Connect Audio through Gain/Mixer to Master. Each node has five candidate presets. Sustained models use their own gate envelope; guitar triggers a short string excitation and rings without a held Gate. All voices are monophonic. Electric Guitar is an unamplified model, not a complete pickup/amp/cabinet chain.

## DSP behavior

- **Violin:** bowed string, bridge and body model. Bow expression controls modeled velocity; pressure and position are independently modulatable. Raw string length follows 340 divided by intended note Hz. Earlier register jumps remain unresolved; do not advertise fixed tuning.
- **Brass:** lip-reed and resonant tube driven by continuous air pressure; lip tension and mute opening can alter tone and pitch. Default pitch-to-tube mapping is preliminary and must be checked at both standard sample rates.
- **Electric Guitar:** six-millisecond windowed-noise strike, with trigger-latched pitch, strength and pluck position. Live string damping, a bound on excitation energy, and output soft limiting. No body, amplifier, cabinet, distortion or polyphonic independent strings.

## Verification and acceptance

The native graph probe emits both the exact production DSP and a Note Source → instrument → Master graph. The new test suite compiles and renders at 44.1 and 48 kHz, checks finite output, idle silence, bounded peaks and notes/retriggers, and prints preliminary pitch diagnostics.

**Pending strict gates:** full C3–C6 chromatic tuning (especially D4 violin and repeat A4), reliable model oscillation under control extremes, measured decay and articulation behavior, consistent pitch across rates, CPU/device capacity, and equal-loudness human A/B comparisons. Do not promote the violin from experimental until octave jumps are absent. Source-code compilation and a clean CI run are necessary but not sufficient for an acoustic-realism claim. Preserve existing sound candidates and preset IDs.

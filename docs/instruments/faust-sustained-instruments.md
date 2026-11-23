# Physical Clarinet and Physical Flute — 7.2.7f

[Instrument nodes](faust-instrument-nodes.md) · [Faust-first design](realistic-instrument-emulation.md) · [Usage](../usage/sound-and-instruments.md#faust-instrument-nodes)

**Status:** implementation in review ([PR #82](https://github.com/wgabel/orbit2/pull/82)). Two monophonic wind candidates using the existing pinned Faust compiler 2.90.0, libraries 2.74.2 and physmodels.lib 1.2.1. Both model the interaction of nonlinear excitation and acoustic propagation; neither is a fixed oscillator/filter approximation or a listening-approved replica. No new runtime dependency.

## Playing and modulation

Add Physical Clarinet or Physical Flute from Audio generators. Wire Note Source (reference A4) Pitch → Pitch (A4) at unity, Gate → Gate, and Audio → Master. No Trigger or external envelope is required. For velocity-sensitive sound, wire Velocity → Expression at unity and set Expression base to 0 after selecting a node preset. Alternatively, leave Expression at its default .8 and modulate it from a slow oscillator or other modulation source while holding Gate. Choose one of five starting presets per instrument.

| Control | Range | Behavior |
| --- | --- | --- |
| Pitch | −21 to +15 semitones (C3–C6) | Base A4; short 4 ms legato tracking, clamped at range limits including vibrato |
| Expression | 0–1 | Live breath-pressure and output control; zero silences output |
| Reed stiffness (clarinet) | 0–1 | Maps to reed table slope .2–.5 |
| Breath noise (flute) | 0–1 | Adjusts noisy pressure component .003–.015 |
| Attack | .015–.4 s | Linear excitation/output rise |
| Release | .02–.5 s | Linear fall to silence; internal resonator can retain state |
| Vibrato depth / rate | 0–30 cents / 2–8 Hz | Sine pitch movement, off by default |
| Output gain | 0–1 | Post-model trim |

Gate starts and releases the breath envelope. Legato changes the same bore's pitch without restarting that envelope. Model startup can add to the configured attack. All eight parameters support ordinary Orbit modulation. As with the other mono models, independent simultaneous notes require separate voices.

## Model and calibration

The exact shared production DSP is in `src/Orbit.Audio/Compilation/FaustSustainedInstruments.cs`. The nonlinear acoustic feedback stays internal to the Faust library model. Effective lengths were fitted to the pinned implementations, not taken as universal physical dimensions. With target Hz `f`, sample rate `Fs`, normalized expression `e`, character `c` and gate envelope `g`:

- Clarinet: `pm.clarinetModel`, length `340/f + .101 - 4760/Fs`, pressure `g*(.55+.18*e)*(1+.006*noise)`, reed slope `.2+.3*c`, bell opening .5, followed by a DC blocker.
- Flute: `pm.fluteModel`, length `504/f - .359 + 3600*(1/44100-1/Fs)`, embouchure .6, pressure `g*(.75+.10*e)*(1+(.003+.012*c)*noise)`. The changed embouchure and rate-dependent length correction resolve register jumps in the earlier .5-embouchure experiment.

Both outputs use a bounded soft normalization `0.65*raw/sqrt(1+raw*raw)*g*e*gain`. This also gives exact idle/release silence; no external ADSR is needed. Expression below .25 is available but not guaranteed to sustain across the full range. Neither model deliberately overblows into a new register. There is no realistic tongue/reed articulation selection, modeled breath controller, or hall response yet.

## Verification

The C# graph probe emits both the exact DSP core and a real Note Source → instrument → Master graph, using the public registry and production compiler. `tests/compiler/PhysicalSustainedInstruments.test.mjs` renders at 44.1 and 48 kHz using 128-frame blocks.

| Model | Sample rate | Trials | Maximum absolute tuning error | Minimum active RMS | Maximum peak |
| --- | ---: | ---: | ---: | ---: | ---: |
| Clarinet | 44100 | 333 | 1.82 cents | .0668 | .4501 |
| Clarinet | 48000 | 333 | 2.21 cents | .0679 | .4551 |
| Flute | 44100 | 333 | 2.06 cents | .0537 | .4503 |
| Flute | 48000 | 333 | 1.35 cents | .0576 | .4502 |

Each group covers all 37 semitones × 3 expression levels (.25, .65, 1) × 3 independent character settings (0, .5, 1), checking the shortest strong autocorrelation period, bounded finite signal, idle and release. The real Note Source regression tests held A4, overlap E5, fallback to A4, release and repeated A4 with higher velocity. Short articulation (250 ms), live-expression change and 18-cent vibrato also passed at both rates. All 12 compiler, 255 domain and 124 presentation tests passed locally. These measurements establish playable tuning, not timbral realism. Compare via the existing [TEST Melody Driver](../examples/instrument-design-and-audition.md), level matched and dry first. Browser/mobile cost and listening approval remain open.

### Bowed string research

`pm.violinModel` was also tested with calibrated length and multiple bow speeds/pressures/contact positions. Several configurations still jumped into an octave on specific chromatic pitches, including D4, and some repeated A4 events. Do not register that wrapper or advertise an improved bowed string until those cases pass the same range and event tests. Investigate stable bowing regions, onset and bridge/body radiation next.

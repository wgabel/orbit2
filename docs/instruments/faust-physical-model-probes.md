# Native Faust physical model probes — Phase 7.2.7b

[Faust-first design](realistic-instrument-emulation.md) · [Pinned artifact/license audit](faust-dependency-audit.md) · [Roadmap](../roadmap/advanced.md#phase-727--faust-first-physical-models-planned) · [Audition workflow](../examples/instrument-design-and-audition.md)

**Status:** local offline engineering proof of concept, partial. Neither candidate is a registered Orbit node, shipped instrument or listening-approved preset. The production distribution gate in 7.2.7a remains open. No generated DSP binaries, vendor source files or audio are checked into the repository.

## Reproduce

Use the exact audited package, not latest Faust. From repository root with Node.js, install the already pinned package as described in README or pass the extracted package directory from the audit.

```sh
npm install --no-audit --no-fund --ignore-scripts --no-save @grame/faustwasm@0.19.0
node scripts/physical-model-probes.mjs node_modules/@grame/faustwasm /tmp/orbit-physical-probes
```

The [standalone experiment](../../scripts/physical-model-probes.mjs) verifies the browser bundle SHA-256, compiles both three-input/one-output parameterized cores with `-single`, records factory dependency metadata and renders via the pinned `FaustMonoDspGenerator.createOfflineProcessor` (128-frame blocks). The generated outputs are `physical-model-report.json` (version/metadata, exact pitch inputs, metrics, timings, dynamic tests and match gains) and `physical-model-audition.wav` (52 seconds, mono, 48 kHz PCM). No new runtime dependencies or production code.

### Stimulus and calibration

Six isolated notes A3, C4, E4, A4, C5, E5 at 220, 261.626, 329.628, 440, 523.251, 659.255 Hz; two seconds per note including 100 ms pre-note silence. Fixed seeded noise in every excitation. Render on 44.1 and 48 kHz; fresh instance for each isolated note. Separate tests exercise state reuse within a render. Measure unscaled output for finite values, peak, pre-note idle, RMS during early/hold/end windows, and estimated fundamental (normalized autocorrelation near target, rejecting weak periodicity). A pitch estimate is not a guarantee of perceptual pitch or mode identity. Raw frequency-to-length conversion is compared to a bounded grid/bracket search for each test note. The search is an offline diagnostic, not an algorithm to run in the audio thread; it does not guarantee a valid mode or convergence. One cent = 1/100 semitone.

```faust
import("stdfaust.lib");
process(f,d,e) = pm.ks(pm.f2l(f),d,e); // pitch Hz, damping, excitation input
```

```faust
import("stdfaust.lib");
process(f,m,p) = pm.fluteModel(pm.f2l(f),m,p); // pitch Hz, mouth position, pressure
```

Pluck: six-millisecond windowed noise burst at strength .08, damping .04; compare .10 damping and a second excitation on the same state at 800 ms. Flute: mouth position .5, pressure .85 with 60 ms ramp-up and 100 ms ramp-down, 2% seeded noise perturbation, and separately a pressure variation of ±.035 at 3 Hz. Also sweep steady pressure .5/.7/.75/.85/.9 and ramp the pitch input from calibrated A4 to C5 over 300 ms while blowing. Pressure is not simply linear gain.

## Observations (2026-11-23)

Node v24.22.3, Linux x64, AMD EPYC 9V45; compiler 2.90.0. Both cores compile with their same minimal-probe dependency lists and per-function metadata as the audit (now dynamic signal arguments rather than constants). The wrappers generate test inputs in the host, not extra Faust imports. All 24 calibrated isolated outputs, expression variants and dynamic exercises were finite. The following values are measured, rounded; timings are from the final run and may vary.

| Rate | Model | Raw pitch error range (cents) | Calibrated pitch error range (cents) | Max raw-amplitude calibrated peak | Offline median per 2 s |
|---|---|---:|---:|---:|---:|
| 44.1 kHz | Pluck | −0.39 to −0.12 | −0.39 to −0.12 | .280 | 2.24 ms |
| 48 kHz | Pluck | +4.39 to +13.37 | −0.96 to +0.93 | .242 | 1.52 ms |
| 44.1 kHz | Flute | −267 to +285 | −0.95 to +0.89 | .917 | 3.54 ms |
| 48 kHz | Flute | −253 to +634 | −0.91 to +100.92 | .917 | 3.98 ms |

Each render had exactly zero pre-note peak. Final 150 ms RMS was at most .000757 for pluck and .00000125 for flute; the pluck intentionally retains a decaying tail, not an artificially gated zero. Retrigger remained finite, with second-strike RMS .0424 at 44.1 kHz and .0356 at 48 kHz. The flute dynamic sweep settled near 523.17/522.99 Hz, but the transient and quality of legato remain unvalidated. Offline timing is median of five two-second vendor render calls after one discarded call, including output allocation/copy and excluding input creation/instance setup; it is not a live Worklet deadline, a device capacity measurement or a benchmark against the full Orbit graph. Use the [7.3.4 benchmark](../quality/dsp-profiling.md) for that follow-up.

### Pluck — advance as a candidate

Length calibration is nearly trivial at 44.1 kHz over the tested range; at 48 kHz the uncorrected tuning is slightly sharp, so a sample-rate-aware pitch mapping is required. With the test calibration, all six notes at both rates are within one cent. Damping materially changes decay, and high notes decay faster at the same damping; expose a musically meaningful decay/time mapping rather than assuming damping is the same duration for every pitch. String excitation/noise must be bounded. One waveguide alone lacks body radiation, string stiffness, fret/hammer sounds, multi-string coupling and the full resonant signature of a guitar or other named instrument. First proposed local integration prototype: gated/triggered generic **Physical Pluck**, external excitation optional, latched strike velocity, pitch changes handled explicitly for ringing notes, damping and output trim. Do not name it Acoustic Guitar yet.

### Flute — useful mechanism, incomplete playable range

At calibrated A4, hold-RMS for pressure .5/.7/.75/.85/.9 was about .0047/.0208/.334/.578/.674 at 44.1 kHz and .0044/.0610/.336/.578/.674 at 48 kHz. Below threshold the model may be dominated by breath noise; an unshaped 0–1 expression control would have a large ineffective zone. A held gate needs a shaped pressure envelope, bounded noise and an expression mapping around the reliable oscillation region. A physical oscillation can take time to build; avoid promising a uniform fast onset.

At 44.1 kHz, the offline calibration reached all six test targets within one cent. At 48 kHz only E4 through E5 passed the one-cent test; A3 and C4 remain roughly +101 and +48 cents even after the search. Independent spectral inspection of the actual 48 kHz audition confirmed the low-note fundamental peaks near 233 and 269 Hz, not the intended 220 and 261.6 Hz. Exploratory changes in tube length and pressure produced abrupt mode changes in the lower register, so simply correcting a linear pitch offset is not sufficient. The raw autocorrelation estimate for one C4 attempt is an octave/mode warning, not a confident pitch measurement. A more robust search over embouchure, tube length, pressure, seed and attack shape is needed; verify the intended fundamental and harmonic mode across sample rates and all keyboard notes, not just the selected pitches. Until then limit research to the verified mid-register and do not implement a broadly playable flute preset.

## Audition procedure and limitations

The WAV has four twelve-second groups separated by one second of silence:

| Time | Voice |
|---|---|
| 0:00 | Dry synthetic pluck (felt-like pluck recipe) |
| 0:13 | Physical pluck |
| 0:26 | Dry synthetic flute (Orbit template partials and approximate band-pass noise/ADSR) |
| 0:39 | Physical flute with pressure movement |

The synthetic references are independently rendered dry approximations of the [felt-like pluck recipe](../examples/sound-quality-and-voice-design.md) and [Flute template](../../src/Orbit.Application/Graphs/InstrumentTemplates.cs), not exact Orbit graph-compiler output; no reverb, gain staging or master processing is reproduced. The notes and excitation timing are fixed; physical resonant attack and release intentionally differ. Gain is matched once per full voice across the active windows using RMS (not perceived loudness); cap output peaks at .85. At 48 kHz the pluck pair matches .06313 RMS and flute pair .075 RMS. The final 20 ms of each isolated audition segment fades for edit boundaries; technical metrics are measured before this export fade or gain. Listen at a comfortable level; compare timbre and articulation, not just loudness. The flute first two notes are mistuned, and the physical pluck decays differently from the synthetic reference. Audio quality has not been rated by a human listener. The user should identify which physical timbres seem more convincing and which artifacts remain.

Next: add full native Orbit dry graph baselines and render against the TEST Melody Driver for exact A/B; establish repeatable sample-rate tuning, bounded excitation and performance under the full app. Evaluate other pressure/embouchure combinations or a legally audited contrasting wind model if the flute remains unreliable. Complete distribution compliance before production integration, and preserve existing synthetic preset identities. Do not label a voice realistic based on objective measurements alone.

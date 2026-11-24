# Sound-design foundation 02 — multilayer industrial and evolving bass

[Foundation catalogue](catalogue.md) · [Repeatable audition](../instrument-design-and-audition.md) · [Voice design](../sound-quality-and-voice-design.md) · [Mixing](../mixing-mastering-and-analysis.md)

## Evidence and purpose

2026-11-24: the listener described *Beneath the Aurora* v2 as better but flat, with insufficient melodic variation and bass that was present but could be more prominent. The first Bass Lab's five candidates were all rated OK, not good. The listener requested more layers. Bass Lab II deliberately compared five complementary branches per voice at matched audition levels. The user rated **Iron Tide and Abyssal Machinery Great**, Tectonic Bloom Good, Molten Circuit OK and Obsidian Cathedral Bad. These are ratings of the delivered independent-preview experiment; native Orbit playback, playback device, and separate ratings of the solo and backed excerpts were not established. Do not claim a finished composition or native-verified preset.

| Candidate | Listener rating | Pitched oscillators / nodes | 250–8000 Hz power | Solo / context sample peak (dBFS) | Source patch SHA-256 |
| --- | --- | ---: | ---: | --- | --- |
| Tectonic Bloom | Good | 10 / 48 | 10.33% | −7.99 / −4.21 | `06bf9e2981b4d557c634e3c7a38d6b9a9e3bb0d5031c1517272952f89c7fcb6d` |
| Iron Tide | Great | 11 / 49 | 16.26% | −7.49 / −3.29 | `c6a98cc06224595993e8fe464be89038f239c28b6a4221cc9b9e80fdee1b41b7` |
| Obsidian Cathedral | Bad | 12 / 49 | 14.71% | −10.03 / −5.27 | `9de7bcac7c0399d87c9d5ad7172d3005e44cc29b68e121e40ee1f62ee4cf5aea` |
| Molten Circuit | OK | 11 / 51 | 9.91% | −6.95 / −4.41 | `e3487e479bf5646cdde3b9066558d2a69755fdfc28d4886178cad7ce43702b26` |
| Abyssal Machinery | Great | 12 / 51 | 15.73% | −8.46 / −3.69 | `79e0bcc00c6e514119ea1eac2c34977a586f7e2961140d3a842ab87916fd448a` |

The recorded analysis is from `Bass-Lab-II-analysis.json` (source SHA-256 `a8dc19ffc10640846d0e908bfe8cdf944ae1ec7e0a5a07d21549935d1a1603d7`). Values are extracted from that report; source patches and MP3s were delivered in the chat. Repository reference copies below reconstruct the inspected source graph's parameter values, connections and canvas placement with new IDs; they are **not byte-identical originals** and have not themselves been rendered or app-import tested. The original source hashes identify the auditioned candidates, not these copies.

## Reusable reference graphs

- [Iron Tide — reconstructed solo patch](bass-lab-ii/iron-tide.orbit2.json): 49 nodes, 63 connections.
- [Abyssal Machinery — reconstructed solo patch](bass-lab-ii/abyssal-machinery.orbit2.json): 51 nodes, 66 connections.

Both are schema 1 data-only patches; use GitHub raw/download, stop transport, import via Patches → Import JSON, and play from transport zero **at a low monitoring level**. The 12-second phrase loops. The common backing was present only in the independent previews, not in the solo patches. The original analysis checked unique IDs, endpoints, finite connection amounts, acyclic reachability and one sequencer; the reconstructed graphs have matching node/edge counts but native deserialization, compiler, playback, true peak and mobile CPU are pending. A 24 Hz high-pass and conservative final Gain are included; these do not guarantee native headroom.

## Design relationships to reuse

Each branch receives the same sequencer Pitch at unity, using the oscillator's semitone tracking. Gate drives separate ADSR profiles. The pitched oscillators use base frequencies relative to A4 (440 Hz), so detuning and octave ratios follow the note. An unmodulated sine + triangle anchor is filtered at 145 Hz (Q .72). Keep this foundation stable while creating moving midrange and upper harmonics in separate branches. Separate branch filters and faders allow weight and character to be adjusted independently rather than making the whole sound louder.

| Branch | Iron Tide | Abyssal Machinery |
| --- | --- | --- |
| Sub | Stable sine/triangle; LP 145 Hz | Same |
| Body | Three saws at −20, +16, +1210 cents; LP 830 ±390 Hz, Q 1.1, 0.18 Hz sweep | Three saws at −11, +13, +1196 cents; LP 680 ±360 Hz, Q 1.05, 0.12 Hz sweep |
| Pulse texture | Near-root/octave PWM pair; BP 450 ±230 Hz, Q 2.4, 0.32 Hz sweep | Near-root/octave PWM pair; BP 390 ±190 Hz, Q 1.65, 0.26 Hz sweep |
| Resonant detail | Saw + pulse at +19/+24 semitones; BP 1350 ±670 Hz, Q 2.8, 0.21 Hz sweep | Triangle + pulse + sine at +19/+24/+31 semitones; BP 910 ±430 Hz, Q 1.8, 0.17 Hz sweep |
| Edge | Octave saw + two-octave pulse, HP 600 Hz, short envelope | Saw/pulse at +24.13/+31 semitones; BP 1850 ±780 Hz, Q 2.5, 0.31 Hz sweep, short envelope |

The independently modulated pulse widths alter the overtone distribution; detuned saws create beating; separately swept resonant filters make different spectral regions emerge and recede; independent slow gain LFOs vary branch relationships. These are feed-forward interactions through summation, not feedback or proof of audio-rate cross-modulation. The long ADSR is 32/320 ms, sustain .82, release 210 ms; the slower swell is 105/460 ms, sustain .84, release 260 ms; the accent is 8/220 ms, sustain .22, release 105 ms. Iron Tide uses long articulation for the body and pulse branch; Abyssal uses the slower body envelope. Output trims are approximately .3633 and .3707 respectively.

The first render of the second experiment exposed quiet upper branches. The iteration adjusted oscillator amplitudes to target branch K-weighted RMS relative to the sub: body −3 dB, pulse −6 dB, resonant layer −8 dB, edge −12 dB, subject to source/peak constraints. The final independent measurements were approximately −25.31/−28.31/−31.31/−33.31/−37.32 dBFS for Iron Tide, and −25.14/−28.14/−31.14/−33.14/−37.14 dBFS for Abyssal Machinery. These are specific audition balance starting points, not mandatory mix targets.

## Repeatable stimulus and measurements

80 BPM, 4/4, 32 eighth-note steps (375 ms), 13 attacks, note length .88, velocity .8, A4 reference. Each row is one bar; dash is tie, R rest.

| Bar | Bass score | Common backing chord |
| --- | --- | --- |
| 1 | D2 – – R A1 – D2 R | Dm |
| 2 | Bb1 – – – F2 R Bb1 R | Bb |
| 3 | G1 – G2 R D2 – Bb1 R | Gm |
| 4 | A1 – – R E2 – A1 R | A |

Independent 48 kHz mono oscillator/envelope reconstruction used Fourier pulse/saw and RBJ biquads with coefficients updated every 128 samples, not the native Faust state-variable filter and scheduler. Each preview: 12 seconds solo, 1 second silence, 12 seconds with identical synthesized backing; all five solo signals approximately −22.5 dBFS K-weighted RMS (comparison proxy, **not LUFS**). Bass vs backing approximately −3.67 dB K-weighted RMS; no claim about perceived partial loudness. Spectral percentages from Welch power, 16384 samples per segment, referenced to total power. First experiment mean 250–8000 Hz power was approximately 3.47%, but changes in multiple sound-design variables prevent causal attribution. The final previews were checked for sample clipping; true peak was not measured.

## Lessons and next applications

1. **Prefer a complementary palette to a louder stack.** A stable sub can provide weight, while separately articulated, beating and filtered upper branches communicate motion on less bass-capable playback systems. Bring each intended branch to a meaningful level and check its contribution to the combined sound; change one relationship at a time in future controlled comparisons.
2. **Layer count and spectral energy are not quality metrics.** Obsidian Cathedral had 12 pitched oscillators, the same five-branch strategy and 14.71% upper-band power, but was rated Bad. Its additive/reed-like design is a rejected candidate for this listener, not proof that organs, additive synthesis or the measured band are undesirable. No specific reason was supplied. Iron Tide and Abyssal Machinery share detuned saws and PWM with independent slow spectral motion; their success is a useful preference hypothesis, not proof that any individual component caused approval.
3. **Use these as candidates, not compulsory presets.** For *Beneath the Aurora*, audition Iron Tide as main moving bass and Abyssal Machinery during atmospheric passages. Try Tectonic Bloom when a warmer palette fits. Preserve approved melodies where applicable, but develop actual phrase rhythm, countermelodies and macro contrast; a new bass alone cannot correct flat arrangement. Adapt the fixed phrase/register to the composition, manage available sequencer lanes, check sub/kick/drone overlap, keep the sub dry when appropriate, and compare contextual bass against simultaneous backing across sparse and dense sections. Measure native output and ask for in-context listener feedback before promoting the combination as composition validated.

Retain the original ratings when later revisions are tested. Do not silently substitute a reconstructed graph or changed settings for the exact source audition.

# Waveform Layer Labs I–VI — richness, interaction, space and expression

[Foundation catalogue](catalogue.md) · [Voice design](../sound-quality-and-voice-design.md) · [Contextual balance](../contextual-layer-balance.md) · [Composition development](../compositional-development.md)

Evidence: listener-approved independent DSP previews, 25 November 2026; not native Faust validation or universal quality claims. Labs I–IV received 22 Great ratings, with qualifications below; Lab V produced mixed ratings and explicit criticism of excessive pitch modulation; Lab VI's seven expressive alternatives were rated Great, control Good. The experiments were short excerpts, not validated full-length compositions. Archive fingerprints identify the exact delivered files; the binary/audio and patch archives remain in the original conversation, not committed here.

## Lab I — waveform layering
Fixed phrase, four oscillator layers (sine sub, triangle body, detuned saw growl, square transient), 80 BPM, 24 s, -20 dBFS RMS, dry. Static Stack — Great; Temporal Layers — Great; Spectral Motion — Great; Living Voice — Great. Listener: “They were all very good. The richness of the sound generated is what we must strive to achieve in all layers of a musical piece.”

A static complementary stack was also approved: complexity and modulation are not prerequisites for quality. Independent envelopes let the bright transient decay before the slower harmonic body blooms; moving filters alter the available harmonics during a note. Example Living Voice ADSR (ms, sustain fraction): sub 25/220/.83/170; body 55/340/.72/230; growl 130/330/.65/250; transient 8/160/.13/95. Body low-pass 440–960 Hz at .22 Hz, growl band-pass 260–1040 Hz at .31 Hz, transient high-pass 410–1070 Hz at .47 Hz. Keep sub stable and check the actual ranges and modulation units.

## Lab II — interacting layers
Same musical stimulus, 24 s, -20 dBFS RMS, no spatial effects, independent preview with 24 Hz high-pass. Harmonic Bloom — Great (envelope shapes amplitude and cutoff); Interference — Great (detuned saws create beating); Harmonic Breathing — Great (audio-rate triangle to saw gain); Spectral Exchange — Great (opposed gain/filter movement); Living Network — Great (combined). Sample peaks respectively -9.45, -8.58, -9.05, -8.20, -8.52 dBFS. Audio-rate multiplication produces sum/difference components; protect sub, use bounded gain offsets, verify headroom and avoid unintended graph cycles. No preference ranking was established.

## Lab III — instrument palette
D minor, 100 BPM, 19.2 s each solo/context, dry. Deep Bass — Great; Expressive Lead — Great; Harmonic Pluck — Great; Evolving Pad — Great; Complementary Melody — Great; Percussion — Great; Atmospheric Texture — Great; Full Ensemble — Great. The listener qualified the ensemble as OK while acknowledging its richness: too many sounds occupying the same region reduce punch and clarity; hip-hop can use just a few sounds occupying complementary spectral regions. Preserve this qualification rather than treating all ratings as interchangeable. Ensemble independent RMS -19 dBFS, sample peak -2.38 dBFS; 181 nodes, 236 connections, native CPU unverified.

The palette demonstrates different roles: stable sub and detuned mid-bass; expressive lead with bloom; pluck with short transient/ringing tail; pad with independently articulated chord voices; mellow answer; pitch-enveloped kick and inharmonic percussion; slow upper harmonic texture. Internal richness is distinct from the number of simultaneous voices. Avoid forcing a full-spectrum stack into every role.

## Lab IV — richness and space
Same D minor framework at 100 BPM, 19.2 s, exact Lab III voice graphs reused with score rests, role trims, filtering and kick-envelope bass ducking. Matched RMS -21.73 dBFS and at least 3 dB sample-peak headroom; no spatial effects or limiter.

| Candidate | Rating | Mean active groups | Lead vs other midband median (dB) |
| --- | --- | ---: | ---: |
| Minimal Foundation | Great | 1.33 | — |
| Sparse Hip-Hop | Great | 1.62 | 11.40 |
| Interlocking Voices | Great | 2.09 | 13.18 |
| Dynamic Density | Great | 2.83 | 9.77 |
| Controlled Ensemble | Great | 4.59 | 9.50 |
| Lab III matched reference | not rerated | 4.59 | 6.76 |

Listener: “They were all great. The first one was clear and the last one was rich.” Activity uses 100 ms windows within 30 dB of each group's maximum RMS. Midband proxy is median active lead against quadrature sum of other group energies in 250–2000 Hz, not a perceptual masking metric or universal target. Multiple arrangement and mix variables changed together, so no causal isolation. Ducking example: kick pitch envelope → bass Gain modulation amount -0.75, base 1. Preserve intentional kick/sub timing; don't automatically duck every bass.

## Lab V — expressiveness and humanization

Same Lab III bass, drums, lead and answer source graphs; fixed bass and drums, 100 BPM, D minor, 19.2-second independent DSP previews, approximately -21 dBFS RMS. The lead and answer performance controls were implemented in the offline renderer, not serialized into new Orbit 2 patches.

| Candidate | Listener rating | Change |
| --- | --- | --- |
| Control | OK | Grid timing, fixed strength and nominal gate |
| Expressive Dynamics | Good | Note strength and brightness coupled |
| Variable Articulation | Good | Gate durations varied |
| Microtiming | Great | Deterministic onset offsets -13 to +23 ms |
| Pitch Expression | Good | 14-cent delayed vibrato, 5.1 Hz, approach bends up to 21 cents |
| Musical Conversation | Good | Dynamics, articulation and answer delays |
| Organic Performance | Great | Combined performance techniques |

Listener: “Although I did not hear the difference in timings I felt that this piece is so much better. The pitch modulation was too strong it felt like beginners attempt”

Interpretation: the listener preferred the microtimed and combined versions, even without consciously identifying timing changes. This is evidence for testing subtle, intentional timing relationships, not a universal finding about random offsets. Pitch expression received a positive rating but the modulation intensity was explicitly criticized. Keep pitch stable by default; test smaller, selective bends and delayed vibrato on sustained notes rather than indiscriminate pitch movement. Phrase-based dynamics and articulation remain useful candidates. Preserve the grid/unchanged backing as a comparison; do not equate adding modulation with expression. Proposed next comparison: subtle offsets, laid-back phrasing, anticipatory phrasing, contrasting timing by role, restrained pitch, phrase-level dynamics/articulation and their combination. Smaller pitch settings are hypotheses pending listening, not listener-validated values.

## Lab VI — controlled expression (offline listening research)

Same D minor score, 100 BPM, 19.2 s, matched RMS -21.04 dBFS, highest sample peak -4.63 dBFS; 36 melodic note events, fixed bass and percussion, deterministic timing. Control — Good; Subtle Humanization — Great; Behind the Beat — Great; Ahead of the Beat — Great; Elastic Groove — Great; Restrained Pitch — Great; Expressive Phrasing — Great; Mature Performance — Great. No further qualitative feedback or ranking was supplied.

The restrained pitch candidate reduced lead vibrato from 14 to 5 cents, answer vibrato to 3.5 cents, approach bends from 21 to 6 cents, delayed vibrato to 170 ms, 5 Hz, selectively applied to 20 of 36 events. Subtle timing offsets -10 to +16 ms, behind beat +12 to +21 ms, ahead -9 to -20 ms (boundary clipping as applicable). Results favor testing restrained expression and intentional timing; they do not prove a universal optimum or isolate every parameter. Lab V criticized stronger pitch motion; Lab VI's restrained candidate was rated Great.

**Owner requirement after Lab VI:** all subsequent Orbit 2 experiments must be realizable using currently implemented features, with an importable patch per candidate. Check the target revision and actual node/parameter/port contracts first; prefer native render. Independent previews may approximate only the serialized graph, not add hidden note scheduling, expression or effects. Explicitly report gaps and propose roadmap work for owner review; do not silently simulate missing capabilities. Labs V–VI are listening research only: expressive variations were implemented offline, not demonstrated in native patches.

### Capability checkpoint (main cc3faaa3da1f256ee6f978c69033bdfb76ca1b4f)

See [Note Sequencer](../../instruments/note-sequencer.md), [pitch](../../instruments/pitch-control.md), [envelopes](../../audio/envelopes.md) and [roadmap](../../roadmap/advanced.md). Per-note gate length merged (PR #111); per-note velocity data merged (#113), scheduling documented in review (#114), editor pending. Do not depend on that scheduling until verified merged/deployed. Existing Pitch, Gate, Trigger and modulation paths permit fixed pitch, independent sequencer rhythms, ties, rests, gate duration and graph-based timbral/pitch movement. Free timing and synchronized step timing are available, but arbitrary per-event signed offsets and capture of expressive timing are not established. Selective pitch gestures can be constructed with explicitly routed modulation lanes; there is no established per-note bend/vibrato event editor. Propose native per-note onset offsets, bounded deterministic groove profiles, and expressive pitch automation only after checking current roadmap; obtain owner approval before adding new features. Do not equate different grid subdivisions with true microtiming.

## Apply to new music
1. Assign musical roles and a genre-appropriate density before designing instruments. Choose sparse, dense or changing density intentionally; never equate more parts with better music.
2. Give important voices a deliberate internal identity: fundamental/body, selected harmonics, articulation and texture as appropriate. Consider static complementary stacks, independent envelopes, filter movement, detuning or bounded feed-forward audio-rate interactions. Use simple tones when musically appropriate. Compare at matched levels; prefer physical or composite models when their role warrants them.
3. Separate roles through register, temporal rests and dialogue, transient shape, dynamics, filtering and spatial placement where supported. Remove competing material before boosting a masked voice. Reserve low-end space for bass and kick; don't filter away the intended identity.
4. Audition solo and context; compare sparse and dense passages at representative windows, preserve motif and musical development independently from density automation. Report actual listener feedback, structural checks, native audio evidence and independent preview measurements separately.
5. Treat performance expression as separate from sound design: compare deliberate microtiming and phrase-based accents against a grid reference; maintain pitch stability and introduce vibrato or approach bends selectively. Lab V's excessive pitch modulation is a negative finding. Verify available event controls before claiming native patch support. All future experimental behaviors must be serialized using implemented Orbit 2 features.

## Evidence archive manifest
SHA-256 of original delivered ZIPs (conversation artifacts; no repository copies yet):
- Waveform-Layer-Lab-I.zip: 2cc8e213283ecf39d22c250e5913f22bb878d22813bcf3391cc944f997d00200
- Waveform-Layer-Lab-II.zip: aa42cb347018e3bc21a7d4e9ea08e08903599a7e70d2c37316ef1a5907e51bed
- Waveform-Layer-Lab-III.zip: 1add1fe03464ebe090a26702cbbef21e6dd26e32d25b214eacc175496ccd4d3a
- Waveform-Layer-Lab-IV.zip: 998e5f1ac452246b2da224e9406f03d391b513b0bcd4d06cc6ba0eb4ce948ca0
- Waveform-Layer-Lab-V.zip: 2623f0e3cf137d0368825366cc63e462ce46e8293a6381cfecb0056ff5047266
- Waveform-Layer-Lab-VI.zip: f89abe9b3743a2a6dfbb6c842e8fc7afa008dd47fc036c96a4d73e023da2574a

Each archive contains source JSONs and previews (Labs V–VI expression variants are offline render scripts, not new serialized graphs), with experiment/measurement metadata. Native import, actual Faust render, runtime performance and full-length composition validation remain pending. Do not reconstruct a graph from these prose notes and label it the original.
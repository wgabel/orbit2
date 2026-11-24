# Voice Lab II and III — expressive rhythmic phrasing and complementary melodies

[Composition guide](classical-composition-and-melody.md) · [Validation](musical-composition-validation.md) · [Sequencer design](../instruments/note-sequencer-design.md)

## Listener evidence and scope

2026-11-24. After Voice Lab I, the user found the versions interesting and preferred audible lower supplementary melodies and tones. The user requested greater variation in actual note duration and behavior, with smaller notes between important chord/melody notes. Voice Lab II tested varied eighth-grid durations and articulation; Voice Lab III developed the positively rated Rhythmic Dialogue using finer grids and separately articulated voices. All studies used 84 BPM, 4/4, D minor, eight bars (22.857 seconds plus a short preview tail) and a common synthetic keyboard-like palette.

| Experiment | Listener rating | Main / lower attacks | Sequencers / nodes | Original patch SHA-256 |
| --- | --- | ---: | ---: | --- |
| II Flowing Dialogue | Good | 34 / 39 | 10 / 74 | `ac23b6a82abc20cefd4a9dc600f74147e96a16fc6ac559f40f0980acf0c50301` |
| II Rhythmic Dialogue | Great | 42 / 44 | 10 / 74 | `8a6ee2abd60960c19672daafd59257381a273912b457b53411468432d678586c` |
| II Breath and Flight | OK | 34 / 46 | 10 / 74 | `3936296b771dfb838494212995ea18a4d64782f5f519b5c7ca7eb5054e1e730d` |
| III Interlocking Grids | OK | 42 / 58 | 13 / 95 | `f4358bb0c440bf89dd976459cf09f0a33bcd776ec7e1c955a9c8e46aa69dca9f` |
| III Contrasting Articulation | Good | 42 / 44 | 13 / 115 | `8e8622844da1e0a767b280ee83530aff18452961706a435fc08d83c8f440afa0` |
| III Elastic Dialogue | Great | 48 / 58 | 21 / 201 | `b14ef8c184caa39367699adaa4bf664b9e919971f5a33f0d25588b3668b7241f` |

Exact listener feedback on III: “Third one was very good. It had variation in note length in the main melody aintroduced additional notes between main ones. The underlying complimentary notes were also very good in the second and third piece.”

Voice Lab II feedback also singled out Rhythmic Dialogue as Great and said rhythmic variety was better, asking whether Orbit could influence individual sequence note lengths and whether differently timed sequencers could complement one another.

The ratings apply to the independently rendered previews, not native Orbit playback or every genre. The source files are `Voice-Lab-II-analysis.json` (SHA-256 `95f9f09196ff2ddb6b5bff94f9068e2916989c7022743e43da6386064fb740c3`) and `Voice-Lab-III-analysis.json` (SHA-256 `edee2e8370df61c5415859487c48c6cd5646b5aea8287460b5b43aa7b5d0eddc`). The six original JSON patch hashes above were checked against the delivered files. The full original patch exports, MP3s and expression automation arrays remain in the delivered assets; this document retains the exact Elastic Dialogue pitched-event score and articulation profiles, not a byte-identical importable patch.

## What the comparison teaches

- **Hierarchy of notes, not equal treatment of every pitch.** Establish important melodic destinations and a recognizable motif. Sustain an anchor where it matters, then subdivide another duration into a short passing, neighboring, repeated or approaching gesture. A note's structural importance depends on harmonic function, metric placement, duration, accent and contour, not simply its pitch or length. Diminution is shortening note values; ornamentation adds or changes intervening notes. Neither requires a faster global BPM.
- **Contrast rhythmic density locally.** Alternate longer notes and compact groups of shorter notes within a phrase. Use repeated-note attacks as deliberate rearticulation, not a tie. Design where the quick gesture starts and where it resolves; avoid indiscriminately inserting notes or making every voice busy. Augment or simplify another phrase for contrast.
- **Make the lower complementary melody a real musical line.** It should have its own rhythmic and melodic identity, register, phrasing and harmonic direction, rather than merely doubling the lead or repeating the bass roots. Give it enough presence to be recognized. An independent bass remains useful for the foundation. In the earlier experiment, the lower answer was measured -3 dB relative to the lead; in III it was -2.5 dB. These are reference mix choices, not mandatory loudness targets or proof of perceived audibility.
- **Separate articulation from notated duration.** A nominal eighth note can be a short detached strike or an expressive connected note; a gate defines excitation/hold timing, while envelope attack, decay, sustain and release define how a voice responds. A trigger-only instrument may ring past gate end. Shape dynamics and accents as well as lengths. Do not simulate all expression by gain alone.
- **Coordinate voices with a common musical clock.** A sustained upper note can leave space for lower ornaments; a busy upper gesture can be supported by a more stable lower line. Independent synchronized divisions are not necessarily different BPMs or polyrhythm. Use free-running clocks only when deliberate drift serves the piece.
- **Avoid causal overclaiming.** III-01 increases lower activity but retains the original lead score and standard envelopes; III-02 preserves the original pitched score while contrasting articulation and changing mix level; III-03 combines those ideas and changes the lead and reply. III-01 and III-03 have identical lower note-duration distributions, yet different ratings. The user's feedback supports the combination and identifiable lower parts, not a universal optimal note count, proof that faster notes alone improve music, or isolated proof of an envelope's effect.

For subsequent pieces select these relationships according to genre and section intent. A sparse drone, ballad or hypnotic groove can require fewer notes and slower change. The earlier Melody Lab I feedback explicitly rejects style-independent rankings. Prefer preserving approved anchors and lower-line relationships while writing new material appropriate to the piece, rather than copying this entire D-minor score.

## Exact Elastic Dialogue event reference

Harmonic framework by bar: Dm | Bb | Gm | A7 | Dm | F | A7(b9) | Dm. The following events are copied from the delivered analysis. Each token is `absolute sixteenth tick:pitch:nominal duration in sixteenth ticks`; zero-based tick 0 starts bar 1, tick 64 starts bar 5, tick 128 is the loop boundary. Unoccupied time is rest. Repeated pitch events are separate onsets. Sorted within each lane, not simultaneous pitches in one monophonic sequencer. The actual gate fraction applies to the final step of a note including preceding ties; the gate is not necessarily equal to nominal duration. Release tails can overlap.

| Lane type | Grid | Gate fraction | Attack / decay / release (ms) | Sustain |
| --- | --- | ---: | --- | ---: |
| Short | Sixteenth | .48 | 3 / 190 / 48 | .12 |
| Long | Sixteenth | .97 | 12 / 850 / 230 | .32 |
| Standard | Eighth | .84 | 8 / 780 / 150 | .22 |

Short and long rows are split into four-bar banks at tick 64, each with its own 64-step sequencer; standard rows cover eight bars with 64 eighth steps. The 14 pitched note lanes plus 7 control lanes consume 21 sequencers, with 201 nodes and 392 connections.

### Main melody
```text
short 1–4: 0:A4:2 2:A4:2 4:D5:2 6:E5:1 7:D5:1 8:E5:2 10:F5:2 12:F5:2 20:D5:1 21:C5:1 22:Bb4:2 24:A4:2 26:F4:2 36:Bb4:2 38:A4:2 44:C5:2 46:Bb4:2 48:A4:2 50:A4:2 56:E4:2 58:C#4:2
short 5–8: 68:D5:2 70:E5:2 76:A5:2 78:G5:2 84:F5:2 86:E5:2 88:D5:2 90:C5:2 96:Bb4:2 98:A4:2 104:E4:2 106:E4:2 116:E5:1 117:D5:1 118:A4:2 120:F4:2
long 1–4: 16:C5:4 32:G4:4 40:D5:4 52:G4:4
long 5–8: 64:A4:4 72:F5:4 80:F5:4 100:G4:4 108:C#5:4 112:D5:4 122:D4:4
```

### Lower answering melody
```text
short 1–4: 4:A3:2 6:Bb3:1 7:A3:1 12:C4:1 13:Bb3:1 14:A3:1 20:F4:2 22:E4:1 23:D4:1 28:A3:2 30:G3:1 31:F3:1 38:Bb3:2 43:C4:1 44:Bb3:2 46:A3:1 47:G3:1 52:G3:2 54:F3:1 55:E3:1 60:Bb3:2 62:A3:1 63:G3:1
short 5–8: 64:F3:2 66:A3:2 72:C4:1 73:D4:1 74:E4:2 86:A3:2 88:F3:2 90:G3:1 91:A3:1 92:C4:2 94:E4:2 100:Bb3:2 102:A3:2 107:F3:1 108:E3:2 110:C#4:2 116:A3:2 118:G3:2 124:E3:1 125:D3:2
long 1–4: 0:F3:4 8:D4:4 16:D4:4 24:Bb3:4 32:G3:4 40:D4:3 48:E3:4 56:C#4:4
long 5–8: 68:D4:4 76:F4:4 80:C4:6 96:G3:4 104:G3:3 112:D4:4 120:F3:4
```

### Bass foundation, inner harmony and intermittent reply
```text
bass: 0:D2:4 6:A2:2 8:D3:4 12:F2:2 14:A2:2 16:Bb2:4 20:F2:2 24:D3:4 28:C3:2 30:A2:2 32:G2:6 38:D3:2 40:G2:2 42:Bb2:2 44:D3:2 46:G2:2 48:A2:4 52:E3:2 56:G2:4 60:C#3:2 62:E3:2 64:D2:4 68:A2:2 70:F2:2 72:D3:4 76:A2:2 78:E2:2 80:F2:6 86:C3:2 88:A2:2 90:C3:2 92:F2:2 94:G2:2 96:A2:4 100:E3:2 102:G2:2 104:Bb2:4 108:A2:2 110:C#3:2 112:D2:8 120:A2:4 124:D2:2
inner: 0:A3:4 6:F3:2 8:A3:4 16:F3:4 22:D4:2 24:F3:4 32:D3:4 40:G3:2 42:Bb3:2 48:C#3:4 54:G3:2 56:E3:4 64:A3:4 70:F3:2 72:A3:4 80:F3:4 86:C4:2 88:F3:4 96:C#4:4 102:E4:2 104:G3:4 112:A3:8
reply short 1–4: 8:D4:2 26:F4:2 28:D4:2 30:F4:1 31:D4:1 40:G3:2 58:E4:2 60:G4:2 62:E4:1 63:C#4:1
reply short 5–8: 72:D4:2 90:C4:2 92:F4:2 94:E4:1 95:C#4:1 104:C#4:2
reply long 1–4: 0:D4:4 16:D4:4 32:Bb3:4 48:C#4:4
reply long 5–8: 64:D4:4 80:C4:4 96:Bb3:4 112:D4:10
```

### Read the relationships

- At tick 4 the theme reaches D5, then E5–D5 in successive sixteenths at ticks 6–7, returns to E5 at 8 and F5 at 10. This is deliberate short motion around longer structural tones, not a new chord at each event.
- At bar 2, C5 sustains over Bb for a quarter note (ticks 16–19); D4 in the lower line sustains simultaneously, then the lower F4–E4–D4 gesture and upper D5–C5–Bb4 subdivide the next beat. Their simultaneous relationships must be heard and evaluated; the score itself does not prove consonance throughout.
- At bar 5, A4 sustains for four ticks while the lower F3–A3 moves in eighths, then lower D4 sustains as the upper voice moves through D5–E5. This is a reciprocal distribution of rhythmic activity.
- At the final bar, long D5 and D4 anchor tonic arrival, with short E5–D5–A4–F4 movement before the low D4 ending. The exact shared theme is varied, not simply repeated with additional layers.

## Measured comparison and limitations

All six independent 48 kHz mono previews measured -21 dBFS K-weighted RMS (ungated comparison proxy, not LUFS), zero clipped samples. II sample peaks: -3.75, -3.64, -4.28 dBFS; III: -4.08, -4.25, -3.50 dBFS. III role RMS relative to Theme: bass -5 dB, lower melody -2.5 dB, inner harmony and reply each -12 dB. III-03 theme durations in sixteenth ticks: 6 × 1, 31 × 2, 11 × 4. Lower melody: 22 × 1, 21 × 2, 2 × 3, 12 × 4, 1 × 6. III-01 lower distribution is identical; III-02 lower distribution: 29 × 2, 13 × 4, 2 × 6.

Independent renderer approximates oscillator/envelope/filter/room behavior. Bank tails are truncated at internal boundaries in the preview; graph uses short gain glide. Expression is shared eighth-grid role gain automation, not native per-note velocity. Static graph checks were reported in the original delivery, not native import, playback or CPU tests. Listener preference for the previews is established, but neither native accuracy nor composition-wide applicability is established. These measurements are from the original reports, not a new render.

## Current Orbit application and follow-up

Current Notes-mode sequencers have 64 equal slots, Tie/Rest, synchronized division or free seconds, and pattern-wide gate fraction and velocity. Use separate lanes for complementary voices, short/long articulation, shared-clock subdivisions and bank gating only where necessary; respect reachable action-lane capacity and monitor CPU. The large graph is a workaround, not the recommended target complexity. The [sequencer design](../instruments/note-sequencer-design.md#expressive-note-events-planned) and [Phase 7.2.8 roadmap](../roadmap/advanced.md) track native event duration, velocity and longer/finer timing. Distinguish emitted Gate/Trigger/Velocity from the response of an instrument. No new feature is implied by the audition.


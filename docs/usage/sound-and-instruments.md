# Using sound, pitch, rhythm and instrument nodes

[Usage index](README.md) · [Pitch control](../instruments/pitch-control.md) · [Envelope contract](../audio/envelopes.md) · [Instrument recipes](../examples/instrument-recipes.md)

## 1. Pitch versus articulation

Pitch answers **which note?** Articulation answers **when does it start and end?** In current Orbit these are separate signals. A Note Sequencer sends continuous relative semitone offsets, not note-on triggers, note-off gates or velocity. Its Hold slots retain the previous pitch; they do not mute audio. Key release does not end a note. This makes it useful for evolving drones, chord banks and separately clocked motifs, but not yet a complete keyboard instrument.

### Live pitch and shared tuning

1. Add Note Sequencer, Oscillator and Master output. Set oscillator to a free frequency of 110 Hz, pitch tracking **Semitones**, and amplitude 0.12. Connect oscillator output to Master via a conservative Gain.
2. Set Note Sequencer Reference note to A2 (MIDI 45), Initial pitch 0 st. Connect its Pitch output to oscillator's Pitch input at amount 1, not its Frequency input. At A2 the oscillator plays 110 Hz; A3 is +12 st = 220 Hz. Add an oscillator at 165 Hz and share the same Pitch connection to preserve their fifth relationship.
3. Play patch; select the sequencer header and choose **Arm keyboard**. Use the on-screen keyboard or A S D F G H J K (white keys), W E T Y U (black keys), and Z/X for octaves. Note release leaves the last pitch sounding; Reset pitch restores Initial pitch. Selecting another node, stopping or navigating disarms the keyboard.

To transpose a fixed chord/voice without playing keys, use **Pitch Control** instead, with +12 st for one octave or −12 st for one octave down. The receiving oscillator's pitch tracking must be enabled; note offsets cannot turn an oscillator into a different waveform. See [pitch control](../instruments/pitch-control.md) ([PR #6](https://github.com/wgabel/orbit2/pull/6)) and [Note Sequencer](../instruments/note-sequencer.md) ([PR #16](https://github.com/wgabel/orbit2/pull/16)).

### Loop a phrase or record a pitch take

- Select the sequencer → **Edit pattern** → Loop. For a slow drone use four steps, Free 15 seconds, glide 4 seconds; enter A2, C3, E3, A2. The loop lasts 60 seconds. For a clocked phrase, choose a synchronized quarter-note step and 8 steps; at 120 BPM that lasts 4 seconds. Step count, division multiplier, Free timing and glide are configurable. Hold preserves pitch. **Step entry** while armed writes notes to slots; Space enters Hold. [PR #17](https://github.com/wgabel/orbit2/pull/17).
- To record live, use Loop, choose a step count and timing, play, arm keyboard, disable Step entry, choose Overdub or Replace active slots and compatible quantization spacing. **Record take**, play notes, then **Commit take**. Cancel discards the draft, and Undo restores the committed pattern. This records quantized pitch onsets, not durations, gates, velocity or silence. The loop and shared Clock align to transport; Reset transport restarts both. [PR #19](https://github.com/wgabel/orbit2/pull/19).

For exact serialized keys, Hold code, ramp behavior and the combined 30 reachable source-lane limit, see the [Note Sequencer implementation](../instruments/note-sequencer.md). Note Sequencer Pitch may also be used as a bounded level score with appropriate scaling (e.g. MIDI note minus reference 0…12 into Gain amount 1/12, base zero); use a separate explicit rest/level lane and keep musical pitch independent. See [T3 Rhythm grid](../examples/genres/orbit-techniques.md#t3--rhythm-grid-patterns-and-rests).

### Play held notes with Note Source

For live keyboard articulation (7.2.3, [PR #41](https://github.com/wgabel/orbit2/pull/41)), add **Note Source** rather than Note Sequencer. Set Reference note to A3 for a 220 Hz oscillator or handpan, and Keyboard velocity (default 0.8). For the original Modal — Handpan connect Pitch at amount 1 and Trigger, then Audio to Master. For direct velocity with no internal edits, choose [Playable modal percussion](#playable-modal-percussion) instead. Play patch, select Note Source and Arm keyboard, then press keys or on-screen piano keys.

For sustained voices, wire Gate to an ADSR Envelope and its output to zero-base oscillator amplitude; use a separate zero-base Gain controlled by Velocity if desired. Last pressed key wins; releasing it falls back to the previous held key without triggering a new strike. Final release closes Gate while Pitch and Velocity remain held for release tails. This is monophonic; Note-mode patterns and polyphony remain future work. See the [Note Source contract](../instruments/pitch-control.md#phase-723--note-source-in-review).

## 2. Create distinct attacks and releases

| Envelope mode | How it starts / stops | Typical application |
| --- | --- | --- |
| AD (attack–decay) | Rising trigger starts a one-shot rise and fall to zero | Pluck, percussion, transient brightness |
| AR (attack–release) | Gate rises, holds at full level, then releases on gate fall | Held tones and swells |
| ADSR (attack–decay–sustain–release) | Gate opens; attack then decay to sustain; gate closes and releases | Sustained articulated instruments |
| Loop | Repeated AD shapes with optional rest, while active | Cyclic modulation without an external clock |

### A clocked melodic pluck

Add Clock, Envelope (AD), Oscillator, Note Sequencer, Gain and Master. Set BPM 120, Clock synchronized quarter-note (0.5 s). Set Envelope Mode AD, Trigger source **Trigger / clock**, attack 15 ms, decay 350 ms. Set oscillator frequency 220 Hz, waveform Triangle, Semitones tracking and base amplitude 0. Set sequencer reference A3, Loop, synchronized quarter-note step, notes A3, C4, E4, D4. Wire:

| From | To | Amount |
| --- | --- | ---: |
| Note Sequencer Pitch | Oscillator Pitch | 1 |
| Clock | Envelope Trigger | 1 (typed) |
| Envelope output | Oscillator Amplitude | 0.22 |
| Oscillator signal | Gain audio (base gain 0.5) | 1 |
| Gain audio | Master output | 1 |

Each clock boundary articulates the current pitch and the envelope returns to silence before the next step. Use a short glide and check timing. Add a quieter upper partial and filter, delay or reverb to taste. For actual rests, control a separate level/velocity Gain with a rhythm mask; a pitch Hold alone still gets triggered by the Clock. A Manual Trigger can replace Clock for individually fired notes (select the pitch separately). To retrigger directly from physical keyboard presses instead of the clock, use [Note Source](#play-held-notes-with-note-source) and connect its Trigger to the envelope. Automated Note-mode articulation remains 7.2.8d.

### A gated sustained voice

Add **Gate**, Envelope (ADSR), an Oscillator with base amplitude 0 or a sustained instrument → Gain (base 0), then Master. Connect Gate to Envelope Gate, Envelope output to oscillator Amplitude (e.g. 0.16) or external Gain (e.g. 0.5). Set attack 100 ms, decay 250 ms, sustain 0.55, release 500 ms. Play and click **Open gate** / **Close gate**. Gate is a timed source: duration defaults to 100 ms; for this example set duration 2000 ms or longer to hear sustain. For a rhythmic gate, select Gate's Trigger / clock source and connect Clock; choose a duration less than the intended interval. A gate falling during attack or decay leads into release. The Note Sequencer may still set pitch independently.

For AD driven by a continuous oscillator, select **Continuous signal** and use Signal trigger; for reusable conversion of continuous sources use **Signal to Trigger** or **Signal to Gate**. Gate rising edge is another AD trigger-source mode. Typed events are unity links, not audio or arbitrary attenuation; never wire a trigger directly to Master or Resonator audio. Envelope **Show plot → Envelope curve** displays the configured trajectory (sustain hold is illustrative) and live measured stage when available; use waveform for the actual modulated result. Implementations: [PR #7](https://github.com/wgabel/orbit2/pull/7), [PR #8](https://github.com/wgabel/orbit2/pull/8), [PR #10](https://github.com/wgabel/orbit2/pull/10), [PR #12](https://github.com/wgabel/orbit2/pull/12), [PR #14](https://github.com/wgabel/orbit2/pull/14). See [envelopes](../audio/envelopes.md) for retriggering, mode and port activation details.

## 3. Built-in instruments and resonators

### Sustained instruments

Choose an **Instrument — …** composite from Add node, then connect its Audio to Master or Mixer. These preset graphs sound continuously while playing. Open each composite to edit its real nodes, filters and effects; each added instance has independent IDs. They are editable synthesized approximations, not recordings or guaranteed acoustic replicas. [PR #31](https://github.com/wgabel/orbit2/pull/31).

| Catalog entry | Native tuning | Suggested use |
| --- | --- | --- |
| Instrument — Organ | A2 / 110 Hz | Harmonic drone, organ-like tones, chord bed |
| Instrument — Flute | A3 / 220 Hz | Soft melodic part, breathy upper voice |
| Instrument — Brass | A2 / 110 Hz | Stabs, warm resonant pad, ensemble color |
| Instrument — Bowed strings | A2 / 110 Hz | Slow swells and sustained harmonic bed |
| Instrument — Didgeridoo | A1 / 55 Hz | Low drone and evolving vocal/formant color |

Use Note Sequencer reference matching the native tuning and connect it to **Pitch (st)** at unity. **Gain offset** is additive around a base of 0.5, not a zero-based envelope input: −0.5 silences, 0 keeps default, +0.5 reaches unity. For simple articulated notes, insert an external Gain with base zero after the composite and modulate it using Envelope as in §2; place reverb after that gain for tails. Genuine note polyphony is not implemented: use independent instrument composites for separate chord tones. For exact internal recipes and gain limits see [instrument recipes](../examples/instrument-recipes.md#phase-721--built-in-instrument-composites-implemented).

### Playable sustained instruments

For held notes rather than drones, choose **Playable — Organ**, **Playable — Flute**, **Playable — Brass**, **Playable — Bowed strings** or **Playable — Didgeridoo** (7.2.6a, [PR #44](https://github.com/wgabel/orbit2/pull/44)).

1. Add Note Source and Playable — Flute. Set Reference note to A3 (A2 for organ, brass and strings; A1 for didgeridoo).
2. Connect Note Source Pitch → Pitch (st) at amount 1, Gate → Gate, and Velocity → Velocity at amount 1. Connect Audio → Master output.
3. Play patch, select Note Source, Arm keyboard, hold a note and release. Try a lower Keyboard velocity and press again; the note is quieter. Overlap keys to play legato, or release before playing a new note for a fresh attack.
4. Open the composite and edit the ADSR for slower swells or shorter articulation. Reverb follows the envelope so it can ring after release.

If silent, check that both Gate and Velocity are connected and that the keyboard is armed. No Trigger cable is needed for these ADSR voices. The original **Instrument — …** entries still sound continuously. These are monophonic editable voices, not sample-based or polyphonic instruments. See [settings and technical recipe](../examples/instrument-recipes.md#phase-726a--playable-sustained-instruments-in-review).

### Impact — AD percussion

Add **Impact — AD percussion**, connect Audio to a mixer or Master, then Play. Its internal 1 Hz square oscillator triggers independent pitch and amplitude AD envelopes. The attack makes a short falling pitch transient and amplitude shapes the impact; the voice oscillator base frequency is 55 Hz, base amplitude zero, frequency modulation +300 Hz, amplitude depth 0.8. Open it to tune the voice, change envelope times or trigger oscillator rate. It has an Audio output (internally wired to Output Rack), and no exposed Trigger input by default. To drive it with your own Clock you must edit the graph and expose an appropriate typed input. [PR #7](https://github.com/wgabel/orbit2/pull/7); annotated example [PR #32](https://github.com/wgabel/orbit2/pull/32).

### Standalone resonator

**Resonator** is an audio processor. Like a bell body, it needs excitation; no input means no ring from an initially silent state. For a tuned strike, add Manual Trigger, Envelope (AD, Trigger / clock, attack 1 ms, decay 50 ms), Noise (amplitude 0), Resonator (choose Wood strike preset), Gain (0.5), Master. Wire Manual Trigger → Envelope Trigger, Envelope → Noise Amplitude at 0.2, Noise → Resonator audio, Resonator → Gain → Master. Play and **Fire trigger**. The burst stops but the tuned ring decays. Try Metal strike or lengthen Decay to hear a longer tail. Lower Excitation gain before feeding a sustained tonal input; high drive designed for strikes can amplify an in-tune continuous tone.

| Control | What changes |
| --- | --- |
| Frequency and Pitch offset | Resonance center, with shared relative semitone tracking and additive Hz modulation |
| Decay (T60) | Time for the free-decaying amplitude to drop 60 dB, not delay length |
| Excitation gain | Energy into the mode (0–32); lower first for sustained excitation |
| Output gain | Wet signal trim (0–1) |
| Wet mix | Blend dry and ringing audio; state continues even at zero wet |

Five presets: Tuned body, Wood strike, Metal strike, Dark drone resonance, Distant ring. Use the spectrum/plot to inspect the actual ring. Unlike a Filter response curve, a dedicated Resonator response plot is not available. [PR #34](https://github.com/wgabel/orbit2/pull/34); [exact parameters and DSP contract](../instruments/physical-modelling.md#phase-724--resonator-implemented).

### Modal percussion

Several resonators with different frequencies and decay times create a more complex, instrument-like ring than one mode. This is **modal synthesis**. [PR #37](https://github.com/wgabel/orbit2/pull/37) provides four independent editable composites:

| Catalog entry | Native frequency | Character / mode count |
| --- | ---: | --- |
| Modal — Handpan | 220 Hz | Tuned, gently metallic (4 modes) |
| Modal — Bell | 330 Hz | Inharmonic ringing bell (5 modes) |
| Modal — Gong | 83 Hz | Low, extended metallic decay (6 modes) |
| Modal — Wood percussion | 294 Hz | Short woody strike (3 modes) |

**Try a rhythmic handpan:** Add Modal — Handpan, Manual Trigger and a root Gain/Master (or Mixer). Connect Manual Trigger to its Trigger at unity, Audio to the mixer/master via a conservative gain. Play, then Fire trigger. Each hit excites a short internal noise burst, which rings through the bank of modes; no raw noise is routed to the composite output. Try Clock instead of Manual Trigger for repeated strikes (start with a slow/half-note or whole-note clock so the tails can decay). Add Note Sequencer with reference A3, connect Pitch to **Pitch (st)** at unity; play a phrase or arm keyboard to change the next strike's pitch. Use the other presets for different transient and decay characters; repeated strikes can overlap.

Exposed controls are **Trigger** (accepts typed Clock), **Pitch (st)** (continuous transposition of every mode), **Strength offset** (additive around 0.75 on internal shared excitation Gain, −0.375 halves the default, −0.75 prevents new excitation without immediately stopping existing tails), and **Excitation** (optional external mono audio, internally attenuated ×0.03 before the shared excitation gain). Continuous pitch changes retune ringing modes as well as future strikes; this is not latched note velocity or independent polyphonic notes. External excitation gives other textures, e.g. a quiet oscillator or shaped audio burst, but keep headroom. Do not connect raw event pulses to the audio excitation socket.

Open the composite to alter the short AD burst, each mode's frequency (stored as Hz), T60, output gain and shared mixer. Moving one mode slightly changes timbre; moving all with Pitch keeps the ratios. Connect Audio to external reverb or chorus for space. To tame the total volume while preserving internal modal interactions, attenuate the output rather than altering all the individual modes. See [modal contract and exact preset ratios](../instruments/physical-modelling.md#phase-725--modal-instrument-composites-implemented). For keyboard-powered pitch, trigger and velocity, use [Note Source](#play-held-notes-with-note-source); the compact modal bank/response editor remains a follow-up.

### Playable modal percussion

Use **Playable — Handpan**, **Playable — Bell**, **Playable — Gong** or **Playable — Wood percussion** (7.2.6b, in review) for direct velocity-sensitive strikes. Unlike Playable sustained instruments, these use Trigger instead of Gate.

1. Add Note Source and Playable — Handpan; set Note Source Reference note to A3. For Bell use E4, Gong E2 or Wood D4 (approximate native tunings).
2. Wire Pitch → Pitch (st), Trigger → Trigger and Velocity → Velocity, all at unity. Connect Audio → Master output.
3. Play, select Note Source and Arm keyboard. Tap a key to strike the instrument; try changing Keyboard velocity from 0.8 to 0.3 before the next hit. A new note-on strikes again even if the previous note is still held. Key release does not stop the ringing.
4. Optional: connect quiet external audio to Excitation. It excites the resonators independently of velocity. Open the composite to edit attack, decay, mode frequencies, weights or output gain.

If the instrument does not strike, check the Velocity cable and value, Trigger and armed keyboard. If you want a Clock with no velocity controller, choose the original **Modal — …** (default strength .75) or set the playable version's internal velocity Gain base to a fixed level. Pitch is shared by all modes, including ringing tails; simultaneous independent notes require future polyphony. See [contract and exact behavior](../instruments/physical-modelling.md#phase-726b--playable-modal-instruments-in-review).

## 4. Make a full piece

Combine independently timed and tuned voice composites; give each a role, local gain and arrangement controls. For example, hold sustained strings as harmony, use the handpan as sparse accents, add the didgeridoo as a quiet changing foundation, and use a clocked articulated triangle/reed voice for the motif. Distinguish the sound design of one voice from composition. Use the [composition guide](../examples/generative-ambient-composition.md), [genre-to-node techniques](../examples/genres/orbit-techniques.md), [contextual layer balance](../examples/contextual-layer-balance.md) and [production workflow](../examples/mixing-mastering-and-analysis.md) for creative and validation guidance. There is no universal required genre, loudness ratio or choice of instrument. Validate structure, listen if possible, measure important parts in context, leave headroom and share the editable JSON with musical notes.

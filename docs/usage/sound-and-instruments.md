# Using sound, pitch, rhythm and instrument nodes

[Usage index](README.md) · [Pitch control](../instruments/pitch-control.md) · [Envelope contract](../audio/envelopes.md) · [Instrument recipes](../examples/instrument-recipes.md)

## 1. Pitch versus articulation

Pitch answers **which note?** Articulation answers **when does it start and end?** A Note Sequencer defaults to **Pitch only (drone)**, which latches relative semitone offsets and never opens or closes an instrument gate. Switch **Playback → Notes (gate + trigger)** for a self-playing monophonic melody: connect its Pitch and Trigger outputs to a plucked instrument, or Pitch and Gate to a sustained instrument. The new mode is in review ([PR #87](https://github.com/wgabel/orbit2/pull/87)); older patches continue to use pitch-only operation.

### Live pitch and shared tuning

1. Add Note Sequencer, Oscillator and Master output. Set oscillator to a free frequency of 110 Hz, pitch tracking **Semitones**, and amplitude 0.12. Connect oscillator output to Master via a conservative Gain.
2. Set Note Sequencer Reference note to A2 (MIDI 45), Initial pitch 0 st. Connect its Pitch output to oscillator's Pitch input at amount 1, not its Frequency input. At A2 the oscillator plays 110 Hz; A3 is +12 st = 220 Hz. Add an oscillator at 165 Hz and share the same Pitch connection to preserve their fifth relationship.
3. Play patch; select the sequencer header to open the piano dock. Use the on-screen keyboard or A S D F G H J K (white keys), W E T Y U (black keys), and Z/X for octaves. Note release leaves the last pitch sounding; Reset pitch restores Initial pitch. Selecting a different compatible source switches the dock target; selecting ordinary nodes or navigating retains it. Collapse to pause input; Show keyboard restores it.

To transpose a fixed chord/voice without playing keys, use **Pitch Control** instead, with +12 st for one octave or −12 st for one octave down. The receiving oscillator's pitch tracking must be enabled; note offsets cannot turn an oscillator into a different waveform. See [pitch control](../instruments/pitch-control.md) ([PR #6](https://github.com/wgabel/orbit2/pull/6)) and [Note Sequencer](../instruments/note-sequencer.md) ([PR #16](https://github.com/wgabel/orbit2/pull/16)).

### Loop a phrase or record a pitch take

- Select the sequencer → **Edit pattern → Edit notes**, choose **Loop pattern**, and tap a pitch/time cell to enter a note (or select a step and play the shared piano to enter and auto-advance). For a slow drone choose Pitch only, four steps, Free 15 seconds (in Settings), and Glide 4 seconds; enter A2, C3, E3, A2. The loop lasts 60 seconds. For a clocked phrase choose synchronized one-beat steps and eight steps; at 120 BPM that lasts four seconds. In Notes mode, Tie extends the previous note and Rest closes its gate. The canvas node keeps a compact first-16-step overview. See [PR #17](https://github.com/wgabel/orbit2/pull/17) and the implemented editor [PR #87](https://github.com/wgabel/orbit2/pull/87).
- In Notes mode, select a populated step in Edit notes and use **Note length (0 = default)** ([PR #111](https://github.com/wgabel/orbit2/pull/111), merged). Use 0.25 for a quarter of the slot, 0.5 for half, or 1 for the full slot. A Tie extends the note into the next slot; the chosen length applies to the last slot in the tie chain. This changes Gate duration, not the step grid or a Trigger-only instrument's decay. Zero restores the pattern-wide default.
- In Notes mode, select a populated step in Edit notes and choose **Note velocity** (CI verified; awaiting merge, [PR #115](https://github.com/wgabel/orbit2/pull/115)). Default inherits the pattern-wide value; 0–100% sets an explicit override, retained through ties. Connect Velocity to compatible Strength/Expression with a suitable base value. Zero is not a Rest. The scheduler is merged ([PR #114](https://github.com/wgabel/orbit2/pull/114)); selecting a different slot shows that slot's independent length/velocity. Both edits support undo/redo.
- To record live, set Loop, choose step count/timing, start patch playback, open **Record**, select Overdub or Replace active slots and quantization. Choose **Record take**, play the shared piano, then **Commit take**. Cancel discards the draft; Undo restores the last committed pattern. This records quantized pitch onsets; it does not yet capture per-note length or velocity. The loop shares the transport clock. [PR #19](https://github.com/wgabel/orbit2/pull/19), [PR #87](https://github.com/wgabel/orbit2/pull/87) (in review).

For exact serialized keys, Hold code, ramp behavior and the combined 30 reachable source-lane limit, see the [Note Sequencer implementation](../instruments/note-sequencer.md). Note Sequencer Pitch may also be used as a bounded level score with appropriate scaling (e.g. MIDI note minus reference 0…12 into Gain amount 1/12, base zero); use a separate explicit rest/level lane and keep musical pitch independent. See [T3 Rhythm grid](../examples/genres/orbit-techniques.md#t3--rhythm-grid-patterns-and-rests).

### Play held notes with Note Source

For live keyboard articulation (7.2.3, [PR #41](https://github.com/wgabel/orbit2/pull/41)), add **Note Source** rather than Note Sequencer. Set Reference note to A3 for a 220 Hz oscillator or handpan, and Keyboard velocity (default 0.8). For the original Modal — Handpan connect Pitch at amount 1 and Trigger, then Audio to Master. For direct velocity with no internal edits, choose [Playable modal percussion](#playable-modal-percussion) instead. Play patch, select Note Source to open the piano dock, then press keys or on-screen piano keys.

On a phone the piano sits above the existing bottom navigation, leaving the rest of the canvas editable. The tiny target label shows which source plays; −/+ choose octave, ■ releases all notes (or resets continuous pitch), ↕ changes key height, and the chevron collapses/opens the dock. Slide across white and black keys to play glissando; selecting an effect or entering a composite retains the target. Select a different compatible Note Source/Note Sequencer to switch. The live pitch value cannot move the keys. If audio still feels delayed, compare a fast-attack voice with a minimal gated oscillator and check Performance for estimated output pipeline latency; the sampled pitch display is not an end-to-end input measurement. ([PR #70](https://github.com/wgabel/orbit2/pull/70) — in review.)

For sustained voices, wire Gate to an ADSR Envelope and its output to zero-base oscillator amplitude; use a separate zero-base Gain controlled by Velocity if desired. Last pressed key wins; releasing it falls back to the previous held key without triggering a new strike. Final release closes Gate while Pitch and Velocity remain held for release tails. This remains monophonic; sequencer Note-mode patterns are in review in [PR #87](https://github.com/wgabel/orbit2/pull/87), while polyphony remains future work. See the [Note Source contract](../instruments/pitch-control.md#phase-723--note-source-in-review).

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

Each clock boundary articulates the current pitch and the envelope returns to silence before the next step. Use a short glide and check timing. Add a quieter upper partial and filter, delay or reverb to taste. For actual rests, control a separate level/velocity Gain with a rhythm mask; a pitch Hold alone still gets triggered by the Clock. A Manual Trigger can replace Clock for individually fired notes (select the pitch separately). To retrigger directly from physical keyboard presses instead of the clock, use [Note Source](#play-held-notes-with-note-source) and connect its Trigger to the envelope. The direct Notes playback and articulation alternative is in review in [PR #87](https://github.com/wgabel/orbit2/pull/87).

### A gated sustained voice

Add **Gate**, Envelope (ADSR), an Oscillator with base amplitude 0 or a sustained instrument → Gain (base 0), then Master. Connect Gate to Envelope Gate, Envelope output to oscillator Amplitude (e.g. 0.16) or external Gain (e.g. 0.5). Set attack 100 ms, decay 250 ms, sustain 0.55, release 500 ms. Play and click **Open gate** / **Close gate**. Gate is a timed source: duration defaults to 100 ms; for this example set duration 2000 ms or longer to hear sustain. For a rhythmic gate, select Gate's Trigger / clock source and connect Clock; choose a duration less than the intended interval. A gate falling during attack or decay leads into release. The Note Sequencer may still set pitch independently.

For AD driven by a continuous oscillator, select **Continuous signal** and use Signal trigger; for reusable conversion of continuous sources use **Signal to Trigger** or **Signal to Gate**. Gate rising edge is another AD trigger-source mode. Typed events are unity links, not audio or arbitrary attenuation; never wire a trigger directly to Master or Resonator audio. Envelope **Show plot → Envelope curve** displays the configured trajectory (sustain hold is illustrative) and live measured stage when available; use waveform for the actual modulated result. Implementations: [PR #7](https://github.com/wgabel/orbit2/pull/7), [PR #8](https://github.com/wgabel/orbit2/pull/8), [PR #10](https://github.com/wgabel/orbit2/pull/10), [PR #12](https://github.com/wgabel/orbit2/pull/12), [PR #14](https://github.com/wgabel/orbit2/pull/14). See [envelopes](../audio/envelopes.md) for retriggering, mode and port activation details.

## 3. Built-in instruments and resonators

For a more realistic organ experiment, use the new Drawbar Organ and Rotary Speaker nodes below, or the [Hammond and pipe-rank recipes](../instruments/realistic-instrument-emulation.md#3-organ-design-with-existing-nodes) for editable primitive-node alternatives.

### Faust instrument nodes

Choose the voice by its musical role before building every foreground sound from oscillators; see [instrument selection and arrangement recipes](../examples/instrument-recipes.md#choose-an-instrument-for-the-musical-role). Physical models are available and technically tested but not guaranteed acoustic replicas. For an automated phrase, a **Note Sequencer in Notes + Loop mode** can supply Pitch plus the required Gate or Trigger and optional Velocity directly, without a separate Note Source. Keep A4 reference and Pitch-to-Pitch at unity; Pitch only mode alone is insufficient to articulate these instruments.

Merged ([PR #81](https://github.com/wgabel/orbit2/pull/81)). Add **Note Source**, leave its reference at A4, and add **Physical Pluck**, **Physical Marimba** or **Drawbar Organ** from Audio generators. Connect Pitch → Pitch (A4), Trigger → Trigger, and Audio → Master output. For the pluck/marimba set Strike strength to 0 and connect Velocity → Strike strength at unity; for organ connect Gate → Gate instead and adjust Swell. Select Play patch, select Note Source, then play the keyboard. The pluck and marimba ring after releasing a key; organ sustains until key release. Try the built-in presets and reset strength to zero after selecting one if using velocity. Add **Rotary Speaker** between organ and master: use Speed 1 (slow) or 2 (fast), 0 to brake. For real-time speed changes adjust the slider or modulate it. See the [exact controls, limits and example routings](../instruments/faust-instrument-nodes.md). These are monophonic candidate instruments, not listening-approved replicas.

**Physical Clarinet** and **Physical Flute** (merged, [PR #82](https://github.com/wgabel/orbit2/pull/82)) use sustained breath-driven models. Connect Note Source Pitch → Pitch (A4), Gate → Gate and Audio → Master. Do not connect Trigger. Optional: set Expression base to 0 and wire Velocity → Expression at unity for keyboard dynamics; otherwise set Expression manually or modulate it continuously. Adjust reed stiffness or breath noise, attack/release and vibrato from the node; compare the five presets per instrument. Both support C3–C6 and legato. See the [wind model controls and calibration](../instruments/faust-sustained-instruments.md).

### New bowed, brass and electric-guitar candidates (PR #86)

The following nodes are **merged** ([PR #86](https://github.com/wgabel/orbit2/pull/86)) but **not yet listener-approved as realistic instruments**. **Physical Violin (experimental)** and **Physical Brass** are sustained models: Note Source at A4, Pitch → Pitch, Gate → Gate, Audio → Master. Optionally connect Velocity → Expression (set Expression base to 0). Violin exposes Bow pressure, Bow position and bow attack/release; brass has Lip tension, Mute opening and breath attack/release. Both support vibrato depth/rate. The violin model may jump octaves on some notes and should not be treated as a reliable acoustic replica until tuning tests pass.

**Physical Electric Guitar** is a triggered steel-string model: Pitch → Pitch, Trigger → Trigger, Velocity → Pluck strength (set strength base to 0), Audio → Master. Pluck position shapes harmonics; String sustain controls damping. Use a separate amplifier/drive/effects chain for electric-guitar tone; there is no amp model in this node. Each string is monophonic; overlapping notes require separate voices. See [controls, limitations and test requirements](../instruments/faust-violin-brass-guitar.md).

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

For held notes rather than drones, choose **Playable — Organ**, **Playable — Flute**, **Playable — Brass**, **Playable — Bowed strings** or **Playable — Didgeridoo** (7.2.6a, implemented, [PR #44](https://github.com/wgabel/orbit2/pull/44)).

1. Add Note Source and Playable — Flute. Set Reference note to A3 (A2 for organ, brass and strings; A1 for didgeridoo).
2. Connect Note Source Pitch → Pitch (st) at amount 1, Gate → Gate, and Velocity → Velocity at amount 1. Connect Audio → Master output.
3. Play patch, select Note Source, hold a note and release. Try a lower Keyboard velocity and press again; the note is quieter. Overlap keys to play legato, or release before playing a new note for a fresh attack.
4. Open the composite and edit the ADSR for slower swells or shorter articulation. Reverb follows the envelope so it can ring after release.

If silent, check that both Gate and Velocity are connected and that the keyboard is armed. No Trigger cable is needed for these ADSR voices. The original **Instrument — …** entries still sound continuously. These are monophonic editable voices, not sample-based or polyphonic instruments. See [settings and technical recipe](../examples/instrument-recipes.md#phase-726a--playable-sustained-instruments-implemented).

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

Use **Playable — Handpan**, **Playable — Bell**, **Playable — Gong** or **Playable — Wood percussion** (7.2.6b, merged, [PR #46](https://github.com/wgabel/orbit2/pull/46)) for direct velocity-sensitive strikes. Unlike Playable sustained instruments, these use Trigger instead of Gate.

1. Add Note Source and Playable — Handpan; set Note Source Reference note to A3. For Bell use E4, Gong E2 or Wood D4 (approximate native tunings).
2. Wire Pitch → Pitch (st), Trigger → Trigger and Velocity → Velocity, all at unity. Connect Audio → Master output.
3. Play, select Note Source to open the piano dock. Tap a key to strike the instrument; try changing Keyboard velocity from 0.8 to 0.3 before the next hit. A new note-on strikes again even if the previous note is still held. Key release does not stop the ringing.
4. Optional: connect quiet external audio to Excitation. It excites the resonators independently of velocity. Open the composite to edit attack, decay, mode frequencies, weights or output gain.

If the instrument does not strike, check the Velocity cable and value, Trigger and armed keyboard. If you want a Clock with no velocity controller, choose the original **Modal — …** (default strength .75) or set the playable version's internal velocity Gain base to a fixed level. Pitch is shared by all modes, including ringing tails; simultaneous independent notes require future polyphony. See [contract and exact behavior](../instruments/physical-modelling.md#phase-726b--playable-modal-instruments-in-review).

## 4. Make a full piece

Combine independently timed and tuned voice composites; give each a role, local gain and arrangement controls. For example, hold sustained strings as harmony, use the handpan as sparse accents, add the didgeridoo as a quiet changing foundation, and use a clocked articulated triangle/reed voice for the motif. Distinguish the sound design of one voice from composition. Use the [composition guide](../examples/generative-ambient-composition.md), [genre-to-node techniques](../examples/genres/orbit-techniques.md), [contextual layer balance](../examples/contextual-layer-balance.md) and [production workflow](../examples/mixing-mastering-and-analysis.md) for creative and validation guidance. There is no universal required genre, loudness ratio or choice of instrument. Validate structure, listen if possible, measure important parts in context, leave headroom and share the editable JSON with musical notes.

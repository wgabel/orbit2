# Historical implementation record — node features

> Superseded 2026-11-23 README implementation notes, regrouped without changing their technical claims. These are **historical**, not current feature status. Go to the [archive index](legacy-readme.md), [roadmap](../../ROADMAP.md) and [feature contracts](../README.md) before implementing.

### Node mute (Phase 6.16.1)

Use **M** in an oscillator, processor or composite node header to silence all of its outgoing signals (including modulation). Click it again to restore output. Mute preserves wiring, parameters and running DSP state; it ramps the output gain over 10 ms without recompiling the graph. Muting the final effect of a lane also silences that effect's tail; muting an earlier generator does not immediately remove downstream reverb or delay tails. The muted state survives reload and is undoable/redoable. Racks and the master output do not offer M.

### Processor bypass (Phase 6.16.2)

Use **B** on Gain, Filter, Parametric EQ, Delay, Reverb, Chorus or Resonator to hear the unprocessed audio input. Click again to restore processing. The shared audition control crossfades over 10 ms between the dry input and normal effect output without recompilation; the wet path continues running, preserving its delay buffers, reverb tail and modulation. B is independent of M: if both are active, the output is silent, and unmuting restores the bypassed signal. Bypass preserves wiring and parameters and is undoable and persisted. Mixers, generators, composites and racks do not have a defined bypass path. Keep headroom when bypassing attenuation such as a Gain node: the incoming level passes through unchanged.

### Individual node preset foundation (Phase 6.17.1)

The application has a `NodePresetService` catalog of versioned, complete base parameter configurations keyed by stable node type and preset ID. Read-only built-ins are grouped by usage and stored in embedded JSON arrays in `src/Orbit.Infrastructure/Presets/BuiltIn/`. Ten initial configurations cover oscillator, noise, gain, filter, EQ, delay, reverb and chorus, with a reserved Custom group per eligible type. Configurations include discrete and dormant values, but no instance identities, wiring, mute/bypass state, layout or live samples. `NodePresetValidator` uses the registered parameter definitions to reject incomplete, invalid or incompatible presets. The complete catalog is validated by unit tests and at app startup. Add data to the appropriate JSON resource without registering a new node or changing DSP.

### Atomic node preset application (Phase 6.17.2)

The application can apply any compatible preset to a root or nested node through one `ApplyNodePresetCommand`. The graph validates every stable parameter key and value before changing anything. It retains node, parameter and port identities, wiring, mute/bypass state and layout, including dormant modulation routes that reactivate when a preset switches the node back to the relevant mode. One committed revision contains all changed `ParameterChanged` facts and is one undoable action; invalid or identical presets do not commit. Slider gesture history remains independent. Ordinary base-value changes update the existing DSP voice and structural changes use its existing replacement/crossfade path. Saved patches retain actual values, not a reference to the preset.

~~~csharp
session.Execute(new ApplyNodePresetCommand(graphId, nodeId,
    nodePresetService.GetPreset("oscillator-deep-sine")));
~~~

### Shared node preset selector (Phase 6.17.3)

Eligible generators and processors now show a **Preset** selector just below their header, on every canvas. Choose a grouped configuration to apply it immediately as one graph edit, including during playback. Its usage description appears below the control (hover for the full text). The selector shows **Modified / no preset** if the current base settings do not exactly match a catalog entry. Manual edits and undo/redo update the selected name; modulation, mute and bypass do not affect matching. The Custom group stays visible even if empty. Composites, racks and nodes without configurable parameters do not show the selector.

### Expanded built-in presets (Phase 6.17.4)

The catalog contains 65 built-in presets, including the ten original configurations. Each eligible node has several usage groups and complete parameter configurations, including values dormant in the chosen mode.

| Node | Presets | Examples |
|---|---:|---|
| Oscillator | 14 | Sub Bass, Detuned Companion, Slow Drift, Bar Swell |
| Noise | 7 | Wind, Deep Rumble, Fine Grain |
| Gain | 6 | Quarter Level, Quiet Bed, Modulation Center |
| Filter | 8 | Dark Drone, Vocal Resonance, Moving Notch |
| Parametric EQ | 7 | Warmth, Presence, Formant Peak |
| Delay | 8 | Dotted Quarter, Double Track, Long Echo |
| Reverb | 8 | Soft Chamber, Long Wash, Subtle Glue |
| Chorus | 7 | Subtle Double, Slow Drift, Wet Vibrato |

These configurations use the existing selector, graph commands and audio engine without new DSP or node-specific UI. The tests apply and match every shipped preset and generate a reachable Faust graph. Sound depends on routing, modulation and input levels; leave headroom when layering or using resonance and feedback.

### Custom node preset library (Phase 6.17.5)

Click **…** beside the node's Preset selector to open its shared custom preset manager. Enter a unique name (up to 80 characters) and optional description, then choose **Save as** to capture the node's complete base configuration. Select an existing custom preset in the manager to rename it, explicitly overwrite its values with the current node settings, or delete it. Name uniqueness is case-insensitive within each node type. Built-ins cannot be edited. Close the manager to choose the saved preset in the Custom group on any compatible node. If several presets have identical values, the selector displays the first exact match in catalog order (built-ins first).

The library is browser-local IndexedDB data independent of named patches and autosave. Custom presets are available after reload and when switching patches; saving, renaming, overwriting and deleting never change the graph or its undo history. If another tab changes the library before a write, reload the library and retry. A storage or validation failure preserves the existing configurations.

### Portable node presets (Phase 6.17.6)

In the custom preset manager, choose a saved preset and **Export selected JSON** to download a single versioned configuration. On another browser (or another patch), open the manager for the same node type, choose **Import preset JSON**, review the name, type, schema and description, and select an import action:

- **Import preset** keeps the file's ID and name, rejecting either ID or name conflicts.
- **Import as copy** creates a new ID and requires a unique copy name.
- **Replace** appears only if the file's ID matches a custom preset of this type; it explicitly replaces that entry with the file's settings and metadata.

Files must contain a single schema-1 custom preset with all registered parameter keys and valid values, including inactive settings. The manager rejects incompatible types, unknown versions, duplicate keys, invalid values and built-in identities. The file contains no patch, wiring, instance identity or DSP state. Preview, cancellation, export and import do not alter the current node; use the selector to apply the imported configuration as a separate graph edit. Import is limited to 256 KiB and is saved atomically through the existing library transaction.

### Signal observation infrastructure (Phase 6.18.1)

The audio runtime now compiles up to eight host-only auxiliary observation channels from existing reachable signals, with channel zero reserved for the mono master before playback gain and envelope. Graph-qualified addresses resolve nested sockets and racks to real weighted input or post-mute/bypass output samples. The browser explicitly isolates channel zero; taps never feed speakers. The shared analysis service provides generation-checked, bounded sample windows and releases its browser analyzers with subscriptions or playback. Addresses beyond the fixed capacity or outside the reachable graph are unavailable without changing playback.

### Oscilloscope and level meter (Phase 6.18.2)

Select a node by its header, then choose **Show scope** at the upper left of the canvas. Choose a mono input or output (output is preferred by default). Start playback to inspect actual DSP samples. The scope provides 5/20/50 ms waveform windows with free-running or rising/falling zero-crossing trigger, and 1/5/10 s history for slow modulation. The history preserves min/max peaks in 10 ms buckets and displays gaps where samples were not captured. Peak and RMS meters report the latest 20 ms in dBFS (±1 is 0 dBFS); signals at or above full scale are highlighted. The waveform scales automatically to accommodate modulation outside ±1. Close the view to release its capture; changing source or display controls never edits the graph. Unavailable taps and stopped playback are shown explicitly.

On an oscillator's output, select **One configured cycle** in the expanded scope to set the timebase from its configured frequency or tempo/division. It automatically follows those settings, using the sample window for short cycles and up to 100 s of bounded min/max history for long ones. Allow history to fill after opening the scope. Modulated frequency changes actual cycle lengths; the configured window is a reference, not a phase-locked measurement.

### Spectrum analyzer (Phase 6.18.3)

Open a node's scope and change **View** to **Spectrum** to inspect the actual fundamental, harmonics and noise on the selected port. The horizontal axis is logarithmic Hz from the first resolvable bin to Nyquist; the vertical axis is peak-amplitude dBFS, calibrated so a bin-centered unit sine is 0 dBFS. Choose 1,024/2,048/4,096/8,192 samples, Hann or rectangular window, and off/fast/slow power smoothing. The readout reports the dominant positive-frequency bin and the actual Hz/bin resolution (about 5.86 Hz at 48 kHz with 8,192 samples). The mean is removed before windowing; DC remains visible in waveform/level mode. Off-bin tones spread across bins and noise levels are per bin, not spectral density. The Hann window reduces leakage; rectangular windows are useful for bin-centered measurements. Both views share one capture lease, do not edit the patch, and keep the 20 ms peak/RMS readouts.

### Integrated node visualizations (Phase 6.18.4)

Eligible nodes now have **Show plot** at the bottom of the shared node frame. Open it to view the selected mono input/output as a compact waveform, spectrum or level meter. **Expand** opens the same view with the detailed timebase, trigger and FFT controls; closing returns to the compact display if it was open. The original selected-node **Show scope** action also opens the expanded panel. Each graph remembers its own compact visibility, selected port, mode and detailed settings, plus one expanded node. Settings survive named saves, export/import, reload and navigation without creating graph undo entries. Plotting is paused when the view is offscreen or the browser tab is hidden; collapsing, navigating or expanding releases the unused view. Stopped playback and unavailable/unrouted taps are explicitly labelled. Processor response curves are a separate Phase 6.18.5 feature.

### Patch documentation and canvas annotations (Phase 6.19 — partial)

Add **Note** to place a titled, editable, resizable and collapsible text card in the current root or nested canvas. Notes use named theme accents and are saved and exported with their owning layout. Select a node or cable and use **Describe selected** to add aliases, descriptions or wire labels. Select related nodes (or a note) and choose **Create visual group** to organize them in a named frame; edit membership explicitly, drag its header to move members together, and resize or delete the frame independently. Use **Patches** for patch-wide overview and discovery. **Annotations** switches between Show, Titles only and Hide; zooming out simplifies note/group detail. **Find annotation** reveals hidden items and **Fit signals** ignores annotations. These features have no signal ports or DSP and can be used while playing without changing the audible patch. Use **Outline** to browse or search the whole graph, locate hidden notes and groups, or open nested composites. See the [architecture and phased plan](../editor/patch-annotations.md).

Canvas counts distinguish local nodes/connections from the recursive **Entire patch** total ([PR #36](https://github.com/wgabel/orbit2/pull/36)); the total includes nested racks and composite wrappers, not note cards or groups. For step-by-step annotation display and finder instructions ([PR #35](https://github.com/wgabel/orbit2/pull/35)), see [organizing a patch](../usage/patch-editor.md#3-document-and-organize-a-patch).

### Envelopes and rhythmic triggering (Phase 7.1 — partial)

**Available now:** Envelope (AD, AR, ADSR and Loop), typed Trigger/Gate/Clock routing, Manual Trigger, timed Gate, Clock, Signal to Trigger/Gate adapters, envelope presets and configured/live envelope visualization. AD supports continuous rising edges, Trigger / clock or Gate rising edge. AR/ADSR use Gate; Loop repeats AD segments. Route its 0–1 output to a zero-base oscillator amplitude or Gain, with an appropriate modulation amount. A Note Sequencer outputs pitch only, so use independent Clock/Gate/Trigger for articulation. Custom multi-point envelopes and additional presets remain planned. [PR #7](https://github.com/wgabel/orbit2/pull/7), [PR #8](https://github.com/wgabel/orbit2/pull/8), [PR #10](https://github.com/wgabel/orbit2/pull/10), [PR #12](https://github.com/wgabel/orbit2/pull/12), [PR #14](https://github.com/wgabel/orbit2/pull/14). See [worked articulation examples](../usage/sound-and-instruments.md#2-create-distinct-attacks-and-releases) and the [envelope contract](../audio/envelopes.md).

### Musical instrument synthesis (Phase 7.2 — partial)

Add an **Instrument — …** composite for a sustained organ, flute, brass, string or didgeridoo-like voice. Connect Pitch Control or Note Sequencer to Pitch (st) for interval-preserving tuning. For held keyboard notes, the five **Playable — …** variants (7.2.6a implemented) expose Pitch, Gate and Velocity for Note Source; connect all three and Audio to Master. The **Resonator** processor rings in response to audio excitation. Four **Modal — …** composites (handpan, bell, gong and wood percussion, 7.2.5 first stage implemented) combine individually tuned resonators with a short internal AD noise burst. Connect Manual Trigger or Clock to Trigger and Audio to Master output; open the composite to edit the modes. Four new **Playable — …** modal variants (7.2.6b in review) accept Note Source Trigger, Pitch and Velocity directly, with independent optional external excitation. For manual keyboard playing, add **Note Source** (7.2.3), select it and arm its keyboard after starting playback. See [usage walkthrough](../usage/sound-and-instruments.md#playable-sustained-instruments), [Note Source contract](../instruments/pitch-control.md#phase-723--note-source-in-review), [instrument recipes](../examples/instrument-recipes.md) and [physical modelling](../instruments/physical-modelling.md). MIDI, Note-mode patterns and bounded polyphony remain planned.

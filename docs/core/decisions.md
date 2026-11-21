# Decisions and open questions

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

## 15. Decisions and open questions

### Agreed or recommended architectural direction

- Use C#/Blazor for application logic and presentation; Faust/WASM for the real-time processing.
- Model every generator, processor and composite with a common node contract.
- Treat modulation as a connection of signals to parameter inputs.
- Keep base values separate from DSP-effective values.
- Own topology in SignalGraph and separate its state from canvas geometry.
- Build a single universal graph editor with nested canvases.
- Represent a drone as a composite graph, not a dedicated monolithic engine.
- Use named Input Rack and Output Rack ports as explicit graph boundaries.
- Keep boundary identity stable across renames and reordering.
- Run all audio-rate computation on a shared authoritative audio clock.
- Provide transport and phase accumulation as distinct concepts.
- Start envelopes with oscillator-edge detection; add reusable typed triggers, gates and clocks with sample-rate semantics, independent of graph edit events.
- Use validated commands and graph/patch-level events with node identification.
- Publish changes only after complete validated transactions.
- Prefer combined/flattened Faust processing for the long term; verify initial simpler execution first.
- Observe actual graph signals through bounded passive taps and shared analysis subscriptions; keep live telemetry and chart settings separate from DSP graph state. Use response calculations matching the real DSP rather than simulated live audio.

<a id="to-decide-during-implementation"></a>

### Original design questions and current resolution

The table preserves the initial recommendations for context; the status column distinguishes decisions already made from questions that still need further implementation.

| Question | Initial recommendation | Current resolution |
|---|---|---|
| Runtime partitioning | Start fixed small Faust graph, then prototype whole-graph compilation; partition only if justified. | Whole-graph compilation and nested flattening implemented; alternative partitioning remains optional. |
| Parameter mapping | Explicit target scale and units; linear, cents, dB, then custom mappings. | Explicit units, scale, display scale and bounded modulation implemented; semitone mapping in 7.2.2. |
| Audio/modulation compatibility | Explicit port semantics with safe adapters; allow audio-rate FM. | Typed ports and validation implemented, including explicit event adapters in 7.1.2. |
| Multiple input connections | Per-port policy; explicit mixer for complex audio routing. | Per-port fan-in and weighted mono mixer implemented. |
| Feedback | Reject cycles first; add explicitly delayed feedback after tests. | Editable cycles still rejected; delay/reverb internal feedback implemented, explicit graph feedback planned. |
| Rack count | One Input Rack and one Output Rack per composite initially. | One input and one output rack per composite implemented, editable stable ports. |
| Transport sync | Define phase lock/reset versus rate sync separately. | Free and synchronized oscillator timing implemented; long-duration sequence precision extension planned. |
| Topology changes | Prepare/compile asynchronously, preserve last good runtime, fade swap; improve state transfer later. | Worker compilation, revision rejection, last-good recovery and crossfade implemented; arbitrary state transfer planned. |
| Preset templates | Independent copies initially; linked/versioned definitions later. | Independent composite instances and individual parameter presets implemented; linked/versioned composite reuse planned. |
| Undo history | Store reversible commands/change sets, including multi-graph operations. | Snapshot-based bounded graph history and grouped gestures implemented; annotation history planned. |
| Discrete parameters | Distinct configuration contract where continuous modulation is inappropriate. | Structural choice metadata and conditional activation implemented. |
| Signal tap capacity | Prototype fixed channels versus precompiled selectable slots with explicit master isolation; profile before choosing a bounded capacity, never recompile for ordinary plot mode/visibility changes. | Bounded auxiliary channels and isolated master implemented; urgent 6.18.8 replaces static assignment with demand-driven reusable slots; multi-drone profiling and advanced analysis remain planned. |

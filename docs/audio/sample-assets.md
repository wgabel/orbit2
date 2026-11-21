# Sample-based synthesis and audio assets

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [dsp engine](dsp-engine.md) · [persistence and history](../editor/persistence-and-history.md) · [graph model](../core/graph-model.md).

<a id="sample-based-synthesis-and-audio-assets-planned-phase-8"></a>

### Sample-based synthesis and audio assets (Phase 8 — planned)

Imported recordings (voice, instruments, field recordings) are reusable immutable assets, not graph parameters or DSP source code. A `SamplePlayer` generator reads an asset and emits an ordinary audio signal, allowing the same routing, modulation, nesting and effects as oscillator generators. Other sample-based generators reuse the asset subsystem.

**Storage and ownership.** Introduce a domain asset identity and data-only metadata (ID, name, format, duration, source sample rate and channel count). Application interfaces such as `IAudioAssetStore` and `AudioAssetLibraryService` manage importing, listing, resolving and validating assets. Browser infrastructure stores original encoded bytes as IndexedDB blobs in a separate asset store; the patch document holds stable asset IDs, not PCM arrays or base64. A decoded PCM cache is runtime-only and bounded by memory usage. Multiple node instances can share one immutable asset while retaining independent parameters, playheads and DSP state. Replacing a recording creates a new identity; do not overwrite data referenced by saved patches or undo history. Deletion must account for saved patches, recovery, active sessions, history and in-flight runtime builds; defer automatic garbage collection until reference tracking is reliable. Browser storage is local, subject to quota and eviction; report failures and retain existing assets and patches.

~~~mermaid
flowchart TD
    F[Imported recording] --> L[Audio asset library]
    L --> I[(IndexedDB binary assets)]
    L --> D[Browser decoder]
    D --> B[Decoded PCM cache]
    P[Patch snapshot with asset IDs] --> R[Audio runtime preparation]
    B --> R
    R --> G[Faust graph and AudioWorklet]
    G --> O[Audio output]
~~~

**Playback contract.** The initial generator supports one-shot, forward/reverse looping and ping-pong playback; amplitude, signed playback rate (pitch and time change together), start/end region and loop crossfade are continuous controls where appropriate. Use the existing modulation and conditional-parameter mechanisms; an explicit mode defines whether a playhead advances normally or follows a modulatable normalized position (scrubbing). Treat mode and asset selection as discrete configuration, not numeric modulation. Begin with mono output and an explicit downmix policy for stereo; preserve original channels so multichannel support can follow. Define region bounds, interpolation, zero-rate behavior, end-of-file silence and restart semantics. Trigger/gate integration follows the clock/trigger node contract when available. Waveform previews use decimated peak data, not audio-thread callbacks.

**DSP integration.** Prototype Faust `soundfile` indexed reads and the installed FaustWasm runtime before choosing the binding strategy. Keep decoded PCM and resource manifests outside generated Faust source; no recompilation solely to embed sample bytes. Resolve all assets and prepare buffers off the render thread before starting or swapping voices. Use actual source/runtime sample rates, interpolated fractional indices, bounded memory, and loop crossfades to prevent clicks. Runtime identity must include the resource bindings as well as DSP topology/source so identical code cannot reuse obsolete assets. Cancel stale preparations by revision, keep the last good graph on failure, and reuse the existing crossfade and disposal lifecycle. Do not promise playhead continuity across arbitrary structural swaps until tested. No sample arrays pass through Blazor interop per audio block.

**Persistence and portability.** Version the patch schema for asset references with backwards-compatible migration of existing asset-free patches. Asset selection is a validated graph command, participates in snapshots, undo/redo and autosave, and preserves IDs across nested graphs. Missing assets remain explicit unresolved references with relink UI; a runtime requiring missing data must not silently play substitute audio. Extend patch export with a versioned self-contained `.orbit` archive (JSON manifest and binary assets); keep existing JSON import/export for asset-free patches and clearly flag reference-only exports. Validate archive size, paths, formats, hashes and references before committing imported data; deduplicate immutable assets and remap conflicting IDs transactionally. No cloud upload is required.

**Future consumers.** `GranularGenerator` reads overlapping enveloped grains from the same asset, with modulatable position, grain length, density, pitch, spread and deterministic random seed where appropriate. `WavetableGenerator` uses separately prepared periodic tables derived from recordings, with interpolation and anti-aliasing; an arbitrary speech segment is not necessarily a suitable single cycle. `EnvelopeFollower` is an audio-input processor emitting a modulation signal with attack/release and gain, usable with recordings or any other audio source; it does not need direct asset ownership.

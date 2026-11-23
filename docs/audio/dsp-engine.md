# Faust and real-time execution

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [signals and ports](../core/signals-and-ports.md) · [transport and timing](transport-and-timing.md) · [parameters and modulation](../core/parameters-and-modulation.md) · [audition](../editor/audition.md).

## 10. Faust and real-time execution

### Runtime foundations and safety

#### Reusable DSP implementations

Small Faust components should accept the signals they need rather than hardcoding UI sliders and modulation.

~~~faust
import("stdfaust.lib");

// Reusable oscillator: inputs may vary at sample rate.
sineGenerator(frequency) = os.osc(frequency);

gainProcessor(signal, gain) = signal * gain;

// A reusable, simple combined oscillator and gain.
oscillator(frequency, volume) =
    sineGenerator(frequency) * volume;

// Minimal standalone example for a build-time test only.
frequency = hslider("frequency", 440, 20, 20000, 1);
volume = hslider("volume", 0.1, 0, 1, 0.001);
process = oscillator(frequency, volume);
~~~

For a real composed patch, the compiler emits base controls and composes the signal-valued inputs. In particular, an ordinary Faust slider is typically read at control/block rate; it is not the same as a sample-by-sample signal input. Avoid using setParamValue in a high-frequency loop to implement LFO modulation.

Conceptual Faust program with internal modulation:

~~~faust
import("stdfaust.lib");

baseFrequency = hslider("baseFrequency", 440, 20, 20000, 1);
lfoRate = hslider("lfoRate", 0.5, 0.01, 20, 0.01);
depth = hslider("depthHz", 100, 0, 400, 1);

lfo = os.osc(lfoRate);
effectiveFrequency = max(20, min(20000, baseFrequency + lfo * depth));
process = os.osc(effectiveFrequency) * 0.1;
~~~

This proves the important distinction: C# changes the base values and depth; Faust computes the modulation and oscillator phase at audio rate.

#### Runtime options

| Aspect | Separate DSP nodes | Compiled composite graph |
|---|---|---|
| Execution | Runtime worklets/nodes connected by Web Audio | A Faust program containing the connected nodes |
| Structural edits | Often dynamic reconnection | Recompile or use preallocated flexible routing |
| Audio-rate routing | Requires compatible signal ports and channel mapping | Internal signal composition |
| Overhead | Potentially many nodes/worklets and routing buffers | Potentially more compiler optimization |
| Complexity | Easier initial wiring in some cases | Requires graph compiler and safe replacement |

**Selected and implemented direction (Phases 4–5):** compile a flattened, connected patch (or a carefully partitioned graph) to Faust. This permits internal sample-level modulation and avoids a JavaScript object for each DSP step. The initial historical prototype was a fixed graph compiled at build time to verify the execution and modulation path (Phase 2); the dynamic compiler followed in Phase 4. One worklet per independently compilable drone is also a feasible intermediate choice if exported signal inputs/outputs support cross-drone modulation.

#### Graph compilation pipeline

~~~mermaid
flowchart TD
    P[Root SignalGraph] --> V[Validate graphs]
    V --> F[Flatten composite boundaries]
    F --> O[Order dependencies]
    O --> B[Build Faust program]
    B --> C[Compile to WASM]
    C --> E[AudioWorklet execution]
    E --> A[Audio output]
~~~

The implemented `GraphFlattener` resolves Input/Output Rack mappings before dependency ordering. For each mirrored socket pair, it introduces a backend-only mono pass-through expression with one input and one output. The original local edges are mapped to those endpoints without changing their amounts. This preserves sums, fan-out and nested routes; a disconnected input contributes zero. No extra DSP processor or sample delay is introduced. Only the root master is the audio sink.

Child leaf and edge IDs are deterministically qualified by owning graph ID; each synthetic route has a distinct identity derived from the composite and port IDs. The manifest reverses this mapping to the original graph ID and local parameter address. The model's snapshots, rack definitions and canvas state remain unchanged. The graph dependency order includes audio dependencies AND parameter modulation dependencies. A topological sort suffices for acyclic graphs. Any explicit feedback mechanism requires additional state/scheduling and compiler support.

The first implementation lives in the plain .NET `Orbit.Audio` library. `FaustGraphCompiler.Compile(GraphSnapshot)` returns a `FaustGraphProgram` containing the source, graph ID and revision, and a list of `FaustParameterControl` mappings. `GraphDependencyOrder` topologically sorts reachable audio and modulation dependencies. `IFaustNodeEmitter` is the strategy interface for one mono output; `OscillatorFaustEmitter` and `GainFaustEmitter` delegate shared routing, control creation and modulation to `FaustNodeContext`. `IFaustMultiOutputEmitter` (Note Source, 7.2.3 implemented) emits one tuple in declared output order; the compiler lowers separate scalar port addresses, shares the node's action input and audition control, and records per-port tap identities. Each port remains mono; multi-output is not stereo. See [Note Source](../instruments/pitch-control.md#phase-723--note-source-in-review). The master output is the terminal expression. No UI or browser runtime is involved.

The first backend supports linear parameters, applying weighted incoming modulation to the smoothed base value and clamping to its definition. Oscillator frequencies also respect the actual sample rate. Disconnected components do not affect the source; master output without an incoming edge produces silence. Exactly one master output is required. Unsupported reachable node types and cycles are rejected.

Parameter controls use stable IDs and definition defaults in the source; edited base values are supplied separately so base-only changes can reuse compiled factories. `FaustParameterControl.GraphId` identifies its owning canvas. The playback service reads values from the owning graph and routes passive meters back via `GraphParameterTelemetry.GetValues(graphId)`. `IGraphModulationRangeEstimator.Estimate(rootSnapshot, graphId)` resolves the same boundaries so internal parameters account for external sources. Edge amounts currently appear as invariant literal coefficients, so depth edits require compiling another program until explicit depth controls are implemented. The probe CLI and offline WASM tests exercise an external LFO controlling internal pitch and gain and audio returning through the output rack.

The graph compiler produces stable parameter addresses and metadata so the browser audio engine can update base controls. Validate names/escaping when generating Faust source; do not concatenate unchecked user labels into executable code. Cache compiled factories by a structural signature where appropriate.

**Controls optimized away by Faust.** The C# graph control manifest describes the authored signal graph *before* Faust's dead-code optimization. A gated Physical Clarinet or Flute wired to Master with **no Gate input** still compiles as a valid, silent graph; Faust can omit Attack or other controls because the disconnected Gate is a constant zero. `getParams()` lists only the controls that survived in the generated AudioWorklet. During initial binding `FaustNodeFactory` applies the surviving controls and remembers the omitted manifest names for *that worklet only*. A later live edit to an omitted name is a no-op, not a crash. Unknown names, invalid numeric values and ambiguous paths still fail, and a batch is validated before any writes. Structural changes create a fresh worklet and re-resolve the controls, so connecting Gate restores its usable controls. Do not interpret silent playback as a sounding instrument: wire Note Source **Gate → Gate** and optionally **Pitch → Pitch**, then play notes. Test coverage: `tests/audio/FaustCompilerClient.test.mjs`.


#### Updating without clicks

| Change | Runtime operation |
|---|---|
| Move/resize/open node | UI-only; no audio change |
| Set oscillator frequency | Update/smooth existing DSP control |
| Change LFO speed or depth | Update/smooth existing DSP control |
| Add/remove node | Recompile if topology cannot be updated dynamically |
| Create/remove connection | Update routing or compile a new graph |
| Change effect order | Update routing/compile |
| Rename a rack port | Update model/projection, typically no audio compile if stable addresses are unchanged |

The browser integration captures one committed snapshot per Play request and maps it to Faust source and initial values via `FaustGraphPlaybackService`. The pinned complete compiler bundle loads lazily inside `FaustCompilerWorker.js`, coordinated by `FaustCompilerClient`. It compiles outside both the UI and audio threads, and returns a WebAssembly module and metadata to the existing AudioWorklet node factory. The node uses the source-specific factory hash for its processor identity. The shared audio envelope handles start and stop.

Live base-only changes use the active compiler control manifest instead of recompiling. `GraphTopology` compares node/port/parameter definitions and connections (including weights), but ignores base values. `ActiveGraphPlayback` serializes revisions, stores the last accepted topology and values, and maps changed `ParameterAddress` instances to stable control names. The browser engine writes the existing worklet's parameters; [parameter-specific smoothing](../core/parameters-and-modulation.md#base-control-smoothing) smooths the change without resetting oscillator phase. While initial compilation is in progress, the browser coalesces base updates before the initial attack.

Structural changes compile new source and compare it with the current program; identical source reuses the existing voice. A changed program is compiled by the worker and instantiated silently before replacement. `MarkRevisionAsync` supersedes in-flight work, and the browser rejects stale results before connecting them. `AudioEngine` treats the Play task as a logical lifetime that can span multiple voices. `AudioVoice` and `AudioEnvelope` schedule complementary linear gain ramps over 50 ms on the same audio clock, after which the retiring voice is disposed. Compiler failures retain the last accepted graph; Stop releases both voices and invalidates pending replacements. This preserves continuity of playback, not arbitrary DSP state.

Compile outside the audio rendering thread, preferably in an appropriate worker or build pipeline. A new graph should be prepared before swapping it in. Use revision numbers to discard stale compilation results and coalesce rapid structural edits. Keep the last good runtime active if compilation fails; report the error in the UI.

Deployment versions the complete audio and persistence JavaScript module trees, not just their entry points. `BrowserModulePaths` resolves both adapters' imports from the shared `OrbitBrowserAssetVersion` assembly metadata; local builds use default unversioned directories. CI rejects unversioned interop paths in the published editor test. IndexedDB database names and keys do not depend on the asset version.

When swapping graphs, use an appropriate short fade/crossfade and preserve state where possible. A newly compiled oscillator may restart phase and filters/effects may lose state; maintaining continuity across topology changes is a distinct engineering problem. Do not promise seamless arbitrary graph edits until tested. Consider the cost of running old and new graphs during a crossfade.

#### Real-time requirements

- A 128-frame block at 48 kHz represents only about 2.67 ms.
- Avoid allocation-heavy work, blocking I/O, graph compilation or ordinary UI event dispatch inside processing callbacks.
- Use actual sample rate/block sizes and reuse buffers.
- Manage summing levels, master gain and headroom to prevent clipping.
- Use suitable band-limited waveform implementations for saw/pulse etc. to control aliasing.
- Avoid abrupt parameter changes, accidental phase resets and unsafe feedback.
- Measure CPU and underruns with representative complex patches, including multiple drones.

**Performance specification (planned Phase 7.3):** see [performance.md](../../performance.md) for the Performance Monitor, precise metric definitions and availability limits, browser/Faust instrumentation, latency and underrun measurements, UI and compiler timings, benchmark patches, troubleshooting examples, and regression/acceptance criteria. Monitoring is passive and separate from graph state, playback and signal observation; it must not create audible paths or change sound.

**Planned Phase 7.5:** [Stereo audio, panning and oscillator unison](stereo-panning-and-unison.md) proposes channel-aware Faust lowering and an explicit audible channel layout separate from auxiliary observation channels, plus independent unison oscillators within the existing graph compiler. Current mono/scalar lowering and host routing remain the implemented contract. Prioritize 7.3.1, 7.3.2 and 7.3.4 baseline profiling first, including crossfade peak resource usage.

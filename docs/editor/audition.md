# Mute and bypass

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [dsp engine](../audio/dsp-engine.md) · [commands and events](../core/commands-and-events.md) · [signals and ports](../core/signals-and-ports.md).

<a id="node-mute-and-processor-bypass-phase-616"></a>

### Node mute and processor bypass (Phase 6.16 — implemented)

Provide fast, reversible auditioning without deleting nodes, rewiring a graph, changing base parameters or overwriting connection amounts. A compact **M** action in the shared node header mutes a node's complete outgoing signal; a **B** action on an eligible processor bypasses its processing and passes its input signal through. These are distinct operations.

| Node category | Mute (M) | Bypass (B) |
|---|---|---|
| Generators and modulation sources | Silence all outputs | Not applicable (no dry input) |
| Eligible audio processors | Silence all outputs | Pass the original audio input unchanged |
| Composite drones and other composites | Silence all exposed outputs | Deferred until explicit dry routing is defined |
| Boundary racks and master output | Not applicable initially | Not applicable initially |

Bypass eligibility is an explicit reusable node capability, not a UI type switch or an assumption that every `IProcessor` has a meaningful dry path. Initially enable B for effects with one unambiguous compatible audio input/output. Only audio input passes through; modulation inputs continue controlling the inactive wet computation. Nodes can retain both states; mute takes precedence over bypass, and unmuting restores the prior bypass state.

~~~mermaid
flowchart TD
    I[Audio input] --> P[Processor DSP]
    I --> B[Smoothed bypass blend]
    P --> B
    B --> M[Smoothed mute gain]
    M --> O[Audio output]
~~~

For a linear lane ending at a mixer, muting its final node silences the entire lane's contribution, including effect tails, while the other lanes remain audible. This also works when the lane ends at a composite output. Muting an earlier node does not necessarily silence downstream delay or reverb tails immediately. Muting a source affects every outgoing branch, including modulation connections, so a fan-out graph is not equivalent to independently muting a single edge.

~~~mermaid
flowchart TD
    A[Oscillator A] --> E[Effects A] --> X[Mixer]
    B[Oscillator B] --> F[Muted last effect] -.-> X
    C[Oscillator C] --> G[Effects C] --> X
    X --> O[Audio output]
~~~

**Domain and application.** Model `IsMuted` and, where supported, `IsBypassed` as explicit node state separate from `ParameterDefinition` and layout; they are not numeric modulation targets. Introduce validated `SetNodeMutedCommand` and `SetNodeBypassedCommand` with graph/node IDs and desired state, plus typed before/after facts. Unsupported targets and no-op edits must not create revisions or history entries. Commit each action once through `PatchSession`; snapshots, history restoration, nested-graph routing, range projection and autosave observe the same committed state. Both operations are undoable and persisted. Update the patch schema compatibly so legacy patches restore unmuted and unbypassed; explicit states round-trip through named saves, recovery and export. Individual parameter presets do not include these audition switches.

**Live audio.** Extend graph lowering and runtime control manifests with stable graph-qualified host controls for mute gain and bypass blend. Do not encode the flags as structural Faust constants or treat their changes as topology changes. Preserve ordinary parameter controls, input routes, oscillator phase, delay/reverb state and shared transport; smoothly ramp mute toward silence and crossfade between dry and processed signals for bypass. Continue processing both paths initially, even while inaudible; CPU-saving suspension and state restoration are separate future features. Composite mute gates every flattened exposed output at the composite boundary (including modulation outputs), not each child node. Apply the gate before downstream fan-out. Effective modulation/range reporting must account for mute and bypass without pretending to know arbitrary wet signal bounds. Initial playback and crossfade replacements initialize from the current committed flags; stale runtime updates must not overwrite newer states.

**Presentation.** Extend the reusable `NodeFrame` header with compact M/B toggle buttons, accessible names, pressed state, tooltips and visible active styling drawn from the JSON design tokens. Prevent button pointer actions from starting node drags; keep selection, navigation and cable geometry intact. The same controls work in root and nested canvases without DSP-specific Razor. Make muted nodes recognizable even without relying on color alone; retain their wires and parameter editors.

**Implemented 6.16.1 — mute.** `SignalNode.SupportsMute` exposes the shared generator/processor/composite capability; `IsMuted` is a separate graph-owned flag. `SetNodeMutedCommand` validates the target and publishes `NodeMutedChanged` only on change; `GraphNodeSnapshot` captures both fields. Schema 1 `SavedNode.isMuted` is optional and missing flags restore as false. History uses the existing validated reconstruction path. `GraphFlattener` marks only composite output routes with their owning composite's mute state and origin. One host mute control is allocated per reachable eligible graph/node identity (shared across all its exposed output routes), independently of the ordinary parameter/meter manifest. The gate is downstream of the emitted expression and upstream of fan-out: the host initializes its target before playback and a sample-rate-aware 10 ms linear slew reaches exact silence/unity without recreating the voice. `GraphTopology` ignores mute state while playback resolves control targets from the latest accepted snapshot; topology replacements initialize from committed flags. Range estimation reports the settled muted output as zero even when the wet range was unknown; effective meters remain actual DSP observations during the ramp.

**Implemented 6.16.2 — bypass.** `AudioEffectProcessor` explicitly declares a compatible single dry audio path, validates its port contract and exposes the stable `BypassInputKey`. Gain, Filter, Parametric EQ, Delay, Reverb and Chorus opt in; Mixer remains a routing processor with no B action. `IsBypassed`, `SetNodeBypassedCommand` and `NodeBypassedChanged` follow the existing independent mute state and command pattern; optional schema 1 `SavedNode.isBypassed` restores false for legacy documents. `FaustAuditionControls` and `FaustAuditionControl` replace the mute-only manifest with one shared kind-qualified host control system for both flags. A bypass blend of 1 sends the weighted dry audio input unchanged; 0 sends the ordinary processor output. The wet computation and all modulation dependencies remain running. The shared 10 ms slew initializes from the committed flag, and the final mute gate takes precedence over the dry/wet blend. No recompilation or processor-specific emitter change is needed for either action. The interval estimator uses the routed dry input bound for settled bypass and zero for mute, including when wet bounds are unknown; temporary fade trajectories are not predicted as settled ranges. `NodeAuditionToggle` renders M/B with distinct active token colors and accessible pressed states inside `NodeFrame`; muted and bypassed parameter controls remain editable. Tests compare dry samples and resumed reverb tails with continuous reference DSP at 44.1/48 kHz, including simultaneous mute/bypass.

**Implementation record:**

- Implemented (6.16.1): reversible node mute with shared header control, persisted state, boundary-wide smooth gain and settled modulation range projection.
- Implemented (6.16.2): explicit effect bypass capability, independent persisted state, shared click-safe audition controls, dry range projection and continuously running wet DSP.

**Deferred routing actions.** Do not implement mute buttons on individual connections, connection solo, global node solo or group mute/solo in this phase. An existing connection amount of zero can temporarily silence a single branch, while endpoint mute covers ordinary linear mixer lanes. Revisit dedicated edge mute only if preserving independent amounts or fan-out auditioning becomes a genuine requirement. If introduced later, edge state must be distinct from configured amount and use the same reversible, click-safe runtime-control approach. Solo scope and multi-output bypass routing require explicit contracts rather than implicit graph traversal.

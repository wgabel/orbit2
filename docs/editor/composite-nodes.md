# Composite nodes and boundary racks

[Architecture overview](../architecture/overview.md) · [Roadmap](../roadmap/overview.md)

Related: [graph model](../core/graph-model.md) · [signals and ports](../core/signals-and-ports.md) · [commands and events](../core/commands-and-events.md) · [canvas](canvas.md) · [persistence and history](persistence-and-history.md).

## 7. Hierarchical signal graphs and canvases

### Composite nodes

This is the architectural feature that replaces separate hardcoded drone and patch editors.

A root patch is a SignalGraph. A drone is a composite node containing another SignalGraph. The subgraph may itself contain other composites, subject to reasonable nesting and recursion validation.

~~~mermaid
flowchart TD
    Root[Root patch graph] --> D1[Drone A graph]
    Root --> D2[Drone B graph]
    Root --> E[Master effect graph]
    D1 --> M[Modulation subgraph]
    D1 --> R[Effects subgraph]
~~~

Externally, each child appears as an ordinary node with named input and output ports. Internally, opening it reveals generators, modulators, processors, mixers, connections and boundary racks.

~~~csharp
public sealed class SignalGraph
{
    public Guid Id { get; init; }

    public IReadOnlyList<ISignalNode> Nodes { get; internal set; }
        = Array.Empty<ISignalNode>();

    public IReadOnlyList<SignalConnection> Connections { get; internal set; }
        = Array.Empty<SignalConnection>();
}

public interface ICompositeNode : ISignalNode
{
    SignalGraph InternalGraph { get; }
}
~~~

Actual mutation methods should protect graph invariants. The above represents an illustrative read model; the application command handler or domain operations own controlled mutation, rather than exposing freely modifiable Lists.

The initial domain implementation now creates an owned child graph via `CompositeNode`, with mandatory `InputRack` and `OutputRack` instances. A composite cannot be inserted into itself, an ancestor, or two graphs simultaneously. Concrete node instances cannot be shared across graphs, so mutable parameters remain independent. Child graphs reject master outputs. Snapshots recursively capture child graphs, revision and rack IDs; `GraphSnapshot.GetGraph` resolves a graph within that immutable patch. The session routes commands to owned descendants, and the editor uses the active canvas graph ID.

### Reusable modules and presets

See [Reusable composite modules (Phase 7.4)](reusable-modules.md) for implemented selection-to-composite, single-level ungrouping in review, and planned persistent custom Module Library, whole-patch conversion, portable graph templates and macro controls. This section remains the record of the existing composite and built-in factory foundation.

A saved composite definition consists of its graph, exposed ports and optional canvas layout. This supports:

- Drone presets and independent instances.
- Reusable effect racks and modulation systems.
- Grouping selected nodes into a composite and exposing crossing edges.
- Expanding/ungrouping composite nodes.
- Copying graph instances, including independent DSP state.

Define whether a preset is copied at instantiation or linked to a shared template. Start with independent instances, with stable instance-scoped IDs; do not accidentally share runtime phase, parameter state or filter state. Shared preset editing/versioning is a later feature.

**Implemented Phase 6.6:** The registry accepts composite preset catalog keys separately from the resulting domain type (`composite.graph`). Factory identity and type validation remain intact. Catalog labels are supplied by registration so adding a preset does not require a UI case. `DroneTemplate` shares the starter/layered graph-construction path. The Layered fifth preset creates three ratio-linked oscillators, pink noise, a `MixerProcessor` and final gain with editable Pitch/Gain rack inputs. The mixer exposes a summed mono input; its emitter and range rule use the existing weighted-input resolution. Each instance owns all its nodes, parameters, ports and DSP state independently; no saved-preset persistence is implied.

~~~mermaid
flowchart TD
    C[Preset catalog key] --> F[Composite factory]
    F --> G[Fresh composite graph]
    G --> S[Committed snapshot]
    S --> U[Shared canvas]
    S --> D[Existing Faust compiler]
~~~

## 8. Input and output racks

The user's proposed solution is the standard interface between canvases: a special internal Input Rack node with named ports that automatically appear as input sockets on the composite node in the parent canvas. The corresponding internal sockets are outputs, allowing external signals to be routed to any compatible internal node.

Output Rack is its mirror image: internal input sockets correspond to public output sockets on the composite.

### Parent graph and child graph

~~~mermaid
flowchart TD
    L["Parent LFO"] --> P["Drone: Pitch input"]
    P --> IR["Child Input Rack: Pitch output"]
    IR --> A["Oscillator A pitch modulation"]
    IR --> B["Oscillator B pitch modulation"]
    A --> M[Internal mixer]
    B --> M
    M --> OR["Child Output Rack: Audio input"]
    OR --> O["Drone: Audio output"]
    O --> OUT[Parent mixer/output]
~~~

| Name | Parent composite socket | Inside corresponding rack |
|---|---|---|
| Pitch | Input | Output on Input Rack |
| Volume | Input | Output on Input Rack |
| Cutoff | Input | Output on Input Rack |
| Reverb mix | Input | Output on Input Rack |
| Audio | Output | Input on Output Rack |
| LFO out (optional) | Output | Input on Output Rack |

A single exposed input can fan out internally to multiple consumers. Named pairs represent the same boundary contract rather than two independent parameter values. For the initial version, allow one Input Rack and one Output Rack per composite to keep the interface easy to understand. The composite may have no exposed inputs, e.g. a self-contained drone.

### Shared definitions

~~~csharp
public record ExposedPort(
    Guid Id,
    string Name,
    SignalType SignalType,
    int ChannelCount,
    double? DefaultValue,
    FanInPolicy FanIn = FanInPolicy.Single
);

public interface IInputRack : ISignalNode
{
    IReadOnlyList<ExposedPort> Ports { get; }
}

public interface IOutputRack : ISignalNode
{
    IReadOnlyList<ExposedPort> Ports { get; }
}
~~~

A shared boundary definition is projected into two socket views with opposite directions. The stable port identity is preserved within the composite instance; the node/address distinguishes parent socket from internal rack socket. This is not two separately mutable collections.

The implementation uses immutable `ExposedPort` records (ID, name, signal type, channel count and receiver fan-in). Both socket views are created from that same definition. Port IDs are unique across the composite and names are unique per direction. Racks are graph-owned and cannot be added or removed separately. The domain transaction replaces the complete validated boundary views rather than mutating records in place; IDs remain stable across rename/reorder. Unconnected default values other than zero await further support.

Possible metadata includes units, ranges, unconnected default values, reorder position, labels and channel layout. For audio inputs, an unconnected default is normally silence; for a modulation input, zero is a useful default when the signal is additive. Defaults must be meaningful for the signal's declared type.

### Editing racks

- Add named port: parent composite socket appears automatically.
- Rename port: both views update without breaking connections because addressing uses IDs, not names.
- Reorder port: changes presentation without rewiring the graph.
- Change port type/channel count/fan-in: future operation; revalidate all affected internal and parent edges.
- Remove port: remove every incident connection on both sides in one validated command.
- Expose optional outputs such as internal LFOs, gates or audio buses, not only the final sound.
- Permit no implicit references from a parent directly to a child oscillator: all external signals cross published boundaries.

The implemented `CompositeBoundaryEditor` resolves the child graph's attached owner and parent, prepares a candidate list and uses `CompositeNode.ReplaceBoundary` to validate it before updating the two projections. The selected rack and outer composite share stable socket IDs and mirrored order, while separate port addresses keep connections local to each graph. The reusable `RackPortEditor` and `RackPortRow` send add, rename, move and remove commands through the existing workspace; the UI offers mono Audio and Modulation ports with a selectable receiver fan-in policy. The graph layer, not a listener or UI view, implements synchronization.

The racks do not generate additional samples or require a separate clock. During compilation, the boundary wiring is resolved to internal signal dependencies without artificial audio latency. The root graph's master output represents the physical playback destination; an internal Output Rack represents a graph boundary, not another speaker.

### Boundary visibility and navigation (Phase 6.19.6 — [PR #18](https://github.com/wgabel/orbit2/pull/18))

The Impact — AD percussion composite ([PR #7](https://github.com/wgabel/orbit2/pull/7)) already connects its internal voice oscillator to the Audio socket of the existing Output Rack. Previously the default grid could wrap that rack beneath a tall node and obscure the boundary. The shared canvas now paints boundary racks after ordinary nodes and explicitly labels their direction relative to the parent graph. New child layouts put the Output Rack in a separate column to the right of every existing node. A nested-only **Locate output rack** action selects and fits the rack using its measured bounds, including in previously saved patches. Existing saved positions and graph wiring are preserved; users may still drag the rack and edit its exposed ports. Fit returns to the full graph. No extra generator, Master Output, synthetic port or DSP change is introduced.

**Planned Phase 7.5:** [Stereo audio and unison](../audio/stereo-panning-and-unison.md#3-channel-aware-graph-and-audible-stereo) extends the existing mirrored boundary definitions to supported stereo Audio ports with stable IDs, channel-aware flattening, explicit mismatch validation, compatible grouping/ungrouping and legacy mono preservation. Existing mono boundary implementation remains authoritative until this ships.

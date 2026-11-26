# Orbit 2 Agent Kit — lightweight patch generation library

[Documentation map](../README.md) · [Roadmap: 7.7](../roadmap/advanced.md#phase-77--offline-agent-kit-planned) · [Persistence](../editor/persistence-and-history.md) · [Music instructions](../agents/music-instructions.md)

## Scope and architectural decision

Planned, owner-approved direction: agents and developers construct and edit native Orbit 2 patch JSON independently of a running Orbit 2 application. Provide a small versioned Python library, generated capability catalog, patch schema, validator, executable examples and instructions. Python standard library is preferred; no browser, AI service, .NET build, Faust compilation, DSP, audio rendering, REST, MCP, database or editor runtime is required by consumers. PowerShell can invoke Python and its CLI. Do not implement a second synthesizer, composition compiler or genre generator. The application remains the authority for playback and native import. Offline validation is not proof of audio quality.

The kit is a separate tooling subsystem (proposed root `tools/agent-kit/`, subject to S1 audit); production runtime must not depend on Python or kit artifacts. The native metadata exporter may depend on existing native registry/serializer contracts, never the reverse. Use existing musical documentation and reference assets; do not duplicate their ownership.

## Source of truth and release contract

Use the native NodeRegistry, stable type/parameter/port keys, factory defaults, PatchDocumentSerializer and SignalGraphValidator as the starting owners (see linked native contracts). S1 must inspect real code and tests, record exact shapes and determine metadata extraction gaps before implementing. Export metadata and canonical native example documents using the native factories/serializer where possible; do not hand-maintain competing node classes or guessed JSON. Include only currently supported capabilities; distinguish planned nodes and inactive conditional ports. Native behavior wins over the kit.

The release kit contains a manifest (kit version, native build/commit, patch schema version, catalog format version, checksums), catalog, schema, Python package, quick start and API reference, selective music-document routing/index and executable examples. Publish an immutable versioned ZIP alongside the matching app deployment; the latest pointer must identify the actually deployed build rather than an arbitrary main revision. Pin a matching kit for each generation. Work offline after extraction, with no package installation beyond supported Python if practical. Fail clearly on incompatible schema/catalog versions; never silently migrate or fabricate unsupported fields. New ordinary nodes become available through generated metadata without Python code changes; new structural features may require explicitly tested adapters. Missing metadata is a release failure or explicitly reported unsupported capability, not a silent fallback.

Catalog metadata includes stable type key, labels/category, factory defaults, parameter IDs/keys/types, units, ranges, discrete choices, scale, modulation eligibility and mapping, structural/conditional behavior, port identities/direction/signal types/channel count/fan-in, permitted conversions and amount semantics, dynamic port rules, state/configuration schema, presets, usage descriptions, limitations and references. Generate technical data from native sources; human-authored usage notes should link their owning docs. Keep compact summary discovery and per-node detail so agents load only relevant data. Do not infer node support from an illustrative snippet.

## Library responsibilities and proposed API

A generic data-driven Node facade wraps native serializable state; no class per oscillator or effect. Separate responsibilities (exact module names subject to audit): catalog, patch/graph, nodes, parameters, connections, modulation, sequencing, composites, presets, layout, validation, serialization and CLI. Keep implementation small; split only where useful. All public operations have explicit IDs, structured errors, clear types, and documented units. Optional helpers translate convenience inputs into verified native representations, never simulate DSP.

Illustrative API (not current functionality or actual node keys):

```python
from orbit2 import Catalog, Patch
catalog = Catalog.load()
print(catalog.list_nodes(category="generator"))
definition = catalog.describe("verified-type-key")
patch = Patch.create("Example", catalog=catalog)
source = patch.add_node(definition.key, id="source")
# Real keys and values are obtained from the catalog:
source.set("verified-parameter-key", 110)
# Add a native output and connect using discovered stable ports.
patch.validate().raise_for_errors()
patch.save("example.json")
```

- Discovery: list/search/filter nodes, inspect full parameters/ports/presets/usage, find compatible endpoints and conversion/amount constraints; no guesswork.
- Graph: create/load/inspect/save native patches; add/remove/duplicate/rename nodes, set parameters individually or in batches, query connections, connect/disconnect/replace routes, preserve graph ownership. Use human-friendly script aliases separately from valid native identifiers. Handle nested graph scopes and deterministic ID mapping when cloning; remove dangling references on explicit removal. Failed operations must leave existing data unchanged.
- Modulation: use canonical signal connections and target parameter mapping; expose native depth/amount and units, multiple sources and supported ranges, not a second modulation matrix. Do not compute live values.
- Sequencing: expose actual sequence data, timing, notes/rests/gates/ties, articulation and automation only as supported; reject unsupported note durations, velocity or polyphony rather than inventing them. Helpers can expand Python loops or note names only where lossless and documented.
- Composites/presets: inspect/edit nested graphs, rack ports, boundary identity and supported macros; instantiate independent copies with remapped references and preserve existing preset semantics. Unknown complex contracts must be reported until supported.
- Editing: load/save without musical or presentation data loss, retain unknown fields when safe and reject unsupported edits where preservation cannot be guaranteed. Preserve IDs, annotations, layout, tempo, configuration and unmodified nested state. Do not silently drop unknown data or change schema. Optional graph tracing, subgraph duplication, patch comparison and merge follow core support.
- Layout: optional deterministic left-to-right signal-flow placement respecting nested canvases and valid coordinate/zoom limits; never rearrange existing patches without explicit request. Separate layout from musical graph.
- CLI: thin wrapper over same library; `python -m orbit2 nodes list`, `nodes describe <key>`, `patch inspect <file>`, `patch validate <file>`, optional `patch layout <file>`. Machine-readable JSON output and stable exit codes; PowerShell runs `python create_piece.py`. No server or stateful remote sessions.

## Validation and compatibility

Validate required JSON fields/types, finite numbers, supported schema versions, stable IDs and uniqueness/scopes, node definitions, parameter bounds/choices, port direction/type/channel compatibility, native conversion and amount rules, fan-in, duplicate/dangling connections, cycle policy including modulation and nested boundaries, conditional ports, sequencing and composite payloads, layout/navigation constraints and output requirements. Distinguish native import errors from advisory musical/graph warnings (e.g. disconnected branches or closed gates). Return stable error codes, graph/node/port/parameter path, expected/actual and actionable details. Never silently clamp or rewrite caller intent. Save validated documents atomically; failed load/edit/save leaves input intact.

Native tests must independently deserialize kit-produced files using the real serializer and validator; compare normalized native state and round-trip untouched documents (including complex/nested examples). Test each exported node factory, parameter/port constraints, invalid fixtures, catalog completeness, version mismatch and representative examples. Where Python cannot faithfully reproduce a native rule, fail closed or explicitly mark the check unsupported; do not claim full native equivalence. CI regeneration/diff checks prevent catalog drift; run native compatibility checks for registry, serialization or schema changes and before publishing. Do not require consumers to run native CI locally. No playback, clipping, Faust compilation success, CPU or listening claims from a graph validator.

## Agent workflow and evidence

For music creation or patch modification select the agent-kit route together with all applicable music, sound-design and native feature routes. Read the current manifest and only relevant definitions, native/usage instructions and validated musical examples. Verify kit availability; until implemented use verified native contracts and disclose fallback. Once available, prefer the kit over hand-written JSON or reconstructed per-chat builders; use Python for original composition, layering, phrase variation, articulation and native graph operations. Do not silently bypass validation. Deliver native JSON, generation script when possible, kit/build identity, validation result and limitations. For existing patches retain successful musical material and modify the requested scope.

Use [whole-piece acceptance gates](../examples/musical-composition-validation.md): score and graph audit still required, acoustic and musical gates remain separate. For patch-only delivery, no rendering is required; mark native playback/listening as unverified until actual evidence, and solicit listener feedback where useful. Never produce a substitute independent MP3 as if rendered by Orbit 2. Native playback remains in Orbit 2; offline rendering is a separate potential feature and not a dependency. Reuse executable reference examples with actual listener evidence, distinguishing native audition, approximation and unverified candidates. Do not treat generic templates as composition rules.

## Suggested distribution layout

```text
orbit2-agent-kit/
  manifest.json
  orbit2/             # generic Python package and CLI
  catalog/            # generated per-node metadata, index, rules, schema
  instructions/       # quick start, API reference, routed music knowledge
  examples/           # scripts, native fixtures, evidence/provenance
```

Repository tests and exporter source need not ship in the minimal consumer archive. Avoid dependency on paths outside the extracted kit; include or resolve version-pinned documentation and referenced examples without forcing a full repository checkout. Exact packaging and Python version are S1 decisions.

## S1 native contract audit

Source inspection pinned to [main at 36375a8](https://github.com/wgabel/orbit2/commit/36375a8db4a82ce9bc5cb46ae53e06bcd0cb4e10). Findings below describe inspected source, not executed compatibility results.

### Native ownership and extraction boundaries

| Contract | Source and implications |
| --- | --- |
| Registry | [BuiltInNodes](../../src/Orbit.Application/Graphs/BuiltInNodes.cs) composes the leaf registry and editor registry; [NodeRegistry](../../src/Orbit.Application/Graphs/NodeRegistry.cs) exposes sorted RegisteredTypes, Describe and Create. Enumerate the editor registry, create detached instances and inspect actual model fields. Creation/catalog key and persisted TypeKey differ for composite presets; emit both and classify unsupported structural creation separately. Never save a preset key as a leaf type. Existing registry tests assert 30 leaf registrations at this revision; do not hardcode this count into the exporter. |
| Parameters | [ParameterDefinition](../../src/Orbit.Domain/Parameters/ParameterDefinition.cs) supplies numeric bounds, choices, scale, unit, smoothing, display scale, recompile and activation; [Parameter](../../src/Orbit.Domain/Parameters/Parameter.cs) owns finite/choice validation and actual BaseValue. Export factory BaseValue separately from definition DefaultValue. Map each modulation socket ID to its stable port key on the same instance; do not export random instance IDs as catalog IDs. |
| Ports and identity | [PortDefinition](../../src/Orbit.Domain/Ports/PortDefinition.cs) carries direction, signal type, channel count, fan-in and conditional activation; [SignalNode.RestoreIdentity](../../src/Orbit.Domain/Nodes/SignalNode.cs) reconstructs exact leaf port/parameter key sets using saved GUIDs and current definitions. Export all sockets and parameters, including dormant ones. Generate unique nonempty GUIDs for new native identities; script aliases are not IDs. |
| Persistence | [PatchDocument](../../src/Orbit.Application/Persistence/PatchDocument.cs) and [PatchDocumentSerializer](../../src/Orbit.Application/Persistence/PatchDocumentSerializer.cs) own schema 1 and camelCase source-generated JSON. Capture from PatchSession snapshots, restore via native registry and graph operations. Deserialize alone is not validation; use Restore and navigation reconstruction. |
| Native routing | [SignalGraphValidator](../../src/Orbit.Domain/Graphs/SignalGraphValidator.cs) owns same-channel matching, same signal type plus Clock→Trigger and Audio→Modulation, finite signed amounts (event destinations require 1), direction, single fan-in, duplicate endpoint/edge IDs and graph dependency cycles. RoutingRejection is public, compatibility helper private. S3 should derive tested conversion/amount matrices from the native validator rather than independently hardcoding guessed policy. |
| Editor import | [CanvasNavigationService](../../src/Orbit.Presentation/Canvas/CanvasNavigationService.cs) reconstructs known graph layouts and verifies active graph exists; [CanvasViewport](../../src/Orbit.Presentation/Canvas/CanvasViewport.cs) validates finite pan and zoom 0.25–4.0 (ratio, not percent). [MusicalTransport](../../src/Orbit.Domain/Timing/MusicalTransport.cs) validates finite tempo 20–300 BPM, default 120. Native restore also validates annotations and their ownership. Test full native model + presentation reconstruction without a browser. |

### Exact persisted shape and complex payloads

- Root: schemaVersion, patchId, rootGraph, beatsPerMinute, activeGraphId, canvasLayouts; optional overview has native defaults/validation. Graph: id, nodes, connections. Leaf: id, typeKey, parameters [{key,id,baseValue}], ports [{key,id}], composite:null, isMuted, isBypassed. Edge: id, source and destination each {nodeId,portId}, amount. Saved canvas: graphId, panX, panY, zoom, positions [{nodeId,x,y}], optional visualization/annotation fields as defined by native records. The existing [harmonic drone JSON](../examples/foundations/breathing-harmonic-drone/reference.orbit2.json) has this structure; its 0.25 zoom is valid. Serializer uses numeric enum values unless a native converter says otherwise; generate fixtures rather than assume names.
- Leaf definitions are reconstructed rather than persisted; exact complete key sets matter. Restore performs specific legacy migrations (oscillator pulse width/pitch tracking, envelope modes, sequencer slots and articulation). S4 produces current complete documents; S6 must not blindly mimic migrations or promise compatibility for arbitrary older files. Unknown fields and input bytes should be preserved on untouched Python round trips; native normalization is a separate comparison.
- Composite payload stores name, inputRackId/outputRackId, exposed input/output definitions, nested graph and optional macros with bindings. Capture excludes internal rack nodes; restore recreates them with saved identities. Catalog aliases are not serialized types. Clone remapping requires graph/rack/port/parameter/connection/macro references, so S9 owns this work. Root master versus child output rack is a domain distinction; audible output is not guaranteed by native import alone.
- [NoteSequencerGenerator](../../src/Orbit.Domain/Nodes/Generators/NoteSequencerGenerator.cs) uses fixed-capacity 64-slot numeric parameter arrays (pattern-step-NN, gate-length, velocity and timing-offset families), RestCode 129 and explicit inherited velocity sentinel -1. This is not a generic arbitrary note-event array. S7 must export native choices, slot counts and mapping rules, retaining all inactive slots. Generic metadata export must not omit these fields.
- Extraction gaps: human usage/category tags, rich preset descriptions, portable dynamic/composite factory data and native legacy migrations are not provided by NodeRegistry.Describe. Export structural metadata directly; classify coverage and link existing human-authored usage documentation rather than infer semantics from names. Activation predicates are structured native metadata, not executable Python conditions; inspect/serialize their full clauses during S2. Don't equate conditional activation with native rejection of a stored connection. S3/S8 must test each behavior explicitly.

### Implementation layout and first exporter contract

Keep the selected repository boundary `tools/agent-kit/`. Choose Python 3.11+ standard library for consumer tooling (no pip dependencies; test Windows and Linux); this is a tooling decision, not a requirement on the app. S2 adds a standalone .NET 10 console exporter under `tools/agent-kit/exporter/`, referencing Orbit.Application and transitively Orbit.Domain only. Both are plain .NET libraries; no Blazor, browser or Faust dependency is required for registry inspection. Production projects must not reference the exporter or Python. S3 may add a separate native test harness referencing Orbit.Presentation to exercise viewports. Put Python sources under `tools/agent-kit/orbit2/` and tests under `tools/agent-kit/tests/`; package generated data with the distribution, never parse C# or depend on source files at consumer runtime.

S2 output should have a catalog-format version, sorted index keyed by creation key, separate persisted type key and leaf/composite/preset classification, stable parameter and port keys, actual defaults and definitions, modulation-key associations and structured activation. Use ordinal sort for deterministic export, invariant JSON, no timestamps or generated instance GUIDs in metadata. Canonical fixture documents (S3) need deterministic identity normalization or stable fixture IDs separately; do not compare raw factory-generated GUIDs. Record native build identity in release manifest later. Every registry creation entry must be represented or explicitly unsupported with reason. The initial exporter is read-only and must not alter production registry semantics.

### Existing baseline and compatibility gates

Existing tests inspected: [NodeRegistryTests](../../tests/Orbit.Domain.Tests/NodeRegistryTests.cs) (registry coverage, independent identities, preset behavior, discrete values, unknown keys); [SignalGraphTests](../../tests/Orbit.Domain.Tests/SignalGraphTests.cs) (fan-in, duplicate IDs/edges, missing endpoints, incompatible signal/channel types, cycles and nonfinite amounts); [PatchPersistenceTests](../../tests/Orbit.Presentation.Tests/PatchPersistenceTests.cs) (native serialize/deserialize/restore, nested identity and viewport round trip, preset values, unsupported schema and invalid graph rejection). The [build workflow](../../.github/workflows/build-and-deploy.yml) runs both .NET suites in Release with SDK 10.0.x. No DSP assets or browser needed for these focused baseline checks.

Run from repository root before S2:

~~~powershell
dotnet test tests/Orbit.Domain.Tests/Orbit.Domain.Tests.csproj --configuration Release --filter "FullyQualifiedName~NodeRegistryTests|FullyQualifiedName~SignalGraphTests"
dotnet test tests/Orbit.Presentation.Tests/Orbit.Presentation.Tests.csproj --configuration Release --filter "FullyQualifiedName~PatchPersistenceTests"
~~~

Baseline verified using [PR #153 build job](https://github.com/wgabel/orbit2/actions/runs/45778891772/job/139769217160) at tested PR head `2fb17a43ca7f442ab9b91610c2e643e22741c83f`: both full native domain and presentation test steps succeeded. [Comparison to current main](https://github.com/wgabel/orbit2/compare/2fb17a43ca7f442ab9b91610c2e643e22741c83f...6fd63c4de3d0416d99bbabd564e3a5f988085f92) shows documentation-only changes (including routing metadata); no native source, test or workflow differences. This is existing CI evidence, not a newly executed local baseline. S1 is complete with merged audit [PR #155](https://github.com/wgabel/orbit2/pull/155). For later slices generate positive/negative native fixtures and test Python-produced JSON via Deserialize → Restore → CanvasNavigationService; compare normalized recaptured documents, record exact tested revision and reject unsupported features explicitly. Build/CI scope must eventually include `tools/agent-kit/` without triggering unrelated app deployment; inspect CI change-scope policy when implementing that integration, not in this audit.

## S2 catalog exporter

Merged implementation ([PR #157](https://github.com/wgabel/orbit2/pull/157)) in `tools/agent-kit/exporter/` is a standalone .NET 10 console executable referencing only Orbit.Application and its domain dependency. Generate the catalog locally (SDK required only for the publisher, not consumers):

~~~powershell
dotnet run --project tools/agent-kit/exporter/Orbit.AgentKit.Exporter.csproj --configuration Release -- catalog.json
~~~

Format 1 has `catalogFormatVersion` and an ordinal-keyed `nodes` object. Each entry identifies its creation key, persisted type key, label, native marker category and metadata status. Leaf entries include every parameter and port (including dormant controls), factory base values versus definition defaults, bounds, choices, units/scales/smoothing, structural flags, display anchors, modulation port keys, and activation as an array of OR alternatives containing AND conditions. Enum names in this catalog are descriptive strings, not the native persisted enum encoding. No instance GUIDs or timestamps are exported. Composite registrations are discoverable with explicit deferred status; GUID-derived boundary keys and nested graphs are intentionally not serialized in S2. Human usage metadata, routing matrices, patch fixtures, selective packaging and composite creation remain later slices.

`AgentKitCatalogTests` in the existing native domain suite checks independent registry determinism, complete key coverage, native leaf values and socket associations, conditional pitch controls, sequencer slots and composite preset aliases. Run `dotnet test tests/Orbit.Domain.Tests/Orbit.Domain.Tests.csproj --configuration Release --filter FullyQualifiedName~AgentKitCatalogTests`. The full domain and presentation suites and build passed in [PR #157 CI](https://github.com/wgabel/orbit2/actions/runs/45787315377/job/139797257750) at head `dfab9ecc10dd58ac333684b2edf8328c7d68300b`; exporter merged. Later main changes do not alter exporter files. A release catalog is not yet published. The existing CI path classification conservatively treats tooling/test changes as runtime builds; separate tooling-only release orchestration remains S12.

## S3a connection rules exporter

Generate the versioned rules JSON independently of the catalog:

~~~powershell
dotnet run --project tools/agent-kit/exporter/Orbit.AgentKit.Exporter.csproj --configuration Release -- --connection-rules connection-rules.json
~~~

Format 1 exports all native signal types and directed compatible signal pairs, the equal-channel rule, mono/single-input event constraints, per-destination amount rules (`finite-signed` or `unity-only`), fan-in behavior, and graph topology constraints (existing same-graph output/input endpoints, unique IDs and endpoint pairs, no dependency cycles). Pair and fan-in tables are derived via native RoutingRejection probes. Channel equality and amount classifications are audited native contracts with runtime probe guards against changed behavior. These are structural rules for already valid ports, not DSP semantics, not a claim that all synthetic channel combinations are supported by the editor, and not a substitute for endpoint ownership/graph validation. The native graph remains authoritative. Dormant socket activation is a separate S8 concern; do not reject stored inactive edges solely on activation.

`AgentKitConnectionRulesTests` exercises deterministic output and compares exported pair/amount/fan-in decisions with actual SignalGraph connections, including rejected routes. Existing SignalGraphTests cover IDs, direction, missing endpoints and cycles. Merged [PR #160](https://github.com/wgabel/orbit2/pull/160); [native CI](https://github.com/wgabel/orbit2/actions/runs/45793774226/job/139819085764) passed at `5e8615405223f853c1e78ff7bafb81e5cc94a02a`. S3b owns native patch document shape and serialized fixtures, including navigation validation; Python builder remains S4.

## S3b native patch contract and fixtures

~~~powershell
dotnet run --project tools/agent-kit/exporter/Orbit.AgentKit.Exporter.csproj --configuration Release -- --patch-contract ./patch-contract
~~~

The output directory contains `patch-schema.json` (structural JSON Schema generated from native persistence DTOs using the tooling-only .NET schema exporter and the native serializer's camelCase/default numeric enum convention), `patch-contract.json` (format/schema versions, native tempo/zoom limits and fixture manifest), and one accepted leaf patch plus five intentionally rejected fixtures (schema version, tempo, viewport, active graph and endpoint). Fixtures use factory-derived complete leaf parameter/port sets and deterministic nonempty identities; serialize via the actual native serializer. The accepted fixture must restore through native graph and navigation before the export returns. CI verifies all negative expectations separately and checks deterministic regeneration and native normalized recapture.

The schema describes serialized shape, not full native acceptance: it is generated from reflection DTO metadata in tooling rather than the internal source-generated serializer context; native tests compare the actual wire shape and exercise the independent restore path. Semantic version, range, node keys, topology, layout and nested graph validation remain native responsibilities, with offline coverage developed in S4–S9. Composite fixture generation and editing remain S9. This slice extends the exporter dependency to framework-neutral Orbit.Presentation solely to reuse actual viewport constraints and navigation checks; it has no dependency on Razor, browser, UI runtime or Faust. Production projects never reference the exporter.

Run `dotnet test tests/Orbit.Presentation.Tests/Orbit.Presentation.Tests.csproj --configuration Release --filter FullyQualifiedName~AgentKitPatchContractTests`. Merged [PR #163](https://github.com/wgabel/orbit2/pull/163); [CI](https://github.com/wgabel/orbit2/actions/runs/45798392221/job/139834875349) passed at `2affa42fda992b53124d688020fe7615877e04fc`. No generated release package claimed.

## S4 minimal Python graph builder

Candidate standard-library-only Python 3.11 package lives under `tools/agent-kit/orbit2/`. Generate the three native exports into one directory (catalog.json, connection-rules.json, patch-contract.json) using S2/S3 commands. From PowerShell at the repository root set `$env:PYTHONPATH = "$PWD/tools/agent-kit"`; consumers of a future extracted kit need only Python and matching data.

~~~python
from orbit2 import Catalog, Patch
catalog = Catalog.load("patch-contract")
patch = Patch.create("Example", catalog=catalog, tempo=92)
osc = patch.add_node("generator.oscillator", alias="lead", frequency=220)
out = patch.add_node("output.master")
patch.connect(osc, "signal-out", out, "audio-in", amount=0.2)
patch.save("example.json")
~~~

Catalog.list_nodes and describe expose actual creation keys and definitions. Node.set and set_parameters validate finite native bounds and choices before mutation; aliases are script-only and GUIDs remain native. Patch.connect enforces basic ownership, endpoint direction, exported signal/channel/amount/fan-in constraints and duplicate/cycle rejection. Native parameter and port identities are complete even when dormant. No automatic placement algorithm yet: empty canvasLayouts delegates placement to the native editor. Leaf-only root graph construction; no load/edit, composite payloads, comprehensive structured validation, musical analysis or rendering. Schema/format versions are checked but build identity pairing is deferred to S12; use exports generated together. Title length mirrors current native overview limit; no generalized annotation API yet.

The existing runtime CI adds a Python 3.11 step generating fresh native exports, running Python tests, constructing a simple modulated graph in Python and importing it through the new tooling-only `--validate-patch` command (Deserialize, Restore, CanvasNavigationService). This is independent native import, not Python self-validation. Native exporter remains optional for consumers. S5 owns broader offline validation/diagnostics. Merged [PR #164](https://github.com/wgabel/orbit2/pull/164); [CI](https://github.com/wgabel/orbit2/actions/runs/45802849756/job/139850272770) passed at `c19f225c91c4ea112a1ec2ff69e278ad81cab8b3`, including Python tests and actual native import.

## S5 offline validation

Merged [PR #166](https://github.com/wgabel/orbit2/pull/166); [CI](https://github.com/wgabel/orbit2/actions/runs/45817551488) passed at `de967d03ed7b3aff63c8f5282bfe7f5fc80008c3`.

`Patch.validate()` and `validate_patch(document, catalog)` return a read-only `ValidationResult` with `is_valid`, structured `errors` (code, JSON path, message, optional expected/actual) and `raise_for_errors()`. `Patch.save` checks the result before opening a temporary file. Validation uses generated leaf definitions, native tempo/zoom/overview constraints and shared builder routing checks. The native fixture manifest is independently exercised by both Python and .NET tests; the existing Python-generated graph still goes through native restore/navigation in CI.

Coverage: current complete root leaf graphs, native-scoped identities, finite bounds and choices, mute/bypass support, ports, routing amounts, fan-in, cycles, overview and simple canvas/navigation. Explicitly reject composite payloads and nonempty annotation/visualization collections; do not migrate legacy node shapes or normalize orphan positions/layouts. These are conservative kit restrictions, not claims that native import rejects all such documents. Unicode tag comparison approximates native ordinal-ignore-case; exact parity for all Unicode and richer presentation contracts remains unverified. S6 owns loading and lossless editing; S7–S9 own advanced structures. No musical-quality or audio validation.

## S6a lossless leaf load and parameter edits

Merged [PR #168](https://github.com/wgabel/orbit2/pull/168); [CI](https://github.com/wgabel/orbit2/actions/runs/45821494299) passed at `532e2074f697c977702cb399600b1f72b19791d8`.

`Patch.load(path, catalog=catalog)` validates the complete S5-supported document before exposing a patch, then keeps its JSON data and original IDs rather than recreating the graph. Use `list_nodes()`, `get_node_by_id(id)`, `Node.type_key`, `Node.get(key)` and the existing atomic `Node.set_parameters`. Saving preserves all unmodified JSON values, including unknown fields, layout, overview, ID spelling and optional field presence; formatting and arbitrary-precision decimal lexemes are not preserved (standard Python JSON number decoding). Duplicate JSON object properties, nonfinite numbers and unsupported S5 documents are rejected rather than silently normalized. Existing add/connect operations remain available, with GUID comparisons case-insensitive. Unknown fields are retained as opaque data, not interpreted or guaranteed compatible with future native builds. S6b owns removal, disconnection, edge updates and reference cleanup; destructive operations must reject cases where opaque references cannot be safely handled.

CI checks untouched parsed round trips, edited native-origin fixtures, invalid edit rollback, and independent native import of both a created and a loaded/edited patch. Advanced presentation collections, legacy migration and nested graphs remain unsupported by S5 validation.

## S6b safe structural edits (candidate)

Implementation: [PR #170](https://github.com/wgabel/orbit2/pull/170) (CI pending).

`Patch.list_connections()` returns detached native edge snapshots; `set_connection_amount(connection_id, amount)` validates a candidate without changing identity, endpoint or order. `disconnect(connection_id)` and `remove_node(node)` validate a candidate before publishing changes. Node removal clears incident edges, saved positions and script aliases; surviving node handles remain usable, removed handles reject edits. Connection IDs and endpoint comparisons are case-insensitive without altering saved GUID spelling. The existing metadata-derived validator checks finite signed amounts and event unity.

Deletion is intentionally conservative: on current S5-supported root leaf documents it checks an explicit cleanup-scope allowlist of understood native DTO fields, not another schema or catalog. Any opaque field anywhere in the document rejects deletion with a structured diagnostic; unknown fields remain supported for non-destructive edits. Nonempty presentation collections, nested graphs and legacy migrations remain out of scope. CI retains created and parameter-edited native import gates and adds a structural edit of the native-origin fixture. S7 owns sequence helpers.

## Non-goals and gates

No runtime editor/UI change, audio engine optimization, offline renderer, musical-quality scoring engine, hosting, REST/MCP or AI-specific backend. S1 must establish actual schema and registry ownership, coverage gaps, dependencies and test strategy; implementation begins only after this evidence. Later work follows the owning roadmap, granular slices and native compatibility gates.

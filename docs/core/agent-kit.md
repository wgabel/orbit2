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

## Non-goals and gates

No runtime editor/UI change, audio engine optimization, offline renderer, musical-quality scoring engine, hosting, REST/MCP or AI-specific backend. S1 must establish actual schema and registry ownership, coverage gaps, dependencies and test strategy; implementation begins only after this evidence. Later work follows the owning roadmap, granular slices and native compatibility gates.

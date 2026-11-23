# Orbit 2 architecture — entry point

[Project](README.md) · [Documentation map](docs/README.md) · [Roadmap](ROADMAP.md)

The detailed [architecture overview](docs/architecture/overview.md) and [task-routing reference](docs/architecture/task-routing.md) are in `docs/architecture/`. This file remains a stable entry point for agents and existing links; it is **not** a duplicate specification.

## 1. Goals and current baseline

[Goals, technology choices and boundaries](docs/architecture/overview.md#1-goals-and-current-baseline).

## Find the context for your task

[Find relevant technical contracts](docs/architecture/task-routing.md#find-the-context-for-your-task), then inspect the owning source and tests.

## Detailed documentation

[Browse the linked feature contracts](docs/architecture/task-routing.md#detailed-documentation).

## Compatibility anchors

Existing deep links into the former architecture file remain resolvable here. Use the [detailed routing page](docs/architecture/task-routing.md#detailed-documentation) to reach their subjects.

<a id="contents"></a>
<a id="2-fundamental-concepts"></a>
<a id="3-signals-and-node-contracts"></a>
<a id="5-ports-and-connections"></a>
<a id="4-parameters-and-modulation"></a>
<a id="9-commands-events-and-transaction-boundaries"></a>
<a id="11-project-organization-and-dependencies"></a>
<a id="15-decisions-and-open-questions"></a>
<a id="to-decide-during-implementation"></a>
<a id="16-references-and-glossary"></a>
<a id="10-faust-and-real-time-execution"></a>
<a id="runtime-foundations-and-safety"></a>
<a id="6-audio-time-oscillator-phase-and-transport"></a>
<a id="algorithmic-reverb-phase-614"></a>
<a id="audio-processors-and-effects"></a>
<a id="chorus-phase-615"></a>
<a id="feedback-delay-phase-613"></a>
<a id="parametric-eq-phase-612"></a>
<a id="pitch-shifter-phase-620-planned"></a>
<a id="envelope-generators-and-rhythmic-triggers-planned-phase-71"></a>
<a id="musical-control-performance-and-instrument-synthesis"></a>
<a id="signal-observation-and-analysis-phase-618"></a>
<a id="signal-observation-and-analysis-planned-phase-618"></a>
<a id="sample-based-synthesis-and-audio-assets-planned-phase-8"></a>
<a id="7-hierarchical-signal-graphs-and-canvases"></a>
<a id="8-input-and-output-racks"></a>
<a id="shared-canvas-and-component-standards"></a>
<a id="signal-analysis-and-visualization-planned-phase-618"></a>
<a id="workspace-persistence-and-history-phases-68611"></a>
<a id="node-mute-and-processor-bypass-phase-616"></a>
<a id="connection-amount-badges-and-inline-editing-planned-phase-6195"></a>
<a id="patch-documentation-and-canvas-annotations-planned-phase-619"></a>
<a id="keyboard-performance-and-continuous-pitch-sequencer-phase-728--planned"></a>
<a id="keyboard-performance-and-continuous-pitch-sequencer-planned-phase-728"></a>
<a id="musical-instrument-synthesis-and-physical-modelling-planned-phase-72"></a>
<a id="12-worked-patch-example"></a>
<a id="14-testing-and-acceptance-criteria"></a>
<a id="13-implementation-roadmap"></a>
<a id="roadmap-at-a-glance"></a>
<a id="reading-guide-and-current-implementation"></a>
<a id="phase-1--domain-primitives-graph-ownership-and-tests"></a>
<a id="phase-2--shared-timing-and-first-sample-rate-modulation"></a>
<a id="phase-3--commands-and-event-dispatch"></a>
<a id="phase-4--flat-canvas-and-dynamic-graph-compiler"></a>
<a id="phase-5--composite-nodes-and-rack-interfaces"></a>
<a id="phase-618--signal-analysis-and-visualization-planned"></a>
<a id="phase-619--patch-documentation-and-canvas-organization-planned"></a>
<a id="phase-7--advanced-functionality-and-performance"></a>
<a id="phase-71--envelope-generators-and-rhythmic-triggers-planned"></a>
<a id="phase-72--musical-instrument-synthesis-and-physical-modelling-planned"></a>


# Orbit 2 architecture — navigation

The technical [architecture overview](docs/architecture/overview.md), [task-by-task routing](docs/architecture/task-routing.md), and [documentation map](docs/README.md) now contain the detailed reference. This root file remains a stable entry point for agents and old external links; it does **not** duplicate the full technical specification.

## 1. Goals and current baseline

See the [architecture goals and design decisions](docs/architecture/overview.md#1-goals-and-current-baseline).

## Find the context for your task

Open the [task routing table](docs/architecture/task-routing.md#find-the-context-for-your-task), then only the relevant feature documents, source and tests.

## Detailed documentation

See the [detailed topic routing](docs/architecture/task-routing.md#detailed-documentation).

<a id="contents"></a>

## Compatibility anchors

The following historical section anchors still resolve here. Follow them to their current, authoritative owner:

<a id="contents"></a>
- [contents](docs/architecture/task-routing.md#contents)
<a id="2-fundamental-concepts"></a>
- [2-fundamental-concepts](docs/architecture/task-routing.md#2-fundamental-concepts)
<a id="3-signals-and-node-contracts"></a>
- [3-signals-and-node-contracts](docs/architecture/task-routing.md#3-signals-and-node-contracts)
<a id="5-ports-and-connections"></a>
- [5-ports-and-connections](docs/architecture/task-routing.md#5-ports-and-connections)
<a id="4-parameters-and-modulation"></a>
- [4-parameters-and-modulation](docs/architecture/task-routing.md#4-parameters-and-modulation)
<a id="9-commands-events-and-transaction-boundaries"></a>
- [9-commands-events-and-transaction-boundaries](docs/architecture/task-routing.md#9-commands-events-and-transaction-boundaries)
<a id="11-project-organization-and-dependencies"></a>
- [11-project-organization-and-dependencies](docs/architecture/task-routing.md#11-project-organization-and-dependencies)
<a id="15-decisions-and-open-questions"></a>
- [15-decisions-and-open-questions](docs/architecture/task-routing.md#15-decisions-and-open-questions)
<a id="to-decide-during-implementation"></a>
- [to-decide-during-implementation](docs/architecture/task-routing.md#to-decide-during-implementation)
<a id="16-references-and-glossary"></a>
- [16-references-and-glossary](docs/architecture/task-routing.md#16-references-and-glossary)
<a id="10-faust-and-real-time-execution"></a>
- [10-faust-and-real-time-execution](docs/architecture/task-routing.md#10-faust-and-real-time-execution)
<a id="runtime-foundations-and-safety"></a>
- [runtime-foundations-and-safety](docs/architecture/task-routing.md#runtime-foundations-and-safety)
<a id="6-audio-time-oscillator-phase-and-transport"></a>
- [6-audio-time-oscillator-phase-and-transport](docs/architecture/task-routing.md#6-audio-time-oscillator-phase-and-transport)
<a id="algorithmic-reverb-phase-614"></a>
- [algorithmic-reverb-phase-614](docs/architecture/task-routing.md#algorithmic-reverb-phase-614)
<a id="audio-processors-and-effects"></a>
- [audio-processors-and-effects](docs/architecture/task-routing.md#audio-processors-and-effects)
<a id="chorus-phase-615"></a>
- [chorus-phase-615](docs/architecture/task-routing.md#chorus-phase-615)
<a id="feedback-delay-phase-613"></a>
- [feedback-delay-phase-613](docs/architecture/task-routing.md#feedback-delay-phase-613)
<a id="parametric-eq-phase-612"></a>
- [parametric-eq-phase-612](docs/architecture/task-routing.md#parametric-eq-phase-612)
<a id="pitch-shifter-phase-620-planned"></a>
- [pitch-shifter-phase-620-planned](docs/architecture/task-routing.md#pitch-shifter-phase-620-planned)
<a id="envelope-generators-and-rhythmic-triggers-planned-phase-71"></a>
- [envelope-generators-and-rhythmic-triggers-planned-phase-71](docs/architecture/task-routing.md#envelope-generators-and-rhythmic-triggers-planned-phase-71)
<a id="musical-control-performance-and-instrument-synthesis"></a>
- [musical-control-performance-and-instrument-synthesis](docs/architecture/task-routing.md#musical-control-performance-and-instrument-synthesis)
<a id="signal-observation-and-analysis-phase-618"></a>
- [signal-observation-and-analysis-phase-618](docs/architecture/task-routing.md#signal-observation-and-analysis-phase-618)
<a id="signal-observation-and-analysis-planned-phase-618"></a>
- [signal-observation-and-analysis-planned-phase-618](docs/architecture/task-routing.md#signal-observation-and-analysis-planned-phase-618)
<a id="sample-based-synthesis-and-audio-assets-planned-phase-8"></a>
- [sample-based-synthesis-and-audio-assets-planned-phase-8](docs/architecture/task-routing.md#sample-based-synthesis-and-audio-assets-planned-phase-8)
<a id="7-hierarchical-signal-graphs-and-canvases"></a>
- [7-hierarchical-signal-graphs-and-canvases](docs/architecture/task-routing.md#7-hierarchical-signal-graphs-and-canvases)
<a id="8-input-and-output-racks"></a>
- [8-input-and-output-racks](docs/architecture/task-routing.md#8-input-and-output-racks)
<a id="shared-canvas-and-component-standards"></a>
- [shared-canvas-and-component-standards](docs/architecture/task-routing.md#shared-canvas-and-component-standards)
<a id="signal-analysis-and-visualization-planned-phase-618"></a>
- [signal-analysis-and-visualization-planned-phase-618](docs/architecture/task-routing.md#signal-analysis-and-visualization-planned-phase-618)
<a id="workspace-persistence-and-history-phases-68611"></a>
- [workspace-persistence-and-history-phases-68611](docs/architecture/task-routing.md#workspace-persistence-and-history-phases-68611)
<a id="node-mute-and-processor-bypass-phase-616"></a>
- [node-mute-and-processor-bypass-phase-616](docs/architecture/task-routing.md#node-mute-and-processor-bypass-phase-616)
<a id="connection-amount-badges-and-inline-editing-planned-phase-6195"></a>
- [connection-amount-badges-and-inline-editing-planned-phase-6195](docs/architecture/task-routing.md#connection-amount-badges-and-inline-editing-planned-phase-6195)
<a id="patch-documentation-and-canvas-annotations-planned-phase-619"></a>
- [patch-documentation-and-canvas-annotations-planned-phase-619](docs/architecture/task-routing.md#patch-documentation-and-canvas-annotations-planned-phase-619)
<a id="keyboard-performance-and-continuous-pitch-sequencer-phase-728--planned"></a>
- [keyboard-performance-and-continuous-pitch-sequencer-phase-728--planned](docs/architecture/task-routing.md#keyboard-performance-and-continuous-pitch-sequencer-phase-728--planned)
<a id="keyboard-performance-and-continuous-pitch-sequencer-planned-phase-728"></a>
- [keyboard-performance-and-continuous-pitch-sequencer-planned-phase-728](docs/architecture/task-routing.md#keyboard-performance-and-continuous-pitch-sequencer-planned-phase-728)
<a id="musical-instrument-synthesis-and-physical-modelling-planned-phase-72"></a>
- [musical-instrument-synthesis-and-physical-modelling-planned-phase-72](docs/architecture/task-routing.md#musical-instrument-synthesis-and-physical-modelling-planned-phase-72)
<a id="12-worked-patch-example"></a>
- [12-worked-patch-example](docs/architecture/task-routing.md#12-worked-patch-example)
<a id="14-testing-and-acceptance-criteria"></a>
- [14-testing-and-acceptance-criteria](docs/architecture/task-routing.md#14-testing-and-acceptance-criteria)
<a id="13-implementation-roadmap"></a>
- [13-implementation-roadmap](docs/architecture/task-routing.md#13-implementation-roadmap)
<a id="roadmap-at-a-glance"></a>
- [roadmap-at-a-glance](docs/architecture/task-routing.md#roadmap-at-a-glance)
<a id="reading-guide-and-current-implementation"></a>
- [reading-guide-and-current-implementation](docs/architecture/task-routing.md#reading-guide-and-current-implementation)
<a id="phase-1--domain-primitives-graph-ownership-and-tests"></a>
- [phase-1--domain-primitives-graph-ownership-and-tests](docs/architecture/task-routing.md#phase-1--domain-primitives-graph-ownership-and-tests)
<a id="phase-2--shared-timing-and-first-sample-rate-modulation"></a>
- [phase-2--shared-timing-and-first-sample-rate-modulation](docs/architecture/task-routing.md#phase-2--shared-timing-and-first-sample-rate-modulation)
<a id="phase-3--commands-and-event-dispatch"></a>
- [phase-3--commands-and-event-dispatch](docs/architecture/task-routing.md#phase-3--commands-and-event-dispatch)
<a id="phase-4--flat-canvas-and-dynamic-graph-compiler"></a>
- [phase-4--flat-canvas-and-dynamic-graph-compiler](docs/architecture/task-routing.md#phase-4--flat-canvas-and-dynamic-graph-compiler)
<a id="phase-5--composite-nodes-and-rack-interfaces"></a>
- [phase-5--composite-nodes-and-rack-interfaces](docs/architecture/task-routing.md#phase-5--composite-nodes-and-rack-interfaces)
<a id="phase-618--signal-analysis-and-visualization-planned"></a>
- [phase-618--signal-analysis-and-visualization-planned](docs/architecture/task-routing.md#phase-618--signal-analysis-and-visualization-planned)
<a id="phase-619--patch-documentation-and-canvas-organization-planned"></a>
- [phase-619--patch-documentation-and-canvas-organization-planned](docs/architecture/task-routing.md#phase-619--patch-documentation-and-canvas-organization-planned)
<a id="phase-7--advanced-functionality-and-performance"></a>
- [phase-7--advanced-functionality-and-performance](docs/architecture/task-routing.md#phase-7--advanced-functionality-and-performance)
<a id="phase-71--envelope-generators-and-rhythmic-triggers-planned"></a>
- [phase-71--envelope-generators-and-rhythmic-triggers-planned](docs/architecture/task-routing.md#phase-71--envelope-generators-and-rhythmic-triggers-planned)
<a id="phase-72--musical-instrument-synthesis-and-physical-modelling-planned"></a>
- [phase-72--musical-instrument-synthesis-and-physical-modelling-planned](docs/architecture/task-routing.md#phase-72--musical-instrument-synthesis-and-physical-modelling-planned)

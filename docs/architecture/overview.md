# Architecture overview and original design goals

[Project](../README.md) · [Task routing](task-routing.md) · [Documentation map](../README.md)


**Status:** Living architecture, implemented contracts and implementation roadmap  
**Implementation and usage audit:** 2026-11-22 · main through [`f66fe0a`](https://github.com/wgabel/orbit2/commit/f66fe0a) (PR #38)
**Target repository:** [wgabel/orbit2](https://github.com/wgabel/orbit2)  
**Original design date:** 2026-11-20

This document consolidates the design discussions for Orbit 2: the original browser boilerplate, separation of responsibilities, signal generation, modulation, audio processing, clocks, hierarchical node canvases, exposed input/output racks, and event-driven editing. It is intended as a reference for implementing the next iterations, not a claim that every proposed system already exists. Earlier implementation sketches, design rationale, examples and acceptance criteria are preserved even when later implementation records supersede the initial wording. The README remains the practical build/run guide. [Performance monitoring and diagnostics](../quality/performance.md) is the dedicated specification for measuring audio reliability, latency, DSP load/headroom, editor responsiveness, compilation and benchmark regressions (Phase 7.3). The [performance targets and explainable health indicators](../quality/performance-metrics.md#31-suggested-health-thresholds) and [device-specific capacity benchmarks](../quality/dsp-profiling.md#94-device-specific-capacity-and-reference-compositions-planned) are planned extensions (7.3.7–7.3.8), with short visible reasons for statuses and device-scoped workload goals.

## 1. Goals and current baseline

### Motivation

Orbit is a browser-based environment for designing synthesizers, drones, modulation systems and musical patches. The original React application provides inspiration for the graph editor, but Orbit 2 is intended to avoid tight coupling between the presentation, application state and real-time audio processing.

The goal is a modular architecture in which new generators, processors, parameter types and composite modules can be added without rewriting the rest of the application.

### Technology choices

| Technology | Responsibility |
|---|---|
| Blazor WebAssembly / C# | Browser UI, graph model, editing, validation and application services |
| Faust | Sample-level sound synthesis and DSP algorithms |
| WebAssembly | Execution format for compiled Faust DSP |
| Web Audio API / AudioWorklet | Real-time audio scheduling and output |
| Minimal JavaScript modules | Browser audio integration and lifecycle; not application business logic |
| GitHub Actions / Pages | Build, test and deployment of the static app |

**Original baseline (historical, Phases 1–2).** The initial implementation included the independent Orbit.Domain graph model (Phase 1) and a continuously running, fixed two-sine modulation graph (Phase 2). The browser bridge, build-time Faust compilation and separated application/UI/infrastructure projects support this playback path. Existing infrastructure concepts include AudioEngine, AudioContextManager, AudioClock, FaustModuleLoader, FaustNodeFactory, AudioVoice, AudioOutput and AudioEnvelope.

**Original next stage — now delivered in Phases 3–4:** validated commands and application events, then a reusable graph editor and general DSP compilation. The active modulation playback path and shared infrastructure provide the baseline.

### Design principles

1. Separation of concerns: UI, graph state, application commands and real-time DSP have different responsibilities.
2. Composition over specialization: a drone is a reusable graph, not a special monolithic synth class.
3. Depend on stable abstractions and use dependency injection at composition roots.
4. Generators and processors must not know which node is connected before or after them.
5. Modulation is a first-class feature of all appropriate continuous numerical parameters.
6. Every signal uses the same authoritative audio timing reference.
7. User-interface operations must not interrupt sound unnecessarily.
8. A new node should normally require a definition, a DSP implementation and registration, not changes to the graph editor's core.
9. Use descriptive names, small cohesive classes, and separate source files for independent objects.
10. Validate graph changes, preserve consistent state and test foundational behavior before adding numerous node types.

# Project organization and dependencies

[Architecture overview](../architecture/overview.md) · [Roadmap](../roadmap/overview.md)

Related: [graph model](graph-model.md) · [commands and events](commands-and-events.md) · [dsp engine](../audio/dsp-engine.md) · [canvas](../editor/canvas.md).

## 11. Project organization and dependencies

The current solution includes Domain, Application, Presentation, Infrastructure, UI and Audio components. Orbit.Domain isolates the graph model from application services and browser-specific infrastructure.

~~~
Orbit.Domain/
  Graphs/
    SignalGraph.cs
    SignalConnection.cs
    SignalGraphValidator.cs
  Nodes/
    ISignalNode.cs
    IGenerator.cs
    IProcessor.cs
    ICompositeNode.cs
    CompositeNode.cs
    InputRack.cs
    OutputRack.cs
  Parameters/
    Parameter.cs
    ParameterDefinition.cs
    ParameterAddress.cs
    ParameterScale.cs
    ParameterUnit.cs
  Ports/
    PortDefinition.cs
    ExposedPort.cs
    SignalType.cs
  Timing/
    IAudioClock.cs
    ITransport.cs

Orbit.Application/
  Graphs/
    NodeRegistry.cs
    Commands/                  # typed graph commands and command handler
    Events/                    # graph facts, change set, internal dispatcher
    Sessions/                  # IPatchSession, PatchSession and result
    Snapshots/                 # immutable graph/node/parameter records
  Notifications/
    NotificationChannel.cs
  Services/
    AudioGraphService.cs
    UndoRedoService.cs
    PatchPersistenceService.cs
  Interfaces/
    IAudioEngine.cs
    IGraphCompiler.cs

Orbit.Presentation/
  Canvas/
    CanvasPoint.cs
    CanvasViewport.cs
    CanvasStateService.cs
    ICanvasStateService.cs
    CanvasStateSnapshot.cs
    GridNodePlacementPolicy.cs
    INodePlacementPolicy.cs
    CanvasStateChange.cs

Orbit.Audio/
  Dsp/
    Generators/
      Sine.dsp
      Triangle.dsp
      Noise.dsp
    Processors/
      Gain.dsp
      LowPass.dsp
      Reverb.dsp
    Utilities/
      Mixer.dsp
      ParameterModulation.dsp

Orbit.Infrastructure/
  Audio/
    BrowserAudioEngine.cs
    FaustGraphCompiler.cs
  wwwroot/audio/
    audio.js
    AudioEngine.js
    AudioContextManager.js
    FaustModuleLoader.js
    FaustNodeFactory.js
    AudioOutput.js
    ...

Orbit.UI/
  Components/
    Canvas/
    Nodes/
    Ports/
    Parameters/
    Navigation/
  Layout/
  State/                       # future navigation and Blazor view state
  Program.cs

Orbit.Tests/
  Domain/
  Application/
  Compiler/
  Integration/
~~~

The exact folders are illustrative and should be adjusted to existing solution naming and conventions, not blindly added all at once.

### Dependency direction

~~~mermaid
flowchart TD
    UI[Orbit.UI] --> APP[Orbit.Application]
    APP --> DOM[Orbit.Domain]
    INF[Orbit.Infrastructure] --> APP
    INF --> DOM
    INF --> DSP[Orbit.Audio / Faust assets]
    UI --> INF
~~~

The UI-to-infrastructure dependency is restricted to composition/setup, where dependencies are injected (e.g. Program.cs). Presentation components consume application contracts. Domain must not reference Blazor, infrastructure or Faust-specific runtime objects.

### Planned GPU renderer isolation (7.3.11)

The new graphical editor's preferred prototype stack is PixiJS + PixiUI + PixiJS Layout, subject to the 7.3.11 approval gate. The new graphical editor will have its own source root, `src/Orbit.GpuRenderer/`, separate from the existing `Orbit.UI` HTML/SVG implementation. It consumes shared renderer-neutral Application/Presentation contracts; existing graph and audio layers do not depend on GPU code. The existing UI composition root may select/mount it through a minimal adapter. Renderer-specific source, prototypes and resources belong inside the new root, with dedicated tests; cross-root exceptions and architectural checks are defined in the [mandatory GPU renderer isolation contract](../editor/gpu-renderer.md#mandatory-source-tree-and-dependency-isolation). This is planned, not a currently implemented project reference.

### Application startup and connection of layers

Program.cs in the Blazor UI is the composition root. It registers the application services, graph registry, event dispatcher, compiler and browser audio engine implementations through dependency injection. The canvas renders the graph model through a view state/projection. User input invokes commands on the application services, not direct modifications of DSP state by the UI.

~~~mermaid
flowchart TD
    P[Program.cs / DI] --> U[Blazor canvas]
    U --> C[Application commands]
    C --> G[Domain graph]
    G --> E[Committed events]
    E --> S[Audio graph service]
    S --> B[Browser audio bridge]
    B --> F[Faust AudioWorklet]
    F --> O[Browser output]
~~~

The existing bridge manages browser restrictions such as starting/resuming AudioContext after a user gesture. Faust is the processing engine, not the author of the application model. JavaScript remains the infrastructure boundary, not a second independently managed graph database.

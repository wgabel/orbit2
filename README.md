# Orbit 2

Orbit 2 is a small browser-based experiment for redesigning Orbit using a layered .NET architecture. It uses standalone Blazor WebAssembly for the UI and a separate, framework-independent C# application library.

The first implementation intentionally contains only one UI component: a counter. This verifies that the browser can run C# code, use dependency injection and update its interface without React, TypeScript, npm or an ASP.NET backend. A dedicated sound-processing engine (such as Rust or Faust compiled to WebAssembly) may be introduced later.

## Requirements and local execution

- .NET 10 SDK
- A modern browser

From the repository root:

```sh
dotnet run --project src/Orbit.UI --urls http://localhost:5000
```

Open http://localhost:5000. Clicking **Increment** calls the C# application service and displays its updated count.

## Architecture and layer boundaries

```mermaid
flowchart TD
    Host["Browser host: index.html"] --> Startup["Composition root: Program.cs"]
    Startup --> UI["View: App.razor"]
    UI --> Presentation["Presentation: App.razor.cs"]
    Presentation --> Contract["Application contract: ICounterService"]
    Contract -. "Implemented by" .-> Service["Application logic: CounterService"]
    Service --> State["In-memory count"]
    Startup -. "Registers implementation" .-> Service
```

| Layer | Current implementation | Responsibility |
| --- | --- | --- |
| Browser host | `wwwroot/index.html` | Loads styles and the Blazor runtime; provides the mount element for the UI. |
| Composition root | `Program.cs` | Creates the WebAssembly host, registers services and mounts the root component. This is where concrete implementations are wired together. |
| View (UI) | `App.razor`, `wwwroot/styles.css` | Defines appearance, displays values and binds user actions to presentation handlers. |
| Presentation | `App.razor.cs` | Connects the UI to the application service through its interface. It contains event handlers and view-facing properties, not application rules. |
| Application | `ICounterService.cs`, `CounterService.cs` | Defines the contract, implements the increment operation and owns the current state. |
| Data/state | `CounterService.Count` | An in-memory integer owned by the service. There is no database or persistence layer yet. |
| Processing/DSP | Not implemented | Future sound generation, modulation and effects. Real-time processing must not depend on the UI or its rendering lifecycle. |

The application library does not reference Blazor or the UI project. The UI project references the application library, but the dependency cannot go in the opposite direction.

**There are two places where the layers meet:** `Program.cs` is the composition root (wiring and dependency injection), while `App.razor.cs` is the presentation boundary (interaction between the view and the application contract).

## Application startup flow

```mermaid
sequenceDiagram
    participant B as Browser
    participant R as Blazor runtime
    participant P as Program.cs
    participant C as App component
    participant S as Counter service
    B->>R: Load blazor.webassembly.js
    R->>P: Run generated Main entry point
    P->>P: Create WebAssembly host builder
    P->>P: Register ICounterService and root component
    P->>R: Build and run host
    R->>C: Create component and inject service
    C->>S: Read Count
    S-->>C: 0
    C-->>B: Render counter into #app
```

1. The browser first loads `src/Orbit.UI/wwwroot/index.html`. Its `#app` element initially displays loading text.
2. The framework script `_framework/blazor.webassembly.js` loads the .NET WebAssembly runtime and the compiled application.
3. The first **application-owned C# code** to execute is in `src/Orbit.UI/Program.cs`. Its top-level statements are compiled into an entry-point method by the C# compiler; there is no manually declared `Main()` method.
4. `WebAssemblyHostBuilder.CreateDefault(args)` creates the host builder.
5. `builder.RootComponents.Add<App>("#app")` registers the root Blazor component and identifies its mount element.
6. `builder.Services.AddSingleton<ICounterService, CounterService>()` tells dependency injection how to resolve the interface. The service instance is shared within this running client application.
7. `await builder.Build().RunAsync()` builds and starts the host. Blazor creates the root component, resolves and injects the service, and renders the view.
8. `App.razor` reads the `Count` property from its code-behind, which in turn reads `ICounterService.Count`.

`App.razor` and `App.razor.cs` are two partial definitions of the same generated `App` component class. The Razor markup supplies the view, while the C# file supplies the presentation behavior. They are kept separate for readability, not separate runtime objects.

## Increment button: UI to processing and back

```mermaid
sequenceDiagram
    actor User
    participant V as App.razor
    participant P as App.razor.cs
    participant S as CounterService
    User->>V: Click Increment
    V->>P: IncrementCounter()
    P->>S: ICounterService.Increment()
    S->>S: Update Count
    P-->>V: Read updated Count on render
    V-->>User: Display new number
```

1. The button in `App.razor` binds its click event to `IncrementCounter()`.
2. Blazor invokes that handler in `App.razor.cs`.
3. The handler calls `ICounterService.Increment()`. Dependency injection provides the actual `CounterService` implementation.
4. `CounterService.Increment()` modifies its own `Count` property.
5. After the component event handler completes, Blazor schedules a render. The view reads `Count` again through the presentation layer and displays the new value.

This is ordinary C# method execution, not a network request. The browser holds the service state in memory. Reloading the application creates a new instance, so the counter resets.

## Repository layout and file responsibilities

```text
Orbit.slnx                          # Solution containing both projects
src/
  Orbit.Application/
    Orbit.Application.csproj        # Framework-independent class library
    Services/
      ICounterService.cs             # Contract consumed by the UI
      CounterService.cs              # Operation and state ownership
  Orbit.UI/
    Orbit.UI.csproj                  # Blazor WASM project and dependencies
    Program.cs                       # C# entry point and composition root
    App.razor                        # Root component markup and event bindings
    App.razor.cs                     # Root component presentation logic
    wwwroot/
      index.html                     # Browser entry document and mount point
      styles.css                     # Visual presentation
.github/
  workflows/
    build-and-deploy.yml             # Compile and deploy static output
.gitignore                           # Exclude generated and local build files
README.md                            # Architecture and development documentation
```

## How the architecture can evolve

The counter demonstrates the boundary between UI and application logic. As Orbit grows, additional projects can be introduced when needed, rather than coupling unrelated responsibilities to the component.

- `Orbit.Domain`: patch, oscillator, effect and node-graph models and domain rules.
- `Orbit.Application`: use cases and contracts for manipulating patches and coordinating processing.
- `Orbit.Infrastructure`: concrete persistence, file storage and browser integration adapters.
- `Orbit.Audio`: dedicated audio integration and DSP boundary, potentially connecting to a Rust or Faust WebAssembly processor in an AudioWorklet.
- `Orbit.UI`: Razor components and presentation logic; no actual sound generation or low-level storage implementation.

These are proposed future responsibilities, not existing projects. The concrete implementations will be connected through dependency injection in the composition root. An audio processing engine would run independently of Blazor UI updates, with a browser audio adapter passing control parameters to it.

## Build and deployment

```sh
dotnet build Orbit.slnx
dotnet publish src/Orbit.UI -c Release -o publish
```

The publish command produces the browser application in `publish/wwwroot`. GitHub Actions builds pull requests and publishes pushes to `main` to GitHub Pages. The workflow changes the HTML base path to `/orbit2/` for deployment and uploads the published static files before the deployment job.

To enable Pages, open **Settings → Pages → Build and deployment → Source**, select **GitHub Actions**, then run the **Build and deploy** workflow manually or push to main.

Expected project site: https://wgabel.github.io/orbit2/

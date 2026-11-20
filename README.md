# Orbit 2

A barebones experiment for redesigning Orbit with Blazor WebAssembly, and eventually a separate DSP engine (Rust or Faust).

## Requirements

- .NET 10 SDK
- A modern browser

No Node.js, npm, React, TypeScript or backend server is required.

## Run locally

```sh
dotnet run --project src/Orbit.UI --urls http://localhost:5000
```

Open http://localhost:5000. The page displays a counter. Clicking the button calls a C# application service through an interface registered with dependency injection.

## Architecture

- `Orbit.Application`: framework-independent C# class library, containing the counter state and service.
- `Orbit.UI`: standalone Blazor WebAssembly application, containing the component markup, presentation code and CSS. It depends on Application, never the other way around.

The application has one interactive component and no audio engine yet.

## Build and deploy

```sh
dotnet build Orbit.slnx
dotnet publish src/Orbit.UI -c Release -o publish
```

GitHub Actions builds pull requests and publishes pushes to `main` to GitHub Pages. To enable Pages, open Settings → Pages → Build and deployment → Source and select **GitHub Actions**. Then run the **Build and deploy** workflow manually or push to main.

Expected project site: https://wgabel.github.io/orbit2/

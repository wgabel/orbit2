# Orbit 2

A browser-based modular synthesizer: C# graph and application logic, Blazor WebAssembly editor, Faust DSP and Web Audio/AudioWorklet runtime. No React or TypeScript.

**[Open Orbit 2](https://wgabel.github.io/orbit2/)**

## Start here

- **[Documentation map](docs/README.md)** — the single navigation index for guides, current contracts, design documents and history.
- **[Usage guide](docs/usage/README.md)** — learn to operate the patch editor and connect instruments.
- **[Create music](docs/examples/README.md)** — composition, sound design, theory and tested patches.
- **[Architecture and task routing](ORBIT2-ARCHITECTURE.md)** — system boundaries and focused technical documentation.
- **[Implementation roadmap](ROADMAP.md)** — canonical status summary; milestone registers provide details, not separate roadmaps.
- **[Contributor and agent rules](AGENTS.md)** — coding, gestures, small work slices and verification.

For current behavior use the implementation and tests, not historical notes or an unmerged proposal.

## Requirements

- .NET 10 SDK
- Node.js 24 and npm (build tooling and infrastructure tests)
- A modern browser supporting Web Audio and AudioWorklet

## Run locally

From the repository root:

```sh
npm exec --yes --package=@grame/faustwasm@0.19.0 -- faust2wasm-ts src/Orbit.Audio/Dsp/Oscillators/ModulatedSine.dsp src/Orbit.Infrastructure/wwwroot/audio/generated/modulated-sine
npm install --no-audit --no-fund --ignore-scripts --no-save @grame/faustwasm@0.19.0
node scripts/prepare-faust-compiler.mjs
node src/Orbit.UI/DesignSystem/generate-tokens.mjs
dotnet run --project src/Orbit.UI --urls http://localhost:5000
```

Open http://localhost:5000. Audio starts on the user's **Play patch** action; **Stop** releases playback. Generated Faust artifacts are local build outputs and are not tracked.

## Build and tests

Run the preparation commands above before the complete audio suite.

```sh
node scripts/check-doc-links.mjs
dotnet test tests/Orbit.Domain.Tests/Orbit.Domain.Tests.csproj -c Release
dotnet test tests/Orbit.Presentation.Tests/Orbit.Presentation.Tests.csproj -c Release
node src/Orbit.UI/DesignSystem/generate-tokens.mjs --check
node --test tests/ui/*.test.mjs
node --test tests/audio/*.test.mjs
dotnet build Orbit.slnx
dotnet publish src/Orbit.UI -c Release -o publish
```

[GitHub Actions](.github/workflows/build-and-deploy.yml) also validates compiled DSP and published browser playback. Main builds deploy the static editor to GitHub Pages only if their commit is still the current main HEAD.

### Browser release identity and cache invalidation

The running editor identifies its build by a CI run number and commit. Published JS/audio assets are commit-versioned, and startup checks the deployment manifest to prevent stale entry pages from mixing releases. If a feature is missing after a merge, first compare the visible build number with the latest completed Pages deployment and its commit; **do not clear browser data**, which may contain locally saved patches. See the [original implementation record](docs/archive/legacy-readme.md#browser-release-identity-and-cache-invalidation) and [PR #72](https://github.com/wgabel/orbit2/pull/72).

## Repository

`src/` contains the domain, application, presentation, audio, infrastructure and Blazor UI projects. `tests/` contains .NET, Node and browser regression tests. `docs/` contains the authored documentation, organized by reader and subsystem. `scripts/` contains build, publishing and benchmark helpers.

Use [project organization](docs/core/project-structure.md) for dependency direction and [architecture](ORBIT2-ARCHITECTURE.md) for responsibilities.

## Historical implementation detail

The former long README combined operational instructions with phase-by-phase feature narratives. Its full snapshot is preserved in [historical implementation notes](docs/archive/legacy-readme.md) for traceability; that archive is **not** the source of current roadmap status, usage behavior or technical contracts.

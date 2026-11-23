# Install and run Orbit 2 locally

[Project](../../README.md) · [Development and testing](development.md) · [Deployment](deployment.md)

## Requirements

- .NET 10 SDK
- Node.js 24 and npm (build tooling and infrastructure tests)
- Browser supporting Web Audio and AudioWorklet

## Prepare Faust and launch

From the repository root:

```sh
npm exec --yes --package=@grame/faustwasm@0.19.0 -- faust2wasm-ts src/Orbit.Audio/Dsp/Oscillators/ModulatedSine.dsp src/Orbit.Infrastructure/wwwroot/audio/generated/modulated-sine
npm install --no-audit --no-fund --ignore-scripts --no-save @grame/faustwasm@0.19.0
node scripts/prepare-faust-compiler.mjs
node src/Orbit.UI/DesignSystem/generate-tokens.mjs
dotnet run --project src/Orbit.UI --urls http://localhost:5000
```

Open **http://localhost:5000**. Use **Play patch** to compile and start the patch and **Stop** to release playback. Browser audio activation needs the user's gesture. The generated Faust assets are ignored build output.

Run the [development checks](development.md) after changing the graph, nodes or deployment pipeline. For editing and using patches see the [usage guide](../usage/README.md).

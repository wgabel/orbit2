# Orbit 2

A browser-based modular synthesizer with a **Blazor WebAssembly/C#** editor, independent signal graph, **Faust DSP** and Web Audio/AudioWorklet playback.

**[Launch Orbit 2](https://wgabel.github.io/orbit2/)**

<a id="start-here"></a>

## Navigate

- [Install and run locally](docs/getting-started/installation.md) · [Development and tests](docs/getting-started/development.md)
- [User guide](docs/usage/README.md) · [Music creation](docs/examples/README.md)
- [Documentation directory](docs/README.md) · [Architecture](ORBIT2-ARCHITECTURE.md)
- [One canonical roadmap](ROADMAP.md) · [Agent/contributor rules](AGENTS.md)

<a id="run-locally"></a>

## Requirements

.NET 10, Node.js 24 with npm, and a browser supporting Web Audio/AudioWorklet. The local Faust tooling must be prepared before playback and the complete audio test suite; follow [setup instructions](docs/getting-started/installation.md).

## Build and tests

See [development commands](docs/getting-started/development.md) and [automated verification](.github/workflows/build-and-deploy.yml).

### Browser release identity and cache invalidation

See [deployment and cache diagnosis](docs/getting-started/deployment.md#browser-release-identity-and-cache-invalidation). **Never clear browser site data as a cache workaround without first protecting saved patches.**

<a id="historical-implementation-detail"></a>

## Repository

Source in `src/`, checks in `tests/`, build helpers in `scripts/`, detailed reference in `docs/`. For implementation history, use the [dated legacy notes index](docs/archive/legacy-readme.md); historical prose is not an authority for current behavior.

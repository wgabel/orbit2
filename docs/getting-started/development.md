# Develop and test Orbit 2

[Installation](installation.md) · [Architecture](../architecture/overview.md) · [Coding standards](../quality/coding-standards.md)

Run the [Faust and compiler preparation](installation.md#prepare-faust-and-launch) before building or running the complete audio suite. The generated directory is ignored by Git.

## Core verification commands

From the repository root:

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

The [build-and-deploy workflow](../../.github/workflows/build-and-deploy.yml) additionally tests the actual compiled DSP at 44.1/48 kHz, browser audio, built-in assets, published editor and diagnostics. Select tests appropriate to your change rather than treating a build alone as evidence of correct behavior.

## Code boundaries and agent protocol

Follow [architecture routing](../architecture/overview.md), [project dependencies](../core/project-structure.md), [agent work slices](../agents/work-slices.md), and [interaction registry](../editor/user-interactions.md) for gesture changes. Put verified implementation status in the owning [milestone register](../roadmap/overview.md), not here.

For previous phase-by-phase technical implementation history see the [historical notes index](../archive/legacy-readme.md).

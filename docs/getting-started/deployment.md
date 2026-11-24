# Deployment and browser release diagnosis

[Project README](../README.md) · [Development checks](development.md) · [CI workflow](../../.github/workflows/build-and-deploy.yml)

The main-branch GitHub Actions workflow builds and tests the graph, Faust DSP, UI tokens, browser playback and published static assets. It deploys `publish/wwwroot` to GitHub Pages at [Orbit 2](https://wgabel.github.io/orbit2/). The Pages job is serialized and rechecks main HEAD before deployment, so an older successful build cannot overwrite a newer one; see [PR #67](https://github.com/wgabel/orbit2/pull/67).

## Browser release identity and cache invalidation

Every CI publish includes the Git commit and workflow run number in its editor build identity and published manifest. Versioned CSS, editor JS, audio and persistence module paths prevent stale runtime asset trees from being mixed across deployments. Startup checks an uncached manifest and requests the correct page for a mismatched deployed commit. Local builds identify themselves as `dev`; already-open tabs can remain on an older version until reloaded. See [PR #72](https://github.com/wgabel/orbit2/pull/72) and the [original implementation record](../archive/legacy-readme.md#browser-release-identity-and-cache-invalidation).

If a merged change appears absent, first check the latest **completed** Pages deployment, its commit and the build identity shown by the running editor. Verify the live fingerprinted JS/CSS before diagnosing browser cache. Do **not** clear site data indiscriminately: IndexedDB may contain saved patches.

For precise historical implementation detail, including error conditions and verification coverage, see [the archived deployment section](../archive/legacy-readme.md#browser-release-identity-and-cache-invalidation). For runtime limitations use the [current source and tests](../quality/testing.md).

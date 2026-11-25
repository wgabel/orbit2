# Deployment and browser release diagnosis

[Project README](../README.md) · [Development checks](development.md) · [CI workflow](../../.github/workflows/build-and-deploy.yml)

The main-branch GitHub Actions workflow builds and tests the graph, Faust DSP, UI tokens, browser playback and published static assets. It deploys `publish/wwwroot` to GitHub Pages at [Orbit 2](https://wgabel.github.io/orbit2/). The Pages job is serialized and checks the current main runtime inputs before deployment; the original ordering guard was introduced in [PR #67](https://github.com/wgabel/orbit2/pull/67).

## Documentation-only CI

Implementation: [PR #132](https://github.com/wgabel/orbit2/pull/132) (in review).

The build workflow always reports the existing `build` check, runs the change-classifier tests and validates documentation links. The separate agent-compliance workflow is unchanged. The shared [change-scope policy](../../scripts/ci-change-scope.mjs) excludes only Markdown under `docs/`, root AGENTS.md, the Copilot instructions, PR template and non-executable agent routing JSON. Reference-patch JSON, unknown paths, tests, workflow files and runtime code require full CI; PR titles do not determine skipping. Renames consider both paths. PRs compare merge base to head; main pushes compare before and after, including all commits in the push. Mixed PRs still build on documentation closeout commits. Manual runs, uncertain or unavailable history and forced pushes trigger full CI.

Documentation-only runs skip .NET setup, Faust, app tests, publishing, benchmarks, artifacts and deployment. A built main revision may deploy after a newer documentation-only merge: under the existing Pages lock the deploy job fetches current main and requires the built commit to be its ancestor with no changed runtime inputs. A newer runtime change or divergent history rejects the artifact; unexpected Git errors fail the guard. Release identity remains the actual built revision, which may precede documentation commits. Manual dispatch on main can force a rebuild/deploy.

Run the classifier and real Git-history fixtures with `node --test tests/tooling/CiChangeScope.test.mjs`. Verify one documentation-only PR/main push, a mixed/runtime PR and main build, and the serialized deployment race in Actions before claiming end-to-end verification.

## Browser release identity and cache invalidation

Every CI publish includes the Git commit and workflow run number in its editor build identity and published manifest. Versioned CSS, editor JS, audio and persistence module paths prevent stale runtime asset trees from being mixed across deployments. Startup checks an uncached manifest and requests the correct page for a mismatched deployed commit. Local builds identify themselves as `dev`; already-open tabs can remain on an older version until reloaded. See [PR #72](https://github.com/wgabel/orbit2/pull/72) and the [original implementation record](../archive/legacy-readme.md#browser-release-identity-and-cache-invalidation).

If a merged change appears absent, first check the latest **completed** Pages deployment, its commit and the build identity shown by the running editor. Verify the live fingerprinted JS/CSS before diagnosing browser cache. Do **not** clear site data indiscriminately: IndexedDB may contain saved patches.

For precise historical implementation detail, including error conditions and verification coverage, see [the archived deployment section](../archive/legacy-readme.md#browser-release-identity-and-cache-invalidation). For runtime limitations use the [current source and tests](../quality/testing.md).

# Faust dependency and licensing audit — Phase 7.2.7a

[Faust-first design](realistic-instrument-emulation.md) · [Roadmap](../roadmap/advanced.md#phase-727--faust-first-physical-models-planned)

**Status (2026-11-23):** exact package and library inventory verified; two minimal candidate functions compile with the pinned browser bundle. Approved as **local research candidates**, not shipped instruments or a blanket license clearance. Release/commercial distribution compliance remains an explicit gate. This is an engineering audit, not legal advice.

## 1. Artifact identity

The repository's [prepare-faust-compiler.mjs](../../scripts/prepare-faust-compiler.mjs) requires `@grame/faustwasm@0.19.0` and copies its ESM browser bundle, source map and COPYING notice. The [README](../../README.md) describes the pinned runtime and compiler-worker path. Audit performed against the actual registry tarball, not upstream master or a globally installed Faust.

| Identity | Verified value |
|---|---|
| npm package | [@grame/faustwasm@0.19.0](https://www.npmjs.com/package/@grame/faustwasm/v/0.19.0) |
| npm gitHead (package provenance, not a proven library commit) | `d148922ebc518c3e33a1cd8aaa06f9c33022df5e` |
| npm license metadata | LGPL-3.0 |
| Compiler reported by both ESM implementations | 2.90.0 |
| Standard libraries from embedded `version.lib` | 2.74.2 |
| Embedded `physmodels.lib` version | 1.2.1 |
| npm tarball SHA-256 | `93ccfe0e9cc174a4ae47786ee4d0e87814f20f935ca743e62213d7dbd5067429` |
| `dist/esm-bundle/index.js` SHA-256 | `c602b02f0d69f09c67d7b2e8f135cc3067d10da6b4c4c80fb2c0278132ae5912` |
| `dist/esm-bundle/index.js.map` SHA-256 | `996afbc4fb61536607bdebcad21ecc4cdc27890c6bbf1f8a04bebfa159259a7a` |
| `libfaust-wasm/libfaust-wasm.wasm` SHA-256 | `db7bde14b2553826ebcc30d8bd967aee98b37f3f0fce98182e5f574eb14bc217` |
| `libfaust-wasm/libfaust-wasm.data` SHA-256 | `12e89b91e9b9fce899b1cdf6f62fdea18d95ce1ae63a69a61d76c3d365e52050` |

The package reports compiler 2.90.0 both from its separate WASM/data and the self-contained bundle used by Orbit. The embedded filesystem supplied the same `physmodels.lib` hash in both. The exact upstream Faust compiler/library git revisions have not been independently established; these fingerprints identify the actual audited bytes. Do not substitute current upstream source on the strength of a matching function name.

## 2. Function-level results

Minimal compile probes (single precision, mono, one signal input and one output):

```faust
import("stdfaust.lib");
process = pm.ks(pm.f2l(440), 0.5);
```

```faust
import("stdfaust.lib");
process = pm.fluteModel(pm.f2l(440), 0.5);
```

| Candidate | Exact signature | Result and next step |
|---|---|---|
| Karplus–Strong pluck | `pm.ks(length,damping,excitation)` | Compiles; audio input is excitation. First experiment: short, bounded excitation burst, variable pitch in meters via `pm.f2l`, damping control and decay. |
| Waveguide flute | `pm.fluteModel(tubeLength,mouthPosition,pressure)` | Compiles; audio input is pressure. First contrasting experiment: shaped continuous pressure, controlled attack/release and pitch calibration; do not confuse a fixed mouth position with measured embouchure. |

Both use hand-tuned length corrections internally. A successful compilation does **not** establish pitch accuracy, sounding/idle behavior, stability at arbitrary controls or acoustic realism. No DSP has yet been rendered, auditioned, integrated into Orbit, or shipped by this audit. Use the parameterized cores, not the library's demonstration MIDI/UI wrappers.

### Compiled dependency inventory

Compiler factory JSON `library_list` and `meta`, plus embedded source inspection. G = LGPL-2.1-or-later with Faust generated-code exception as stated in the file; S = STK-4.3 terms for designated functions only. Individual function overrides take precedence over a file default. Hashes are of the exact embedded UTF-8 library bytes in `/usr/share/faust/`.

| Library | Version | License evidence for probe | Pluck | Flute | SHA-256 |
|---|---|---|---|---|---|
| stdfaust.lib | — | G | yes | yes | `dc0b5d2fa3b3b9726658b220c6e825e333c1999a07fe59f55d778037c9f8daa0` |
| physmodels.lib | 1.2.1 | G; explicit header | yes | yes | `4e8aed3746d7dfe98dc870ee0df79f5ab5d63b2c639b0d2713fa58a3284214ea` |
| maths.lib | 2.9.0 | G; also declared in factory metadata | yes | yes | `76de0cdb88f5e47563db87dc1905b18030be7856e5b4c0751c60d7fc6aa57ec6` |
| platform.lib | 1.3.0 | G | yes | yes | `4d09a4239f752bff5d44ec7ddbeea80c1cc76ec915d872ae3b0c467bd59288f5` |
| delays.lib | 1.2.0 | G; preserve J. O. Smith III authorship for `fdelay4` and `fdelayltv` | yes | yes | `7993d1e6d41b113bc3a9ae71de0ee73522c33f730921e4c66779dab543e71e1d` |
| routes.lib | 1.4.0 | G; unrelated sorting routines have separate MIT declarations | yes | yes | `ec5f8102bc1636cd87a84190b9da3d0994d7dfb8137387cd0bbe8550e67c4db2` |
| signals.lib | 1.7.0 | G for GRAME section; mixed-license file; flute foot uses `si.smooth` | no | yes | `0c56b23b43cebd808f3f607c4870ecff5430390c0f6a50628edd2f401345d86a` |
| filters.lib | 1.9.0 | S: `dcblocker`, `pole`, `zero` explicitly declared in factory metadata | no | yes | `4b4fcba46cd950738338b4361fa6cb2fbd43f7bfc45ea41581b856e66e8a65f6` |

The two factories report no external sample assets or coefficient-file loads. This is the dependency inventory of these minimal probes, not of future wrappers including envelopes, noise, modulation, filters or other functions. Repeat the audit against the final emitted program; factory metadata is supporting evidence, not a substitute for source/license review. Other instruments, models and assets remain unaudited.

## 3. Distribution decision and outstanding gates

- **Generated instrument DSP:** the embedded physical-model library includes the GRAME LGPL-2.1-or-later exception permitting independently licensed compiled Faust output. The missing header in upstream history was fixed in [issue #250, commit 85368ff](https://github.com/grame-cncm/faustlibraries/issues/250); the exact Orbit package already contains it, so no dependency upgrade is needed solely for this reason. This grant does not automatically extend to source redistribution, other licenses, or the compiler itself.
- **STK functions:** the flute factory reports `LicenseRef-STK-4.3` for the three J. O. Smith III filters (copyright 2003–2019). [STK 4.3.0 license text](https://github.com/grame-cncm/faustlibraries/blob/master/licenses/stk-4.3.0.md) permits reuse/commercial distribution with retained copyright and permission notices. Package all applicable notices with distributed output; preserve authorship. The embedded library directory did not expose a license directory/file for STK. Obtain and preserve the applicable full text with the eventual release inventory.
- **Compiler/runtime distribution:** the npm package declares LGPL-3.0, but its COPYING.txt identifies Faust wasm (GRAME, 2021–2024) under LGPL-2.1-or-later and contains LGPL 2.1. Later versions are permitted by that notice, but resolve the precise release licensing and applicable terms before distribution. The exception for generated DSP is not an exemption for the browser compiler package. [LGPL 2.1 sections 4 and 6](https://www.gnu.org/licenses/old-licenses/lgpl-2.1.en.html) address corresponding source, notices and combination/replacement obligations; review the applicable version for the actual bundle.
- **Current Orbit release path:** the inspected prepare script copies bundle, source map and COPYING only. It does not itself publish a verified complete corresponding-source archive, all embedded library notices or a documented replacement/rebuild route. Root paths LICENSE, LICENSE.md, NOTICE and THIRD-PARTY-NOTICES.md were not found on audited main. This is an identified evidence gap, not a finding that the whole project violates a license; verify other release channels and intended project/distribution terms. Resolve in a separately reviewed release/compliance change: establish source revisions or exact corresponding source/build inputs, publish or provide compliant access to them, ship applicable full license texts and notices, document replacement/rebuild as applicable, and review against Orbit's intended license. Preserve npm integrity and artifact checks in the build/release process.
- **Scope:** proceed with bounded local offline experiments for these two cores; no new production integration/distributed instrument until the final emitted DSP closure and distribution obligations are recorded and reviewed. A source-copy approach has different obligations from importing a library function; prefer the latter. Do not change the pinned version or redistribute upstream assets merely on the basis of this audit.

## 4. Repeatable checks

From a disposable working directory, with Node.js and npm (tested Node 24):

```sh
npm view @grame/faustwasm@0.19.0 version dist.integrity gitHead license --json
npm pack @grame/faustwasm@0.19.0 --ignore-scripts
tar -xf grame-faustwasm-0.19.0.tgz
sha256sum grame-faustwasm-0.19.0.tgz package/dist/esm-bundle/index.js \
  package/dist/esm-bundle/index.js.map package/libfaust-wasm/libfaust-wasm.{wasm,data}
```

Run the following as ESM in that directory (e.g. save as audit.mjs). Node-only globals allow running the unmodified browser bundle in Node; do not add them to Orbit's browser code.

```js
import { createRequire } from "node:module";
import { createHash } from "node:crypto";
import { resolve } from "node:path";

globalThis.require = createRequire(import.meta.url);
globalThis.__dirname = resolve("package/dist/esm-bundle");
const { instantiateFaustModule, LibFaust, FaustCompiler } =
  await import("./package/dist/esm-bundle/index.js");
const lib = new LibFaust(await instantiateFaustModule());
console.log("compiler", lib.version());
const fs = lib.fs();
const version = fs.readFile("/usr/share/faust/version.lib", { encoding: "utf8" });
console.log(version);
const compiler = new FaustCompiler(lib);
const paths = new Set();
for (const [name, expr] of [
  ["ks", "pm.ks(pm.f2l(440),0.5)"],
  ["flute", "pm.fluteModel(pm.f2l(440),0.5)"]
]) {
  const factory = await compiler.createMonoDSPFactory(
    name, 'import("stdfaust.lib"); process = ' + expr + ";", "-single");
  const meta = JSON.parse(factory.json);
  console.log(name, meta.inputs, meta.outputs, meta.library_list, meta.meta);
  for (const path of meta.library_list) paths.add(path);
}
for (const path of [...paths].sort()) {
  const bytes = fs.readFile(path);
  console.log(path, createHash("sha256").update(bytes).digest("hex"));
  // Inspect the complete source and per-function declarations locally.
  console.log(new TextDecoder().decode(bytes).split("\n")
    .filter(s => /copyright|licen[cs]e|declare version/i.test(s)).join("\n"));
}
```

See the official [Faust library contribution licensing guidance](https://faustlibraries.grame.fr/contributing/) for per-symbol license metadata.

## 5. Follow-up: Phase 7.2.7b

The [native offline probe results](faust-physical-model-probes.md) now document the two models' rendering, calibration and pressure/damping behavior. The local experiment does not resolve the production distribution gate above.

Render the pluck and flute as local, unshipped mono experiments with the same pinned toolchain at 44.1/48 kHz. Measure pitch/decay/stability, deliberate idle behavior and headroom; exercise time-varying controls and compare level-matched against existing Orbit approximations using the [audition workflow](../examples/instrument-design-and-audition.md). The pluck is the simpler first fixture, and the flute tests sustained physical excitation. Keep the release compliance follow-up open, independent of sound-quality experiments.

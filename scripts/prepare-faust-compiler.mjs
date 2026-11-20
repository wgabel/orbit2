// Copy the pinned upstream browser compiler bundle to the static asset tree.
// The package is installed at build time; no CDN or runtime npm dependency.
import { copyFile, mkdir, readFile } from "node:fs/promises";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

const packageDirectory = resolve(process.argv[2] || "node_modules/@grame/faustwasm");
const metadata = JSON.parse(await readFile(resolve(packageDirectory, "package.json"), "utf8"));
if (metadata.name !== "@grame/faustwasm" || metadata.version !== "0.19.0")
    throw new Error("Expected @grame/faustwasm@0.19.0");
const destination = fileURLToPath(new URL("../src/Orbit.Infrastructure/wwwroot/audio/generated/compiler/", import.meta.url));
await mkdir(destination, { recursive: true });
for (const file of ["index.js", "index.js.map"])
    await copyFile(resolve(packageDirectory, "dist/esm-bundle", file), resolve(destination, file));
await copyFile(resolve(packageDirectory, "COPYING.txt"), resolve(destination, "COPYING.txt"));

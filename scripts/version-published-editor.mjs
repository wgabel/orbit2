// Publish the complete editor asset trees under immutable commit-specific URLs.
// The HTML entry remains at the stable Pages URL but can revalidate its identity.
import { cp, readFile, writeFile } from "node:fs/promises";
import { resolve } from "node:path";
import { pathToFileURL } from "node:url";

export async function versionPublishedEditor(root, commit, build) {
    if (!/^[0-9a-f]{40}$/.test(commit ?? "") || !/^[1-9]\d*$/.test(build ?? ""))
        throw new Error("Expected a full commit SHA and positive CI build number");

    const entry = resolve(root, "index.html");
    let html = await readFile(entry, "utf8");
    const meta = '<meta name="orbit-build" content="local" />';
    const numberMeta = '<meta name="orbit-build-number" content="local" />';
    if (!html.includes(meta) || !html.includes(numberMeta)) throw new Error("Missing or already versioned build identity");
    html = html.replace(meta, `<meta name="orbit-build" content="${commit}" />`);
    html = html.replace(numberMeta, `<meta name="orbit-build-number" content="${build}" />`);
    for (const file of ["tokens.css", "base.css", "components.css"]) {
        const href = `href="css/${file}"`;
        if (!html.includes(href)) throw new Error("Missing stylesheet: " + file);
        html = html.replace(href, `href="css-${commit}/${file}"`);
    }
    for (const directory of ["css", "js"]) {
        await cp(resolve(root, directory), resolve(root, `${directory}-${commit}`),
            { recursive: true, force: false, errorOnExist: true });
    }
    await writeFile(entry, html, "utf8");
    await writeFile(resolve(root, "build-manifest.json"), JSON.stringify({ commit, build }) + "\n", "utf8");
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
    await versionPublishedEditor(...process.argv.slice(2));
}

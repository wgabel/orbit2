// Contract tests for the generated design system and the styles that consume it.
import test from "node:test";
import assert from "node:assert/strict";
import { readFile, readdir } from "node:fs/promises";
import { generateTokensCss, readTokenFiles } from "../../src/Orbit.UI/DesignSystem/generate-tokens.mjs";

const [shared, dark, light] = await readTokenFiles();
const cssDirectory = new URL("../../src/Orbit.UI/wwwroot/css/", import.meta.url);
const css = generateTokensCss(shared, dark, light);

test("checked-in stylesheet matches the JSON source deterministically", async () => {
    const actual = await readFile(new URL("tokens.css", cssDirectory), "utf8");
    assert.equal(actual.replaceAll("\r\n", "\n"), css);
    assert.equal(generateTokensCss(shared, dark, light), css);
});

test("dark is the default and light overrides the same semantic roles", () => {
    const [defaultScope, lightScope] = css.split(':root[data-theme="light"]');
    assert.match(defaultScope, /--semantic-color-canvas: var\(--primitive-color-ink-950\)/);
    assert.match(lightScope, /--semantic-color-canvas: var\(--primitive-color-light-canvas\)/);
    assert.match(defaultScope, /--component-slider-modulation-color: var\(--semantic-color-modulation\)/);
    assert.match(defaultScope, /--component-node-min-width: 240px/);
});

test("rejects missing aliases and cycles rather than producing broken CSS", () => {
    const missing = structuredClone(shared);
    missing.component.slider["thumb-color"] = "{semantic.color.missing}";
    assert.throws(() => generateTokensCss(missing, dark, light), /Unknown token reference/);
    const cyclic = structuredClone(shared);
    cyclic.component.slider["thumb-color"] = "{component.slider.track-color}";
    cyclic.component.slider["track-color"] = "{component.slider.thumb-color}";
    assert.throws(() => generateTokensCss(cyclic, dark, light), /Circular token reference/);
});

test("rejects theme key mismatches and non-color overrides", () => {
    const missing = structuredClone(light);
    delete missing.semantic.color.generator;
    assert.throws(() => generateTokensCss(shared, dark, missing), /identical semantic color keys/);
    const invalid = structuredClone(light);
    invalid.semantic.radius = { input: "20px" };
    assert.throws(() => generateTokensCss(shared, dark, invalid), /only define semantic colors/);
});

test("rejects names that collide after flattening and unsafe declarations", () => {
    const collision = structuredClone(shared);
    collision.primitive["color-ink"] = { "950": "#000000" };
    assert.throws(() => generateTokensCss(collision, dark, light), /Duplicate CSS variable/);
    const invalid = structuredClone(shared);
    invalid.primitive.color.white = "white; background: red";
    assert.throws(() => generateTokensCss(invalid, dark, light), /Invalid CSS token value/);
});

async function stylesheetFiles(directory) {
    const result = [];
    for (const entry of await readdir(directory, { withFileTypes: true })) {
        if (entry.isDirectory()) result.push(...await stylesheetFiles(new URL(entry.name + "/", directory)));
        else if (entry.name.endsWith(".css") && entry.name !== "tokens.css") result.push(new URL(entry.name, directory));
    }
    return result;
}

test("all authored styles use defined tokens and avoid hardcoded visual constants", async () => {
    const names = new Set([...css.matchAll(/^\s*(--[\w-]+):/gm)].map(match => match[1]));
    const stylesheets = await stylesheetFiles(cssDirectory);
    assert.ok(stylesheets.length >= 7);
    for (const url of stylesheets) {
        const source = (await readFile(url, "utf8")).replace(/\/\*[\s\S]*?\*\//g, "");
        for (const match of source.matchAll(/var\((--[\w-]+)\)/g))
            assert.ok(names.has(match[1]), url.pathname + ": unknown " + match[1]);
        assert.doesNotMatch(source, /#[a-f0-9]{3,8}\b|\b(?:rgba?|hsla?)\(/i, url.pathname);
        assert.doesNotMatch(source, /\b\d*\.?\d+(?:px|rem|em|ms)\b/i, url.pathname);
    }
});

test("the document loads the generated token stylesheet before component styles", async () => {
    const document = await readFile(new URL("../index.html", cssDirectory), "utf8");
    assert.ok(document.includes('href="css/tokens.css"'));
    assert.ok(document.indexOf('href="css/tokens.css"') < document.indexOf('href="css/base.css"'));
    assert.ok(document.indexOf('href="css/base.css"') < document.indexOf('href="css/components.css"'));
    const imports = await readFile(new URL("components.css", cssDirectory), "utf8");
    for (const match of imports.matchAll(/@import url\("([^"]+)"\)/g))
        assert.ok((await readFile(new URL(match[1], cssDirectory), "utf8")).length);
});

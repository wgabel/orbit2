import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const source = await readFile(new URL("../../src/Orbit.UI/wwwroot/js/nodeLibraryStorage.js", import.meta.url), "utf8");
const storage = await import("data:text/javascript," + encodeURIComponent(source));

test("node library preferences round trip, ignore invalid storage, and tolerate disabled storage", () => {
    let value;
    globalThis.localStorage = { getItem: () => value, setItem: (_, next) => { value = next; } };
    assert.equal(storage.read(), null);
    storage.write({ favorites: ["generator.oscillator"], recent: ["processor.delay"] });
    assert.deepEqual(storage.read(), { favorites: ["generator.oscillator"], recent: ["processor.delay"] });
    value = "{broken";
    assert.equal(storage.read(), null);
    value = JSON.stringify({ favorites: [null, "test", 4], recent: Array(10).fill("node") });
    assert.deepEqual(storage.read(), { favorites: ["test"], recent: Array(6).fill("node") });
    globalThis.localStorage = { getItem: () => { throw Error("denied"); }, setItem: () => { throw Error("denied"); } };
    assert.equal(storage.read(), null);
    assert.doesNotThrow(() => storage.write({ favorites: [], recent: [] }));
});

const databaseName = "orbit2-patches";
const storeName = "workspace";
const key = "current";

function openDatabase() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(databaseName, 1);
        request.onupgradeneeded = () => request.result.createObjectStore(storeName);
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
        request.onblocked = () => reject(new Error("Patch storage upgrade is blocked."));
    });
}

async function transact(mode, action) {
    const database = await openDatabase();
    try {
        return await new Promise((resolve, reject) => {
            const transaction = database.transaction(storeName, mode);
            const request = action(transaction.objectStore(storeName));
            transaction.oncomplete = () => resolve(request.result ?? null);
            transaction.onabort = () => reject(transaction.error ?? new Error("Patch storage transaction aborted."));
            transaction.onerror = () => reject(transaction.error ?? request.error);
        });
    } finally {
        database.close();
    }
}

export function load() { return transact("readonly", store => store.get(key)); }
export function save(json) { return transact("readwrite", store => store.put(json, key)); }

// Named entries use a disjoint key namespace; existing recovery records need no migration.
const namedPrefix = "named:";
export async function listNames() {
    const keys = await transact("readonly", store => store.getAllKeys());
    return keys.filter(key => typeof key === "string" && key.startsWith(namedPrefix)).map(key => key.slice(namedPrefix.length));
}
export function readNamed(name) { return transact("readonly", store => store.get(namedPrefix + name)); }
export function writeNamed(name, json, overwrite) {
    return transact("readwrite", store => overwrite ? store.put(json, namedPrefix + name) : store.add(json, namedPrefix + name));
}
export function deleteNamed(name) { return transact("readwrite", store => store.delete(namedPrefix + name)); }

// The complete custom library has its own key, independent of recovery and named patches.
// Comparing inside the same readwrite transaction prevents lost edits from other tabs.
const nodePresetKey = "node-presets:custom";
export function loadNodePresets() { return transact("readonly", store => store.get(nodePresetKey)); }
export function replaceNodePresets(expectedJson, json) {
    return transact("readwrite", store => {
        let matched = false;
        const request = store.get(nodePresetKey);
        request.onsuccess = () => {
            matched = (request.result ?? null) === expectedJson;
            if (matched) store.put(json, nodePresetKey);
        };
        return { get result() { return matched; } };
    });
}

export function download(name, json) {
    const url = URL.createObjectURL(new Blob([json], { type: "application/json" }));
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = name;
    anchor.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}

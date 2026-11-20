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

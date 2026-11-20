// Loads and caches compiled Faust assets, sharing in-flight requests.
// Failed loads are evicted so a later playback can retry.
export class FaustModuleLoader {
    #fetchResource;
    #compileModule;
    #factories = new Map();

    constructor(fetchResource, compileModule) {
        this.#fetchResource = fetchResource;
        this.#compileModule = compileModule;
    }

    load(baseUrl) {
        const key = baseUrl.href;

        if (!this.#factories.has(key)) {
            const pending = this.#loadFactory(baseUrl).catch(error => {
                this.#factories.delete(key);
                throw error;
            });
            this.#factories.set(key, pending);
        }

        return this.#factories.get(key);
    }

    async #loadFactory(baseUrl) {
        const [binaryResponse, metadataResponse] = await Promise.all([
            this.#fetchResource(new URL("dsp-module.wasm", baseUrl)),
            this.#fetchResource(new URL("dsp-meta.json", baseUrl))
        ]);

        if (!binaryResponse.ok || !metadataResponse.ok) {
            throw new Error("Could not load Faust assets for " + baseUrl.href);
        }

        const [module, json] = await Promise.all([
            binaryResponse.arrayBuffer().then(this.#compileModule),
            metadataResponse.text()
        ]);

        // Each node receives its own soundfiles object; the immutable compiled
        // module and metadata can be reused across independent voices.
        return { module, json };
    }
}

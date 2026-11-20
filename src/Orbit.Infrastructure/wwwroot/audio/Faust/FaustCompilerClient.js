// Lazy worker client: the costly compiler runs outside the UI and audio threads.
// The worker sends immutable compiled WebAssembly modules, not samples.
export class FaustCompilerClient {
    #createWorker;
    #worker;
    #pending = new Map();
    #nextId = 0;
    #disposed = false;

    constructor(createWorker) {
        this.#createWorker = createWorker;
    }

    compile(source) {
        if (this.#disposed) return Promise.reject(new Error("Faust compiler is disposed."));
        if (typeof source !== "string" || !source.trim())
            return Promise.reject(new Error("Faust source is empty."));

        try {
            if (!this.#worker) this.#openWorker();
        } catch (error) {
            return Promise.reject(error);
        }
        const id = ++this.#nextId;
        return new Promise((resolve, reject) => {
            this.#pending.set(id, { resolve, reject });
            try {
                this.#worker.postMessage({ id, source });
            } catch (error) {
                this.#pending.delete(id);
                reject(error);
            }
        });
    }

    #openWorker() {
        const worker = this.#createWorker();
        this.#worker = worker;
        worker.onmessage = event => {
            const { id, factory, error } = event.data;
            const pending = this.#pending.get(id);
            if (!pending) return;
            this.#pending.delete(id);
            if (error || !factory) pending.reject(new Error(error || "Faust returned no factory."));
            else pending.resolve(factory);
        };
        worker.onerror = event => {
            event.preventDefault?.();
            if (this.#worker === worker)
                this.#close(new Error(event.message || "Faust compiler worker failed."));
        };
        worker.onmessageerror = () => {
            if (this.#worker === worker)
                this.#close(new Error("Could not receive the compiled Faust graph."));
        };
    }

    #close(error) {
        this.#worker?.terminate();
        this.#worker = undefined;
        for (const request of this.#pending.values()) request.reject(error);
        this.#pending.clear();
    }

    dispose() {
        this.#disposed = true;
        this.#close(new Error("Faust compiler is disposed."));
    }
}

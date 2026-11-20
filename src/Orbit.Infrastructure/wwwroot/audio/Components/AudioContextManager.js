// Owns the lifetime of the application's one shared Web Audio context.
// The constructor dependency permits testing without browser globals.
export class AudioContextManager {
    #context;
    #createContext;

    constructor(createContext) {
        this.#createContext = createContext;
    }

    async getRunningContext() {
        // Called directly from the user gesture, before loading any DSP assets.
        this.#context ??= this.#createContext();

        if (this.#context.state !== "running") {
            await this.#context.resume();
        }

        return this.#context;
    }

    // Clock queries must never create or resume an AudioContext.
    getCurrentContext() {
        return this.#context;
    }

    async dispose() {
        if (this.#context && this.#context.state !== "closed") {
            await this.#context.close();
        }

        this.#context = undefined;
    }
}

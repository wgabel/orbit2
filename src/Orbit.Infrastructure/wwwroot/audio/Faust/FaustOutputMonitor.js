// Adapts Faust's passive output meters to a latest-value snapshot for the UI.
// No audio processing or UI rendering occurs here.
export class FaustOutputMonitor {
    #node;
    #values = new Map();

    constructor(node) {
        this.#node = node;
        node.setOutputParamHandler((path, value) => {
            if (this.#node && Number.isFinite(value)) this.#values.set(path, value);
        });
    }

    getSnapshot() {
        return Object.fromEntries(this.#values);
    }

    dispose() {
        this.#node?.setOutputParamHandler(null);
        this.#node = undefined;
        this.#values.clear();
    }
}

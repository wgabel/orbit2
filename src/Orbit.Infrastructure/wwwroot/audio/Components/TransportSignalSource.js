import { toTransportAnchor } from "./TransportPhase.js";

// Lazily owns the single transport worklet and its outgoing connections.
// Tempo edits do not load a module, create a context, or send per-frame data.
export class TransportSignalSource {
    #context;
    #node;
    #loading;
    #targets = new Set();
    #anchor = toTransportAnchor({ beatsPerMinute: 120, beatPosition: 0, isPlaying: false });

    setSnapshot(snapshot) {
        this.#anchor = toTransportAnchor(snapshot);
        this.#node?.port.postMessage(this.#anchor);
    }

    async connect(context, target) {
        if (this.#context !== context) {
            this.dispose();
            this.#context = context;
        }
        if (!this.#node) {
            const loading = this.#loading ??= context.audioWorklet.addModule(
                new URL("./TransportSignalProcessor.js", import.meta.url));
            try {
                await loading;
                if (this.#context !== context) throw new Error("Transport context changed.");
                this.#node ??= new AudioWorkletNode(context, "orbit-transport-signal", {
                    numberOfInputs: 0, numberOfOutputs: 1, outputChannelCount: [1],
                    processorOptions: { anchor: this.#anchor }
                });
            } catch (error) {
                if (this.#loading === loading) this.#loading = undefined;
                throw error;
            }
        }
        this.#node.connect(target);
        this.#targets.add(target);
    }

    disconnect(target) {
        if (this.#targets.delete(target)) this.#node?.disconnect(target);
    }

    dispose() {
        this.#node?.disconnect();
        this.#node?.port.close();
        this.#node = undefined;
        this.#loading = undefined;
        this.#context = undefined;
        this.#targets.clear();
    }
}

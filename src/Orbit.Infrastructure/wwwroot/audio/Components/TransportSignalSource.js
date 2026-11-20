import { toTransportAnchor } from "./TransportPhase.js";

// Lazily owns the single transport worklet and its outgoing connections.
// Tempo edits do not load a module, create a context, or send per-frame data.
export class TransportSignalSource {
    #context;
    #node;
    #loading;
    #targets = new Set();
    #tempoControls = new Map();
    #anchor = toTransportAnchor({ beatsPerMinute: 120, beatPosition: 0, isPlaying: false });

    setSnapshot(snapshot) {
        const previousTempo = this.#anchor.beatsPerMinute;
        this.#anchor = toTransportAnchor(snapshot);
        this.#node?.port.postMessage(this.#anchor);
        if (previousTempo !== this.#anchor.beatsPerMinute)
            for (const [target, path] of this.#tempoControls)
                target.setParamValue(path, this.#anchor.beatsPerMinute);
    }

    async connect(context, target) {
        if (this.#context !== context) {
            this.dispose();
            this.#context = context;
        }
        // Tempo-aware effects need the absolute BPM, not the derivative of a
        // wrapped Float32 beat signal. Only phase consumers need the worklet.
        const tempoPath = target.getParams().find(path => path.endsWith("/orbit_tempo"));
        if (target.numberOfInputs === 0) {
            if (tempoPath) {
                target.setParamValue(tempoPath, this.#anchor.beatsPerMinute);
                this.#tempoControls.set(target, tempoPath);
            }
            return;
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
        if (tempoPath) {
            target.setParamValue(tempoPath, this.#anchor.beatsPerMinute);
            this.#tempoControls.set(target, tempoPath);
        }
    }

    disconnect(target) {
        this.#tempoControls.delete(target);
        if (this.#targets.delete(target)) this.#node?.disconnect(target);
    }

    dispose() {
        this.#node?.disconnect();
        this.#node?.port.close();
        this.#node = undefined;
        this.#loading = undefined;
        this.#context = undefined;
        this.#targets.clear();
        this.#tempoControls.clear();
    }
}

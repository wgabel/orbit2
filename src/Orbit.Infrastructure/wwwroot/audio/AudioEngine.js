// Coordinates infrastructure components and owns active playback lifetimes.
// It neither loads WASM directly nor performs sample processing.
export class AudioEngine {
    #contextManager;
    #nodeFactory;
    #output;
    #createMonitor;
    #active = new Map();
    #disposed = false;

    constructor(contextManager, nodeFactory, output, createMonitor) {
        this.#contextManager = contextManager;
        this.#nodeFactory = nodeFactory;
        this.#output = output;
        this.#createMonitor = createMonitor;
    }

    async play(request) {
        this.#validate(request);
        if (this.#disposed) {
            throw new Error("The audio engine is disposed.");
        }
        if (this.#active.has(request.soundId)) {
            throw new Error("Playback already active: " + request.soundId);
        }

        const playback = {
            cancelled: false, voice: undefined, node: undefined,
            controlNames: new Set(Object.keys(request.parameters ?? {})),
            pendingParameters: Object.create(null)
        };
        this.#active.set(request.soundId, playback);

        try {
            // Resume before any DSP download to preserve user activation.
            const context = await this.#contextManager.getRunningContext();
            if (playback.cancelled) return;

            const node = await this.#nodeFactory.create(
                context, request.processor, request.parameters ?? {}, request.source);

            if (playback.cancelled) {
                node.disconnect();
                node.destroy?.();
                return;
            }

            try {
                // Edits made during worker compilation are applied before the
                // voice attacks, with the last value winning for each control.
                if (Object.keys(playback.pendingParameters).length > 0)
                    this.#nodeFactory.setParameters(node, playback.pendingParameters);
                playback.node = node;
                playback.voice = this.#output.createVoice(context, node);
            } catch (error) {
                node.disconnect();
                node.destroy?.();
                throw error;
            }

            playback.monitor = this.#createMonitor(node);
            if (!playback.cancelled) {
                await playback.voice.play(request);
            }
        } finally {
            try {
                try {
                    playback.monitor?.dispose();
                } finally {
                    playback.voice?.dispose();
                }
            } finally {
                this.#active.delete(request.soundId);
            }
        }
    }

    getPlaybackState(soundId) {
        const playback = this.#active.get(soundId);
        if (!playback) return "idle";
        if (playback.cancelled) return "stopping";
        return playback.voice ? "playing" : "starting";
    }

    // Only values of existing controls may change; this never recreates a node.
    setParameters(soundId, parameters) {
        const playback = this.#active.get(soundId);
        if (!playback || playback.cancelled) return false;
        if (!parameters || typeof parameters !== "object" || Array.isArray(parameters))
            throw new Error("Invalid audio parameter update.");
        for (const [name, value] of Object.entries(parameters)) {
            if (!playback.controlNames.has(name) || !Number.isFinite(value))
                throw new Error("Invalid audio parameter: " + name);
        }
        if (playback.node) this.#nodeFactory.setParameters(playback.node, parameters);
        else Object.assign(playback.pendingParameters, parameters);
        return true;
    }

    // A snapshot is read at display rate, independently of the sample clock.
    getOutputValues(soundId) {
        const playback = this.#active.get(soundId);
        return !playback || playback.cancelled ? null : playback.monitor?.getSnapshot() ?? null;
    }

    stop(soundId) {
        const playback = this.#active.get(soundId);
        if (playback) {
            playback.cancelled = true;
            playback.voice?.stop();
        }
    }

    async dispose() {
        this.#disposed = true;
        for (const soundId of this.#active.keys()) {
            this.stop(soundId);
        }

        this.#output.dispose();
        await this.#contextManager.dispose();
    }

    #validate(request) {
        if (!request || typeof request.soundId !== "string" || !request.soundId ||
            typeof request.processor !== "string" || !request.processor ||
            !Number.isFinite(request.volume) || request.volume < 0 || request.volume > 1 ||
            (request.source != null && (typeof request.source !== "string" || !request.source.trim())) ||
            (request.parameters != null &&
                (typeof request.parameters !== "object" || Array.isArray(request.parameters)))) {
            throw new Error("Invalid audio playback request.");
        }
    }
}

// Coordinates infrastructure components and owns active playback lifetimes.
// It neither loads WASM directly nor performs sample processing.
export class AudioEngine {
    #contextManager;
    #nodeFactory;
    #output;
    #active = new Map();
    #disposed = false;

    constructor(contextManager, nodeFactory, output) {
        this.#contextManager = contextManager;
        this.#nodeFactory = nodeFactory;
        this.#output = output;
    }

    async play(request) {
        this.#validate(request);
        if (this.#disposed) {
            throw new Error("The audio engine is disposed.");
        }
        if (this.#active.has(request.soundId)) {
            throw new Error("Playback already active: " + request.soundId);
        }

        const playback = { cancelled: false, voice: undefined };
        this.#active.set(request.soundId, playback);

        try {
            // Resume before any DSP download to preserve user activation.
            const context = await this.#contextManager.getRunningContext();
            if (playback.cancelled) return;

            const node = await this.#nodeFactory.create(
                context, request.processor, request.parameters ?? {});

            if (playback.cancelled) {
                node.disconnect();
                node.destroy?.();
                return;
            }

            try {
                playback.voice = this.#output.createVoice(context, node);
            } catch (error) {
                node.disconnect();
                node.destroy?.();
                throw error;
            }

            if (!playback.cancelled) {
                await playback.voice.play(request);
            }
        } finally {
            try {
                playback.voice?.dispose();
            } finally {
                this.#active.delete(request.soundId);
            }
        }
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
            (request.durationMilliseconds !== null &&
                (!Number.isFinite(request.durationMilliseconds) || request.durationMilliseconds <= 0)) ||
            !Number.isFinite(request.volume) || request.volume < 0 || request.volume > 1 ||
            (request.parameters != null &&
                (typeof request.parameters !== "object" || Array.isArray(request.parameters)))) {
            throw new Error("Invalid audio playback request.");
        }
    }
}

// Represents one active signal path. It owns its source and voice gain;
// the output component owns the shared master output.
export class AudioVoice {
    #context;
    #node;
    #gainNode;
    #envelope;
    #endTime;
    #disposed = false;

    constructor(context, node, gainNode, envelope) {
        this.#context = context;
        this.#node = node;
        this.#gainNode = gainNode;
        this.#envelope = envelope;
    }

    async play(request) {
        const startTime = this.#context.currentTime + 0.01;
        this.#envelope.scheduleAttack(this.#gainNode.gain, startTime, request.volume);
        // Only stop establishes an end time for a sustained graph.
        this.#endTime = Infinity;

        // This timer only observes the audio clock and handles cleanup.
        // All audible timing is scheduled by the AudioEnvelope.
        while (!this.#disposed && this.#context.state !== "closed" &&
            this.#context.currentTime < this.#endTime) {
            const remaining = (this.#endTime - this.#context.currentTime) * 1000;
            await new Promise(resolve => setTimeout(resolve, Math.max(1, Math.min(50, remaining))));
        }
    }

    stop() {
        if (this.#disposed || this.#endTime === undefined) {
            return;
        }

        if (this.#context.state !== "running") {
            this.#gainNode.gain.cancelScheduledValues(this.#context.currentTime);
            this.#gainNode.gain.value = 0;
            this.#endTime = this.#context.currentTime;
            return;
        }

        if (this.#context.currentTime < this.#endTime) {
            const now = this.#context.currentTime;
            const duration = Math.min(0.04, this.#endTime - now);
            this.#endTime = this.#envelope.release(this.#gainNode.gain, now, duration);
        }
    }

    dispose() {
        if (this.#disposed) {
            return;
        }

        this.#disposed = true;
        try {
            this.#node.disconnect();
            this.#node.destroy?.();
        } finally {
            this.#gainNode.disconnect();
        }
    }
}

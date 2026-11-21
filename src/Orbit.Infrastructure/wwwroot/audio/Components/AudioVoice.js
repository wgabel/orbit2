// Represents one active signal path. It owns its source and voice gain;
// the output component owns the shared master output.
export class AudioVoice {
    #context;
    #node;
    #gainNode;
    #envelope;
    #endTime;
    #disposed = false;
    #capture;

    constructor(context, node, gainNode, envelope, capture) {
        this.#context = context;
        this.#node = node;
        this.#gainNode = gainNode;
        this.#envelope = envelope;
        this.#capture = capture;
    }

    readSignal(subscription, channel, generation) { return this.#capture?.read(subscription, channel, generation) ?? null; }
    readSignalWindow(subscription, channel, generation) { return this.#capture?.readWindow(subscription, channel, generation) ?? null; }
    releaseSignal(subscription) { this.#capture?.release(subscription); }

    play(request, transition) {
        const startTime = transition?.startTime ?? this.#context.currentTime + 0.01;
        this.#envelope.scheduleAttack(this.#gainNode.gain, startTime, request.volume, transition?.duration);
        // Only stop establishes an end time for a sustained graph.
        this.#endTime = Infinity;
        return this.#waitForEnd();
    }

    async #waitForEnd() {
        // This timer only observes the audio clock and handles cleanup.
        // All audible timing is scheduled by the AudioEnvelope.
        while (!this.#disposed && this.#context.state !== "closed" &&
            this.#context.currentTime < this.#endTime) {
            const remaining = (this.#endTime - this.#context.currentTime) * 1000;
            await new Promise(resolve => setTimeout(resolve, Math.max(1, Math.min(50, remaining))));
        }
    }

    stop(transition) {
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
            const startTime = transition?.startTime ?? now;
            const duration = Math.max(0, Math.min(transition?.duration ?? 0.04, this.#endTime - startTime));
            this.#endTime = this.#envelope.release(this.#gainNode.gain, startTime, duration);
        }
    }

    dispose() {
        if (this.#disposed) {
            return;
        }

        this.#disposed = true;
        try {
            this.#capture?.dispose();
            this.#node.disconnect();
            this.#node.destroy?.();
        } finally {
            this.#gainNode.disconnect();
        }
    }
}

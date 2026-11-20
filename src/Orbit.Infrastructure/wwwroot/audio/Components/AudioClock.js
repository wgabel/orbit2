// Observes the existing AudioContext without starting or resuming it.
// Web Audio owns elapsed audio time; this class never maintains its own timer.
export class AudioClock {
    #contextManager;

    constructor(contextManager) {
        this.#contextManager = contextManager;
    }

    getSnapshot() {
        const context = this.#contextManager.getCurrentContext();
        if (!context || context.state === "closed") return null;

        // currentTime is the context's render timeline (block-granular on the
        // control thread). DSP itself executes against its actual sample clock.
        return {
            currentSample: Math.round(context.currentTime * context.sampleRate),
            sampleRate: context.sampleRate,
            isRunning: context.state === "running"
        };
    }
}

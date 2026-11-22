// Small, context-wide host diagnostic aggregate. No audio-thread instrumentation.
export class RuntimeDiagnostics {
    #clock;
    #nextId = 0;
    #lastAttempt = null;
    #lastAccepted = null;
    #attempts = 0;
    #accepted = 0;
    #failed = 0;
    #superseded = 0;
    #cancelled = 0;
    #pending = 0;
    #processorErrors = 0;
    #collecting = true;

    constructor(clock = () => globalThis.performance.now()) { this.#clock = clock; }

    begin(kind, request, revision = request.graphRevision ?? null) {
        const owner = this;
        const attempt = { id: ++this.#nextId, kind, revision, generation: request.generation ?? null,
            start: this.#clock(), timings: { get collecting() { return owner.#collecting; } }, result: "pending", finished: false };
        this.#attempts++;
        this.#pending++;
        this.#lastAttempt = attempt;
        return attempt;
    }

    accept(attempt, fadeDurationMs = null) {
        if (!this.#collecting || attempt.finished || attempt.result !== "pending") return;
        attempt.acceptanceMs = this.#clock() - attempt.start;
        attempt.fadeDurationMs = fadeDurationMs;
        attempt.result = "accepted";
        this.#accepted++;
        this.#lastAccepted = attempt;
    }

    finish(attempt, outcome) {
        if (!this.#collecting || attempt.finished) return;
        attempt.finished = true;
        this.#pending--;
        attempt.completionMs = this.#clock() - attempt.start;
        if (attempt.result === "accepted") return; // a scheduled swap remains accepted even if stopped during fade
        attempt.result = outcome;
        if (outcome === "failed") this.#failed++;
        else if (outcome === "superseded") this.#superseded++;
        else this.#cancelled++;
    }

    stop() { this.#collecting = false; }
    processorError() { if (this.#collecting) this.#processorErrors++; }

    #operation(attempt) {
        if (!attempt) return null;
        return {
            id: attempt.id, kind: attempt.kind, revision: attempt.revision, generation: attempt.generation,
            result: attempt.result, workerRoundTripMs: attempt.timings.workerRoundTripMs ?? null,
            workletCreateMs: attempt.timings.workletCreateMs ?? null,
            parameterBindingMs: attempt.timings.parameterBindingMs ?? null,
            acceptanceMs: attempt.acceptanceMs ?? null, fadeDurationMs: attempt.fadeDurationMs ?? null,
            completionMs: attempt.completionMs ?? null
        };
    }

    snapshot(playbacks) {
        let currentVoices = 0, retiringVoices = 0, outputChannels = 0, occupiedAuxSlots = 0, captureLeases = 0;
        for (const playback of playbacks) {
            for (const instance of playback.voices) {
                if (!playback.cancelled && instance === playback.current) currentVoices++;
                else retiringVoices++;
                outputChannels += instance.outputChannels;
                const capture = instance.voice.getSignalDiagnostics?.();
                occupiedAuxSlots += capture?.occupiedSlots ?? 0;
                captureLeases += capture?.activeLeases ?? 0;
            }
        }
        return {
            attempts: this.#attempts, accepted: this.#accepted, failed: this.#failed,
            superseded: this.#superseded, cancelled: this.#cancelled, pending: this.#pending, processorErrors: this.#processorErrors,
            currentVoices, retiringVoices, outputChannels, occupiedAuxSlots, captureLeases,
            lastAttempt: this.#operation(this.#lastAttempt), lastAccepted: this.#operation(this.#lastAccepted)
        };
    }
}

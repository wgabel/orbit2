// Passive context-global metrics. Never create/resume a context, reset browser
// statistics, acquire analysis taps or run code on the audio rendering thread.
const finite = (value, integer = false) => typeof value === "number" &&
    Number.isFinite(value) && value >= 0 && (!integer || Number.isSafeInteger(value)) ? value : null;
const secondsToMs = value => value === null ? null : finite(value * 1000);
const read = (object, key, integer = false) => {
    try { return finite(object?.[key], integer); } catch { return null; }
};
const readCounters = context => {
    let stats;
    try { stats = context?.playbackStats; } catch { /* unavailable */ }
    return {
        underrunEvents: read(stats, "underrunEvents", true),
        underrunDurationMs: secondsToMs(read(stats, "underrunDuration")),
        playbackDurationMs: secondsToMs(read(stats, "totalDuration"))
    };
};
const fields = ["underrunEvents", "underrunDurationMs", "playbackDurationMs"];

export class AudioPerformanceMonitor {
    #context;
    #active = new Set();
    #baseline = null;
    #last = null;
    #totals = null;
    #epoch = 0;
    #sampleNumber = 0;
    #origins = {};

    // Called only when a logical playback becomes accepted, not on graph swaps.
    begin(playback, context) {
        if (context !== this.#context) {
            this.#context = context;
            this.#active.clear();
            this.#totals = null;
        }
        if (!this.#active.size) this.#startBaseline();
        this.#active.add(playback);
    }

    end(playback) {
        if (!this.#active.delete(playback)) return;
        if (!this.#active.size) this.#sample(); // freeze final totals, excluding later idle time
    }

    #startBaseline(counters = readCounters(this.#context)) {
        this.#baseline = counters;
        this.#sampleNumber++;
        this.#origins = Object.fromEntries(fields.map(key => [key, this.#sampleNumber]));
        this.#last = { ...this.#baseline };
        this.#totals = Object.fromEntries(fields.map(key => [key, this.#baseline[key] === null ? null : 0]));
        this.#epoch++;
    }

    #sample() {
        const current = readCounters(this.#context);
        this.#sampleNumber++;
        // A decreased counter means the source was reset. Never subtract across
        // that boundary or report negative counts.
        if (fields.some(key => current[key] !== null && this.#last[key] !== null &&
            current[key] < this.#last[key])) {
            this.#startBaseline(current);
            return;
        }
        for (const key of fields) {
            if (current[key] === null) {
                this.#baseline[key] = null;
                this.#totals[key] = null;
            } else if (this.#baseline[key] === null) {
                // A newly supported/recovered metric has no earlier baseline.
                this.#baseline[key] = current[key];
                this.#origins[key] = this.#sampleNumber;
                this.#totals[key] = null;
            } else {
                this.#totals[key] = current[key] - this.#baseline[key];
            }
        }
        this.#last = current;
    }

    snapshot(context, reset = false) {
        if (context !== this.#context) {
            this.#context = context;
            this.#active.clear();
            this.#totals = null;
            this.#baseline = this.#last = null;
        }
        const active = this.#active.size > 0;
        if (active && reset) this.#startBaseline();
        else if (active) this.#sample();

        const baseLatencyMs = secondsToMs(read(context, "baseLatency"));
        const outputLatencyMs = secondsToMs(read(context, "outputLatency"));
        const totals = this.#totals ?? {};
        const missing = totals.underrunDurationMs ?? null;
        const duration = totals.playbackDurationMs ?? null;
        return {
            sessionEpoch: this.#epoch,
            playbackState: active ? "active" : this.#totals ? "stopped" : "idle",
            contextState: context?.state ?? "not-created",
            sampleRateHz: read(context, "sampleRate"),
            baseLatencyMs,
            outputLatencyMs,
            estimatedPipelineLatencyMs: baseLatencyMs === null || outputLatencyMs === null
                ? null : finite(baseLatencyMs + outputLatencyMs),
            underrunEvents: totals.underrunEvents ?? null,
            underrunDurationMs: missing,
            playbackDurationMs: duration,
            missingAudioPercent: missing !== null && duration > 0 && missing <= duration &&
                this.#origins.underrunDurationMs === this.#origins.playbackDurationMs
                ? finite(100 * missing / duration) : null
        };
    }
}

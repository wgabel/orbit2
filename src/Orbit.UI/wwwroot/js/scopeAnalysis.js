// Pure time-domain analysis. Times refer to the approximate audio-context
// end of the native analyzer window, not a sample-accurate worklet timestamp.
export function measureLevels(samples, start = 0) {
    let peak = 0, squares = 0, count = 0;
    for (let i = start; i < samples.length; i++) {
        const sample = samples[i];
        if (!Number.isFinite(sample)) continue;
        peak = Math.max(peak, Math.abs(sample));
        squares += sample * sample;
        count++;
    }
    return { peak, rms: count ? Math.sqrt(squares / count) : 0 };
}

export const toDecibels = amplitude => amplitude > 0 ? 20 * Math.log10(amplitude) : -Infinity;

export function scopeWindow(samples, sampleRate, seconds, trigger) {
    const length = Math.min(samples.length, Math.max(2, Math.round(sampleRate * seconds)));
    let start = samples.length - length;
    let triggered = false;
    if (trigger !== "free") {
        // Find the newest crossing with a complete window after it.
        for (let i = start; i > 0; i--) {
            const a = samples[i - 1], b = samples[i];
            if (trigger === "rising" ? a < 0 && b >= 0 : a > 0 && b <= 0) {
                start = i;
                triggered = true;
                break;
            }
        }
    }
    return { samples: samples.subarray(start, start + length), seconds: length / sampleRate, triggered };
}

// The bucket number stored in each ring slot distinguishes gaps and wraps.
// Preserve both extrema rather than averaging away brief modulation peaks.
export class MinMaxHistory {
    static BucketSeconds = 0.01;
    static Capacity = 1000;
    #keys = new Float64Array(MinMaxHistory.Capacity).fill(NaN);
    #minimum = new Float32Array(MinMaxHistory.Capacity);
    #maximum = new Float32Array(MinMaxHistory.Capacity);
    #lastEnd = null;
    #rate = null;

    clear() { this.#keys.fill(NaN); this.#lastEnd = null; this.#rate = null; }

    append(samples, sampleRate, time) {
        const end = Math.round(time * sampleRate);
        if (this.#rate !== sampleRate || (this.#lastEnd !== null && end < this.#lastEnd)) this.clear();
        this.#rate = sampleRate;
        const begin = end - samples.length;
        const first = Math.max(0, begin, this.#lastEnd ?? begin);
        for (let frame = first; frame < end; frame++) {
            const sample = samples[frame - begin];
            if (!Number.isFinite(sample)) continue;
            const key = Math.floor(frame / (sampleRate * MinMaxHistory.BucketSeconds));
            const slot = key % MinMaxHistory.Capacity;
            if (this.#keys[slot] !== key) {
                this.#keys[slot] = key;
                this.#minimum[slot] = this.#maximum[slot] = sample;
            } else {
                this.#minimum[slot] = Math.min(this.#minimum[slot], sample);
                this.#maximum[slot] = Math.max(this.#maximum[slot], sample);
            }
        }
        this.#lastEnd = end;
    }

    // Invoke the consumer only for actual captured bins, including gaps.
    visit(seconds, visitor) {
        if (this.#lastEnd === null) return;
        const count = Math.min(MinMaxHistory.Capacity, Math.max(1, Math.round(seconds / MinMaxHistory.BucketSeconds)));
        const last = Math.ceil(this.#lastEnd / (this.#rate * MinMaxHistory.BucketSeconds)) - 1;
        for (let index = 0; index < count; index++) {
            const key = last - count + 1 + index;
            if (key < 0) continue;
            const slot = key % MinMaxHistory.Capacity;
            if (this.#keys[slot] === key) visitor(index / Math.max(1, count - 1), this.#minimum[slot], this.#maximum[slot], key);
        }
    }
}

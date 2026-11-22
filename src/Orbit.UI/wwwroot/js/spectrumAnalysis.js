// Bounded, reusable radix-2 FFT plan. Magnitudes are single-sided peak
// amplitudes normalized by window coherent gain (unit sine = 0 dBFS).
export class SpectrumAnalyzer {
    static Sizes = Object.freeze([1024, 2048, 4096, 8192]);
    #size; #smoothing; #window; #windowSum = 0; #reverse; #cos; #sin;
    #real; #imag; #power; #decibels; #lastTime = null; #sampleRate = null;
    #result = null;

    constructor(size, window = "hann", smoothingSeconds = 0) {
        if (!SpectrumAnalyzer.Sizes.includes(size) || !["hann", "rectangular"].includes(window) ||
            !Number.isFinite(smoothingSeconds) || smoothingSeconds < 0 || smoothingSeconds > 1)
            throw new RangeError("Invalid spectrum configuration");
        this.#size = size;
        this.#smoothing = smoothingSeconds;
        this.#window = new Float64Array(size);
        this.#reverse = new Uint16Array(size);
        this.#cos = new Float64Array(size / 2);
        this.#sin = new Float64Array(size / 2);
        this.#real = new Float64Array(size);
        this.#imag = new Float64Array(size);
        this.#power = new Float64Array(size / 2 + 1);
        this.#decibels = new Float64Array(size / 2 + 1);
        const bits = Math.log2(size);
        for (let i = 0; i < size; i++) {
            const weight = window === "hann" ? 0.5 - 0.5 * Math.cos(2 * Math.PI * i / size) : 1;
            this.#window[i] = weight;
            this.#windowSum += weight;
            let reversed = 0, value = i;
            for (let bit = 0; bit < bits; bit++) {
                reversed = (reversed << 1) | (value & 1);
                value >>>= 1;
            }
            this.#reverse[i] = reversed;
            if (i < size / 2) {
                this.#cos[i] = Math.cos(-2 * Math.PI * i / size);
                this.#sin[i] = Math.sin(-2 * Math.PI * i / size);
            }
        }
    }

    reset() { this.#lastTime = this.#sampleRate = this.#result = null; }

    // Returned bin storage is owned by this analyzer and reused on the next
    // frame. The input is never changed or retained.
    analyze(samples, sampleRate, time) {
        if (samples.length < this.#size || !Number.isFinite(sampleRate) || sampleRate <= 0 || !Number.isFinite(time))
            throw new RangeError("Invalid spectrum input");
        if (this.#sampleRate !== sampleRate || (this.#lastTime !== null && time < this.#lastTime)) this.reset();
        if (this.#lastTime === time && this.#result) return this.#result;
        const size = this.#size, offset = samples.length - size;
        const real = this.#real, imag = this.#imag;
        // Estimate DC in the same window used for the FFT. Subtracting an
        // unweighted block mean before a Hann window introduces a spurious
        // first-bin component when the captured tone is not bin-centered.
        // Center the weighted sum on the first sample so a constant signal
        // subtracts exactly to zero, even with floating-point window weights.
        const reference = Number.isFinite(samples[offset]) ? samples[offset] : 0;
        let mean = 0, weightedDifference = 0;
        for (let i = 0; i < size; i++) {
            const sample = Number.isFinite(samples[offset + i]) ? samples[offset + i] : 0;
            mean += sample;
            weightedDifference += (sample - reference) * this.#window[i];
        }
        mean /= size;
        const dcOffset = reference + weightedDifference / this.#windowSum;
        for (let i = 0; i < size; i++) {
            const sample = samples[offset + i];
            // The reported mean remains the actual arithmetic sample mean.
            real[this.#reverse[i]] = ((Number.isFinite(sample) ? sample : 0) - dcOffset) * this.#window[i];
        }
        imag.fill(0);
        for (let length = 2; length <= size; length *= 2) {
            const half = length / 2, stride = size / length;
            for (let block = 0; block < size; block += length) {
                for (let j = 0; j < half; j++) {
                    const a = block + j, b = a + half, twiddle = j * stride;
                    const r = real[b] * this.#cos[twiddle] - imag[b] * this.#sin[twiddle];
                    const v = real[b] * this.#sin[twiddle] + imag[b] * this.#cos[twiddle];
                    real[b] = real[a] - r; imag[b] = imag[a] - v;
                    real[a] += r; imag[a] += v;
                }
            }
        }
        const alpha = this.#lastTime === null || !this.#smoothing ? 0 :
            Math.exp(-(time - this.#lastTime) / this.#smoothing);
        let peakBin = 0, peakPower = 0;
        for (let bin = 0; bin <= size / 2; bin++) {
            const factor = (bin === 0 || bin === size / 2 ? 1 : 2) / this.#windowSum;
            const power = (real[bin] ** 2 + imag[bin] ** 2) * factor ** 2;
            this.#power[bin] = alpha * this.#power[bin] + (1 - alpha) * power;
            this.#decibels[bin] = this.#power[bin] > 0 ? 10 * Math.log10(this.#power[bin]) : -Infinity;
            // The windowed DC bin is not eligible for the positive-frequency peak.
            if (bin > 0 && this.#power[bin] > peakPower) {
                peakBin = bin;
                peakPower = this.#power[bin];
            }
        }
        this.#lastTime = time;
        this.#sampleRate = sampleRate;
        return this.#result = {
            decibels: this.#decibels, binWidth: sampleRate / size,
            peakBin, peakDecibels: peakBin ? this.#decibels[peakBin] : -Infinity,
            mean
        };
    }
}

// Logarithmic positive-frequency axis. The actual first resolvable bin and
// Nyquist, not an invented audio-rate range, define the endpoints.
export function spectrumFrequencyPosition(frequency, minimum, maximum) {
    return Math.log(frequency / minimum) / Math.log(maximum / minimum);
}

export function spectrumTicks(minimum, maximum) {
    const ticks = [];
    for (let decade = 10 ** Math.floor(Math.log10(minimum)); decade <= maximum; decade *= 10)
        for (const factor of [1, 2, 5]) {
            const frequency = decade * factor;
            if (frequency >= minimum && frequency <= maximum) ticks.push(frequency);
        }
    return ticks;
}

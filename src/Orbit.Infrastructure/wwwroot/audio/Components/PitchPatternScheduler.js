// A fixed-capacity configuration is replaced atomically at a render-quantum boundary.
// No clocks, timers, allocations, or historical event replay in render().
export function validPitchPattern(p) {
    return p && typeof p.loop === 'boolean' && typeof p.free === 'boolean' &&
        Number.isInteger(p.count) && p.count >= 1 && p.count <= 64 &&
        Number.isInteger(p.ticks) && p.ticks >= 1 && p.ticks <= 1024 &&
        Number.isFinite(p.seconds) && p.seconds >= .01 && p.seconds <= 600 &&
        Array.isArray(p.steps) && p.steps.length === 64 && p.steps.every(n => Number.isInteger(n) && n >= 0 && n <= 128);
}

export class PitchPatternScheduler {
    constructor(pattern) {
        if (!validPitchPattern(pattern)) throw new Error('Invalid pitch pattern.');
        this.pattern = pattern;
        this.ordinal = -1;
        this.generation = -1;
        this.dirty = true;
    }

    setPattern(pattern) {
        if (!validPitchPattern(pattern)) return false;
        this.pattern = pattern;
        this.dirty = true;
        return true;
    }

    render(output, clock) {
        if (!output || !clock?.[6]) return;
        const p = this.pattern;
        for (let i = 0; i < output.length; i++) {
            const generation = clock[6][i];
            const reset = this.generation !== -1 && generation !== this.generation;
            this.generation = generation;
            if (!p.loop) {
                if (this.dirty) output[i] += 65 * 256; // sentinel: no active step; preserve manual command
                this.dirty = false;
                this.ordinal = -1;
                continue;
            }
            const position = p.free ? clock[4][i] + clock[5][i] : clock[2][i] + clock[3][i];
            const duration = p.free ? p.seconds : p.ticks / 4;
            // At most 0.1 micro-unit tolerance compensates Float32 fraction rounding;
            // well below a sample at supported sample rates and tempos.
            const ordinal = Math.floor((position + 1e-7) / duration);
            if (!this.dirty && !reset && ordinal === this.ordinal) continue;
            const step = ordinal % p.count;
            let code = p.steps[step];
            // On seek, edit, or replacement, resolve the current slot instead of
            // replaying missed events. Leading Holds on the first pass have no predecessor.
            if (!code && (this.dirty || reset || ordinal !== this.ordinal + 1)) {
                const limit = Math.min(ordinal, p.count - 1);
                for (let back = 1; back <= limit && !code; back++) code = p.steps[(step - back + p.count) % p.count];
                if (!code && reset) code = 129;
            }
            // The existing manual input wins if it coincides with a boundary.
            output[i] = (step + 1) * 256 + (this.dirty && code ? code : output[i] || code);
            this.ordinal = ordinal;
            this.dirty = false;
        }
    }
}

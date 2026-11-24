// Fixed-capacity, sample-clocked pitch and optional note articulation.
// Existing pitch-only patterns preserve their exact legacy event stream.
// Low 8 bits: action code. Next 7: step/sentinel. Remaining bits:
// 0 = legacy/default velocity; 1..101 = explicit 0..100%. All words
// are exact Float32 integers and the existing manual lane is unchanged.
const VELOCITY_STRIDE = 32768;
function validVelocityOverride(value) {
    return value === -1 || Number.isFinite(value) && value >= 0 && value <= 1 &&
        value === Math.round(value * 100) / 100;
}
function encodeVelocity(value) {
    return value == null || value === -1 ? 0 : (Math.round(value * 100) + 1) * VELOCITY_STRIDE;
}
export function validPitchPattern(p) {
    return p && typeof p.loop === 'boolean' && typeof p.free === 'boolean' &&
        (p.notes === undefined || typeof p.notes === 'boolean') &&
        (p.gateFraction === undefined || Number.isFinite(p.gateFraction) && p.gateFraction >= .05 && p.gateFraction <= 1) &&
        (p.velocity === undefined || Number.isFinite(p.velocity) && p.velocity >= 0 && p.velocity <= 1) &&
        (p.gateFractions == null || Array.isArray(p.gateFractions) && p.gateFractions.length === 64 &&
            p.gateFractions.every(n => Number.isFinite(n) && n >= 0 && n <= 1)) &&
        (p.velocityOverrides == null || Array.isArray(p.velocityOverrides) && p.velocityOverrides.length === 64 &&
            p.velocityOverrides.every(validVelocityOverride)) &&
        Number.isInteger(p.count) && p.count >= 1 && p.count <= 64 &&
        Number.isInteger(p.ticks) && p.ticks >= 1 && p.ticks <= 1024 &&
        Number.isFinite(p.seconds) && p.seconds >= .01 && p.seconds <= 600 &&
        Array.isArray(p.steps) && p.steps.length === 64 && p.steps.every(n => Number.isInteger(n) && n >= 0 && n <= 129);
}

export class PitchPatternScheduler {
    constructor(pattern) {
        if (!validPitchPattern(pattern)) throw new Error('Invalid pitch pattern.');
        this.pattern = pattern;
        this.ordinal = -1;
        this.generation = -1;
        this.dirty = true;
        this.gateOpen = false;
        this.noteGateFraction = 0;
        this.releasedOrdinal = -1;
    }

    setPattern(pattern) {
        if (!validPitchPattern(pattern)) return false;
        const previous = this.pattern;
        const sameTiming = previous.loop && pattern.loop && previous.count === pattern.count &&
            previous.free === pattern.free && previous.ticks === pattern.ticks && previous.seconds === pattern.seconds &&
            Boolean(previous.notes) === Boolean(pattern.notes);
        this.pattern = pattern;
        if (!sameTiming) {
            this.dirty = true;
            this.gateOpen = false;
        }
        return true;
    }

    render(output, clock) {
        if (!output || !clock?.[6]) return;
        const p = this.pattern;
        const articulated = p.notes === true;
        for (let i = 0; i < output.length; i++) {
            const generation = clock[6][i];
            const reset = this.generation !== -1 && generation !== this.generation;
            this.generation = generation;
            if (!p.loop) {
                if (this.dirty) output[i] += 65 * 256; // sentinel: no active step
                this.dirty = false;
                this.ordinal = -1;
                this.gateOpen = false;
                continue;
            }
            const position = p.free ? clock[4][i] + clock[5][i] : clock[2][i] + clock[3][i];
            const duration = p.free ? p.seconds : p.ticks / 4;
            const ordinal = Math.floor((position + 1e-7) / duration);
            const step = ((ordinal % p.count) + p.count) % p.count;
            const changed = this.dirty || reset || ordinal !== this.ordinal;
            if (changed) {
                let code = p.steps[step];
                if (articulated) {
                    // 0 ties the previous note; 129 is an explicit rest.
                    // Release on reset/rest; ordinary note boundaries retrigger.
                    const discontinuity = reset || this.dirty || ordinal !== this.ordinal + 1;
                    if (discontinuity) this.gateOpen = false;
                    if (code === 129 || (code === 0 && discontinuity)) {
                        output[i] = (step + 1) * 256 + 130;
                        this.gateOpen = false;
                    } else {
                        output[i] = (step + 1) * 256 + (code || output[i]);
                        if (code >= 1 && code <= 128) {
                            this.gateOpen = true;
                            // Only scheduled onsets carry overrides; manual actions keep their legacy default.
                            output[i] += encodeVelocity(p.velocityOverrides?.[step]);
                            // Latch explicit duration at onset; ties inherit it.
                            this.noteGateFraction = p.gateFractions?.[step] ?? 0;
                        }
                    }
                    this.releasedOrdinal = -1;
                } else {
                    if (code === 129) code = 0; // Rest is silent in legacy pitch-only mode.
                    if (!code && (this.dirty || reset || ordinal !== this.ordinal + 1)) {
                        const limit = Math.min(ordinal, p.count - 1);
                        for (let back = 1; back <= limit && !code; back++) {
                            const candidate = p.steps[(step - back + p.count) % p.count];
                            if (candidate <= 128) code = candidate;
                        }
                        if (!code && reset) code = 129;
                    }
                    output[i] = (step + 1) * 256 + (this.dirty && code ? code : output[i] || code);
                }
                this.ordinal = ordinal;
                this.dirty = false;
            }
            // A tie continues through its step. Release near the end of the last
            // note/tie step, only when the following slot isn't another tie.
            if (articulated && this.gateOpen && this.releasedOrdinal !== ordinal &&
                p.steps[(step + 1) % p.count] !== 0 &&
                (position / duration - ordinal) >= (this.noteGateFraction || (p.gateFraction ?? .9))) {
                // Do not overwrite a boundary pulse; the next sample is enough.
                if (output[i] === 0) {
                    output[i] = (step + 1) * 256 + 130;
                    this.gateOpen = false;
                    this.releasedOrdinal = ordinal;
                }
            }
        }
    }
}

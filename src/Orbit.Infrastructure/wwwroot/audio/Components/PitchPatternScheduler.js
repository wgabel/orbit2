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
const MAX_TIMING_OFFSET = .45;
function validTimingOffset(value) {
    return Number.isFinite(value) && Math.abs(value) <= MAX_TIMING_OFFSET &&
        value === Math.round(value * 100) / 100;
}
function stepAt(p, ordinal) {
    return ((ordinal % p.count) + p.count) % p.count;
}
function offsetAt(p, ordinal) {
    const step = stepAt(p, ordinal);
    const code = p.steps[step];
    return code >= 1 && code <= 128 ? p.timingOffsets?.[step] ?? 0 : 0;
}
function hasTimingOffsets(p) {
    return p.notes === true && (p.timingOffsets?.some((value, step) =>
        value !== 0 && step < p.count && p.steps[step] >= 1 && p.steps[step] <= 128) ?? false);
}
// Adjacent onsets cannot cross with offsets bounded to ±.45 of one step.
// Ties and rests retain their grid boundaries. Before transport zero no events exist.
function timedOrdinal(p, position, duration) {
    const units = (position + 1e-7) / duration;
    const grid = Math.floor(units);
    if (units >= grid + 1 + offsetAt(p, grid + 1)) return grid + 1;
    if (units < grid + offsetAt(p, grid)) return grid - 1;
    return grid;
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
        (p.timingOffsets == null || Array.isArray(p.timingOffsets) && p.timingOffsets.length === 64 &&
            p.timingOffsets.every(validTimingOffset)) &&
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
        this.hasOffsets = hasTimingOffsets(pattern);
        this.lastPosition = null;
        this.skipThroughOrdinal = -1;
        this.noteTimingOffset = 0;
        this.noteEndOrdinal = -1;
    }

    setPattern(pattern) {
        if (!validPitchPattern(pattern)) return false;
        const previous = this.pattern;
        const sameTiming = previous.loop && pattern.loop && previous.count === pattern.count &&
            previous.free === pattern.free && previous.ticks === pattern.ticks && previous.seconds === pattern.seconds &&
            Boolean(previous.notes) === Boolean(pattern.notes);
        const hasOffsets = hasTimingOffsets(pattern);
        if (sameTiming && (this.hasOffsets || hasOffsets) && this.lastPosition !== null && !this.dirty) {
            const duration = pattern.free ? pattern.seconds : pattern.ticks / 4;
            // Do not replay an onset moved into the already-rendered past.
            const current = timedOrdinal(pattern, this.lastPosition, duration);
            if (current > this.ordinal) this.skipThroughOrdinal = Math.max(this.skipThroughOrdinal, current);
        }
        this.pattern = pattern;
        this.hasOffsets = hasOffsets;
        if (!sameTiming) {
            this.dirty = true;
            this.gateOpen = false;
            this.skipThroughOrdinal = -1;
            this.lastPosition = null;
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
                this.lastPosition = null;
                this.skipThroughOrdinal = -1;
                continue;
            }
            const position = p.free ? clock[4][i] + clock[5][i] : clock[2][i] + clock[3][i];
            const duration = p.free ? p.seconds : p.ticks / 4;
            const backwards = articulated && this.lastPosition !== null && position < this.lastPosition;
            let ordinal = this.hasOffsets ? timedOrdinal(p, position, duration) :
                Math.floor((position + 1e-7) / duration);
            // A live offset edit may move the current onset later; it must not retrigger.
            if (articulated && !this.dirty && !reset && !backwards) ordinal = Math.max(ordinal, this.ordinal);
            this.lastPosition = position;
            if (reset || backwards) this.skipThroughOrdinal = -1;
            // The first positive-offset onset waits; an early first onset is clipped to zero.
            if (ordinal < 0) {
                if (this.dirty || reset || backwards || this.ordinal >= 0) output[i] = 256 + 130;
                this.ordinal = -1;
                this.dirty = false;
                this.gateOpen = false;
                continue;
            }
            const step = stepAt(p, ordinal);
            const changed = this.dirty || reset || backwards || ordinal !== this.ordinal;
            if (changed) {
                let code = p.steps[step];
                if (articulated) {
                    // 0 ties the previous note; 129 is an explicit rest.
                    // Release on reset/rest; ordinary note boundaries retrigger.
                    const discontinuity = reset || backwards || this.dirty || ordinal !== this.ordinal + 1;
                    const skipped = ordinal <= this.skipThroughOrdinal;
                    if (ordinal > this.skipThroughOrdinal) this.skipThroughOrdinal = -1;
                    if (discontinuity) this.gateOpen = false;
                    if (skipped || code === 129 || (code === 0 && discontinuity)) {
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
                            const offset = offsetAt(p, ordinal);
                            this.noteTimingOffset = ordinal === 0 ? Math.max(0, offset) : offset;
                            this.noteEndOrdinal = ordinal;
                            if (this.noteTimingOffset !== 0) {
                                // Latch the tied extent for shifted notes, once per onset.
                                for (let next = 1; next < p.count && p.steps[(step + next) % p.count] === 0; next++)
                                    this.noteEndOrdinal++;
                            }
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
            if (articulated && this.gateOpen && this.releasedOrdinal !== ordinal) {
                const fraction = this.noteGateFraction || (p.gateFraction ?? .9);
                const releaseDue = this.noteTimingOffset !== 0
                    ? (position + 1e-7) / duration >= this.noteEndOrdinal + this.noteTimingOffset + fraction
                    : p.steps[(step + 1) % p.count] !== 0 && position / duration - ordinal >= fraction;
                if (!releaseDue) continue;
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

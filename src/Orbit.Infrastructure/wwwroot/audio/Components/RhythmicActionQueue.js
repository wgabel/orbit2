// Fixed per-lane queues. A zero between commands preserves one-sample pulses
// across render quanta, even when multiple host messages arrive together.
export const MAX_ACTIONS = 64;
export const MAX_RHYTHMIC_INPUTS = 30;

export class RhythmicActionQueue {
    constructor(count) {
        this.lanes = Array.from({ length: count }, () => ({ values: new Uint8Array(MAX_ACTIONS), head: 0, size: 0, gap: false }));
        this.consumed = new Uint16Array(count);
    }

    push(index, action) {
        const lane = this.lanes[index];
        if (!lane || (action !== 1 && action !== 2) || lane.size >= MAX_ACTIONS) return false;
        lane.values[(lane.head + lane.size++) % MAX_ACTIONS] = action;
        return true;
    }

    clear() {
        for (const lane of this.lanes) { lane.head = 0; lane.size = 0; lane.gap = false; }
    }

    render(channels, offset) {
        this.consumed.fill(0);
        for (let index = 0; index < this.lanes.length; index++) {
            const lane = this.lanes[index], output = channels[offset + index];
            if (!output) continue;
            output.fill(0);
            for (let i = 0; i < output.length; i++) {
                if (lane.gap) { lane.gap = false; continue; }
                if (!lane.size) break;
                output[i] = lane.values[lane.head]; lane.head = (lane.head + 1) % MAX_ACTIONS;
                lane.size--; lane.gap = true; this.consumed[index]++;
            }
        }
        return this.consumed;
    }
}

// Fixed per-lane queues. A zero between commands preserves one-sample pulses
// across render quanta, even when multiple host messages arrive together.
import { MonophonicNoteState, validNoteAction, isNoteRelease, NOTE_RESET } from './MonophonicNoteState.js';
export const MAX_ACTIONS = 64;
export const MAX_RHYTHMIC_INPUTS = 30;

export function validAction(input, action) {
    return input && Number.isInteger(action) && (input.isNote ? validNoteAction(action) : input.isPitch ? action >= 1 && action <= 129 :
        action === 1 || (action === 2 && input.allowClose));
}

export class RhythmicActionQueue {
    constructor(inputs) {
        this.inputs = typeof inputs === 'number' ? Array.from({ length: inputs }, () => ({allowClose: true})) : inputs;
        this.lanes = this.inputs.map(input => ({ values: new Uint32Array(MAX_ACTIONS), head: 0, size: 0, gap: false,
            dropped: 0, notes: input.isNote ? new MonophonicNoteState() : null }));
        this.consumed = new Uint16Array(this.inputs.length);
        for (let index = 0; index < this.inputs.length; index++)
            if (this.inputs[index].isPitch && this.inputs[index].initialAction) this.push(index, this.inputs[index].initialAction);
    }

    push(index, action) {
        const lane = this.lanes[index];
        if (!lane || !validAction(this.inputs[index], action)) return false;
        if (lane.size >= MAX_ACTIONS) {
            if (!lane.notes || !isNoteRelease(action)) return false;
            // Overload must fail closed, never leave a held gate. Acknowledge discarded words.
            lane.dropped += lane.size; lane.head = 0; lane.size = 0; action = NOTE_RESET;
        }
        lane.values[(lane.head + lane.size++) % MAX_ACTIONS] = action;
        return true;
    }

    clear() {
        for (const lane of this.lanes) { lane.head = 0; lane.size = 0; lane.gap = false; lane.dropped = 0; lane.notes?.apply(NOTE_RESET); }
    }

    render(channels, offset) {
        this.consumed.fill(0);
        for (let index = 0; index < this.lanes.length; index++) {
            const lane = this.lanes[index], output = channels[offset + index];
            if (!output) continue;
            output.fill(0);
            this.consumed[index] = lane.dropped; lane.dropped = 0;
            for (let i = 0; i < output.length; i++) {
                if (lane.gap) { lane.gap = false; continue; }
                if (!lane.size) break;
                const word = lane.values[lane.head];
                output[i] = lane.notes ? lane.notes.apply(word) : word; lane.head = (lane.head + 1) % MAX_ACTIONS;
                lane.size--; lane.gap = true; this.consumed[index]++;
            }
        }
        return this.consumed;
    }
}

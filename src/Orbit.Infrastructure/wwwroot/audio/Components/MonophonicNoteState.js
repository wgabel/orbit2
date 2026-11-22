// Fixed-capacity last-note priority. All state is owned by this worklet generation.
// Output words: note+1 + 512*velocity7; bit 17 marks a new strike; 129 closes gate.
export const NOTE_RESET = 129;
export const NOTE_STRIKE = 131072;
export const isNoteRelease = action => action >= 129 && action <= 257;
export function validNoteAction(action) {
    if (!Number.isInteger(action) || action < 1 || action > 127 * 512 + 128) return false;
    const note = action % 512;
    return (note >= 1 && note <= 128) || isNoteRelease(action);
}

export class MonophonicNoteState {
    constructor() {
        this.order = new Uint8Array(128);
        this.velocity = new Uint8Array(128);
        this.count = 0;
    }
    apply(action) {
        if (action === NOTE_RESET) { this.count = 0; return NOTE_RESET; }
        const release = isNoteRelease(action);
        const note = release ? action - 130 : action % 512 - 1;
        let index = 0;
        while (index < this.count && this.order[index] !== note) index++;
        const wasActive = index === this.count - 1 && index < this.count;
        if (index < this.count) {
            for (let i = index; i < this.count - 1; i++) this.order[i] = this.order[i + 1];
            this.count--;
        }
        if (!release) {
            this.order[this.count++] = note;
            this.velocity[note] = Math.floor(action / 512);
            return NOTE_STRIKE + action;
        }
        if (!wasActive) return 0;
        if (!this.count) return NOTE_RESET;
        const fallback = this.order[this.count - 1];
        return fallback + 1 + 512 * this.velocity[fallback];
    }
}

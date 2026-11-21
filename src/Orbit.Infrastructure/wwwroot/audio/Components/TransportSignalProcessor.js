import { beatAtFrame } from "./TransportPhase.js";

// One audio-rate source per context, shared by all current/retiring Faust
// worklets. The absolute frame number prevents cumulative phase drift.
class TransportSignalProcessor extends AudioWorkletProcessor {
    constructor(options) {
        super();
        this.anchor = options.processorOptions.anchor;
        this.resetPending = true;
        this.port.onmessage = event => {
            const next = event.data;
            this.resetPending ||= next.resetGeneration !== this.anchor.resetGeneration || next.isPlaying !== this.anchor.isPlaying || next.sampleRate !== this.anchor.sampleRate;
            this.anchor = next;
        };
    }

    process(inputs, outputs) {
        const channel = outputs[0]?.[0];
        const reset = outputs[0]?.[1];
        if (!channel) return true;
        const anchor = this.anchor.sampleRate === sampleRate
            ? this.anchor : { ...this.anchor, isPlaying: false };
        for (let i = 0; i < channel.length; i++) {
            channel[i] = beatAtFrame(anchor, currentFrame + i, sampleRate);
            // Separate whole/fraction channels preserve fractional precision
            // across long loops; only the action adapter consumes these.
            const elapsed = anchor.isPlaying ? Math.max(0, currentFrame + i - anchor.frame) / sampleRate : 0;
            const beat = (anchor.absoluteBeat ?? anchor.beat) + elapsed * anchor.beatsPerMinute / 60;
            const seconds = (anchor.seconds ?? 0) + elapsed;
            if (outputs[0][2]) {
                const wholeBeat = Math.floor(beat), wholeSeconds = Math.floor(seconds);
                outputs[0][2][i] = wholeBeat;
                outputs[0][3][i] = beat - wholeBeat;
                outputs[0][4][i] = wholeSeconds;
                outputs[0][5][i] = seconds - wholeSeconds;
                outputs[0][6][i] = anchor.resetGeneration;
            }
        }
        if (reset) {
            reset.fill(0);
            if (this.resetPending) reset[0] = 1;
        }
        this.resetPending = false;
        return true;
    }
}

registerProcessor("orbit-transport-signal", TransportSignalProcessor);

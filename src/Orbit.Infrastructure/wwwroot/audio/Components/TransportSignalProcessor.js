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
        for (let i = 0; i < channel.length; i++)
            channel[i] = beatAtFrame(anchor, currentFrame + i, sampleRate);
        if (reset) {
            reset.fill(0);
            if (this.resetPending) reset[0] = 1;
        }
        this.resetPending = false;
        return true;
    }
}

registerProcessor("orbit-transport-signal", TransportSignalProcessor);

import { beatAtFrame } from "./TransportPhase.js";

// One audio-rate source per context, shared by all current/retiring Faust
// worklets. The absolute frame number prevents cumulative phase drift.
class TransportSignalProcessor extends AudioWorkletProcessor {
    constructor(options) {
        super();
        this.anchor = options.processorOptions.anchor;
        this.port.onmessage = event => { this.anchor = event.data; };
    }

    process(inputs, outputs) {
        const channel = outputs[0]?.[0];
        if (!channel) return true;
        const anchor = this.anchor.sampleRate === sampleRate
            ? this.anchor : { ...this.anchor, isPlaying: false };
        for (let i = 0; i < channel.length; i++)
            channel[i] = beatAtFrame(anchor, currentFrame + i, sampleRate);
        return true;
    }
}

registerProcessor("orbit-transport-signal", TransportSignalProcessor);

import { AudioVoice } from "./AudioVoice.js";
import { SignalCapture } from "./SignalCapture.js";

// Owns the master output graph. Each playback receives a separate, initially
// silent gain node so independent voices never alter each other's volume.
export class AudioOutput {
    #envelope;
    #masterGain;
    #context;

    constructor(envelope) {
        this.#envelope = envelope;
    }

    createVoice(context, sourceNode, channels = 1) {
        if (this.#context !== context) {
            this.dispose();
            this.#context = context;
            this.#masterGain = context.createGain();
            this.#masterGain.connect(context.destination);
        }

        const voiceGain = context.createGain();
        voiceGain.gain.value = 0;
        let capture;
        try {
            capture = new SignalCapture(context, sourceNode, channels);
            capture.connectMaster(voiceGain);
            voiceGain.connect(this.#masterGain);
        } catch (error) {
            capture?.dispose();
            voiceGain.disconnect();
            throw error;
        }

        return new AudioVoice(context, sourceNode, voiceGain, this.#envelope, capture);
    }

    dispose() {
        this.#masterGain?.disconnect();
        this.#masterGain = undefined;
        this.#context = undefined;
    }
}

import { RhythmicActionQueue } from './RhythmicActionQueue.js';

class RhythmicActionProcessor extends AudioWorkletProcessor {
    constructor(options) {
        super();
        this.phaseChannels = options.processorOptions.phaseChannels;
        this.queue = new RhythmicActionQueue(options.processorOptions.count);
        this.port.onmessage = ({ data }) => {
            if (data.clear) this.queue.clear();
            else this.queue.push(data.index, data.action);
        };
    }

    process(inputs, outputs) {
        const channels = outputs[0];
        if (!channels) return true;
        for (let i = 0; i < this.phaseChannels; i++) {
            const source = inputs[0]?.[i];
            if (source) channels[i].set(source);
            else channels[i].fill(0);
        }
        const consumed = this.queue.render(channels, this.phaseChannels);
        if (consumed.some(count => count > 0)) this.port.postMessage(consumed);
        return true;
    }
}

registerProcessor('orbit-rhythmic-actions', RhythmicActionProcessor);

import { RhythmicActionQueue } from './RhythmicActionQueue.js';
import { PitchPatternScheduler } from './PitchPatternScheduler.js';

class RhythmicActionProcessor extends AudioWorkletProcessor {
    constructor(options) {
        super();
        this.phaseChannels = options.processorOptions.phaseChannels;
        this.queue = new RhythmicActionQueue(options.processorOptions.inputs);
        this.patterns = options.processorOptions.inputs.map(input => input.pattern ? new PitchPatternScheduler(input.pattern) : null);
        this.port.onmessage = ({ data }) => {
            if (data.clear) this.queue.clear();
            else if (data.patterns) {
                for (const update of data.patterns) this.patterns[update.index]?.setPattern(update.pattern);
            } else this.queue.push(data.index, data.action);
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
        for (let index = 0; index < this.patterns.length; index++)
            this.patterns[index]?.render(channels[this.phaseChannels + index], inputs[0]);
        if (consumed.some(count => count > 0)) this.port.postMessage(consumed);
        return true;
    }
}

registerProcessor('orbit-rhythmic-actions', RhythmicActionProcessor);

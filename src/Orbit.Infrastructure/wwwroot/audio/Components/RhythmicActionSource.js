import { MAX_ACTIONS, MAX_RHYTHMIC_INPUTS } from './RhythmicActionQueue.js';

// One bounded, generation-owned source per actual Faust voice, not per UI node.
const modules = new WeakMap();

export class RhythmicActionSource {
    static async create(context, target, inputs, phaseChannels) {
        if (!inputs.length || inputs.length > MAX_RHYTHMIC_INPUTS || phaseChannels < 0 || phaseChannels > 2)
            throw new Error('Invalid rhythmic action input manifest.');
        let loading = modules.get(context);
        if (!loading) {
            loading = context.audioWorklet.addModule(new URL('./RhythmicActionProcessor.js', import.meta.url));
            modules.set(context, loading);
        }
        try { await loading; }
        catch (error) { if (modules.get(context) === loading) modules.delete(context); throw error; }
        const node = new AudioWorkletNode(context, 'orbit-rhythmic-actions', {
            numberOfInputs: phaseChannels ? 1 : 0,
            numberOfOutputs: 1, channelCount: Math.max(1, phaseChannels), channelCountMode: 'explicit', channelInterpretation: 'discrete',
            outputChannelCount: [phaseChannels + inputs.length], processorOptions: { count: inputs.length, phaseChannels }
        });
        try {
            target.channelInterpretation = 'discrete';
            node.connect(target);
            return new RhythmicActionSource(node, inputs);
        } catch (error) { node.disconnect(); node.port.close(); throw error; }
    }

    constructor(node, inputs) {
        this.node = node;
        this.inputs = inputs;
        this.pending = new Uint16Array(inputs.length);
        this.active = true;
        node.port.onmessage = ({ data }) => {
            if (this.active && data.length === this.pending.length)
                for (let i = 0; i < data.length; i++) this.pending[i] = Math.max(0, this.pending[i] - data[i]);
        };
    }

    send(index, action) {
        const input = this.inputs[index];
        if (!this.active || !Number.isInteger(index) || !input || (action !== 1 && (action !== 2 || !input.allowClose)) || this.pending[index] >= MAX_ACTIONS)
            return false;
        this.pending[index]++;
        this.node.port.postMessage({ index, action });
        return true;
    }

    deactivate() {
        if (!this.active) return;
        this.active = false;
        this.node.port.postMessage({ clear: true });
    }

    dispose() {
        this.deactivate();
        this.node.disconnect();
        this.node.port.close();
    }
}

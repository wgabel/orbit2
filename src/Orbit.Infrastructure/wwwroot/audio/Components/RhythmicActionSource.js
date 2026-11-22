import { MAX_ACTIONS, MAX_RHYTHMIC_INPUTS, validAction } from './RhythmicActionQueue.js';
import { validPitchPattern } from './PitchPatternScheduler.js';
import { isNoteRelease, NOTE_RESET } from './MonophonicNoteState.js';

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
        const longClock = inputs.some(input => input.pattern);
        if (inputs.some(input => input.pattern && (!input.isPitch || !validPitchPattern(input.pattern)))) throw new Error('Invalid pitch pattern.');
        const node = new AudioWorkletNode(context, 'orbit-rhythmic-actions', {
            numberOfInputs: phaseChannels || longClock ? 1 : 0,
            numberOfOutputs: 1, channelCount: longClock ? 7 : Math.max(1, phaseChannels), channelCountMode: 'explicit', channelInterpretation: 'discrete',
            outputChannelCount: [phaseChannels + inputs.length], processorOptions: { inputs, phaseChannels }
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
        inputs.forEach((input, index) => { if (input.isPitch && validAction(input, input.initialAction)) this.pending[index] = 1; });
        this.active = true;
        node.port.onmessage = ({ data }) => {
            if (this.active && data.length === this.pending.length)
                for (let i = 0; i < data.length; i++) this.pending[i] = Math.max(0, this.pending[i] - data[i]);
        };
    }

    send(index, action) {
        const input = this.inputs[index];
        if (!this.active || !Number.isInteger(index) || !validAction(input, action))
            return false;
        if (this.pending[index] >= MAX_ACTIONS) {
            if (!input.isNote || !isNoteRelease(action)) return false;
            // At most one extra panic word while acknowledgements are outstanding.
            if (this.pending[index] > MAX_ACTIONS) return true;
            action = NOTE_RESET;
        }
        this.pending[index]++;
        this.node.port.postMessage({ index, action });
        return true;
    }

    setPatterns(updates) {
        if (!this.active || !updates.every(update => this.inputs[update.index]?.pattern && validPitchPattern(update.pattern))) return false;
        this.node.port.postMessage({ patterns: updates });
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

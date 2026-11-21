import { SignalCapture } from "./Components/SignalCapture.js";
import { RhythmicActionSource } from "./Components/RhythmicActionSource.js";

// Coordinates logical playback lifetimes. A playback may have two voices
// during a crossfade; DSP factories, voices and envelopes own their mechanics.
export class AudioEngine {
    #contextManager;
    #nodeFactory;
    #output;
    #createMonitor;
    #transport;
    #createActions;
    #actions = new WeakMap();
    #active = new Map();
    #disposed = false;

    constructor(contextManager, nodeFactory, output, createMonitor, transport = null, createActions = RhythmicActionSource.create) {
        this.#contextManager = contextManager;
        this.#nodeFactory = nodeFactory;
        this.#output = output;
        this.#createMonitor = createMonitor;
        this.#transport = transport;
        this.#createActions = createActions;
    }

    async play(request) {
        this.#validate(request);
        if (this.#disposed) {
            throw new Error("The audio engine is disposed.");
        }
        if (this.#active.has(request.soundId)) {
            throw new Error("Playback already active: " + request.soundId);
        }

        let signalReady;
        const ready = new Promise(resolve => { signalReady = resolve; });
        const playback = {
            cancelled: false, current: undefined, voices: new Set(), ready,
            latestRevision: -1, replacing: false,
            controlNames: new Set(Object.keys(request.parameters ?? {})),
            pendingParameters: Object.create(null)
        };
        this.#active.set(request.soundId, playback);

        try {
            // Resume before any DSP download to preserve user activation.
            const context = await this.#contextManager.getRunningContext();
            playback.context = context;
            if (playback.cancelled) return;

            const node = await this.#createNode(context, request);

            if (playback.cancelled) {
                this.#destroyNode(node);
                return;
            }

            try {
                // Edits made during worker compilation are applied before the
                // voice attacks, with the last value winning for each control.
                if (Object.keys(playback.pendingParameters).length > 0)
                    this.#nodeFactory.setParameters(node, playback.pendingParameters);
            } catch (error) {
                this.#destroyNode(node);
                throw error;
            }

            playback.current = this.#startVoice(playback, node, request);
            signalReady(true);
            // A retiring voice ending is not the end of the logical playback.
            while (playback.current) {
                const voice = playback.current;
                await voice.finished;
                if (voice === playback.current) break;
            }
        } finally {
            playback.cancelled = true;
            signalReady(false);
            for (const instance of playback.voices) instance.voice.stop();
            await Promise.allSettled([...playback.voices].map(instance => instance.finished));
            this.#active.delete(request.soundId);
        }
    }

    // A newer revision makes an in-flight compilation stale.
    markRevision(soundId, revision) {
        const playback = this.#active.get(soundId);
        if (playback) playback.latestRevision = Math.max(playback.latestRevision, revision);
    }

    async replace(request, revision) {
        this.#validate(request);
        const playback = this.#active.get(request.soundId);
        if (!playback || playback.cancelled || revision < playback.latestRevision) return false;
        this.markRevision(request.soundId, revision);
        if (playback.replacing) throw new Error("A replacement is already pending.");
        playback.replacing = true;
        const isCurrent = () => this.#active.get(request.soundId) === playback &&
            !playback.cancelled && revision === playback.latestRevision;
        try {
            if (!await playback.ready || !isCurrent()) return false;
            let node;
            try { node = await this.#createNode(playback.context, request); }
            catch (error) { if (!isCurrent()) return false; throw error; }
            if (!isCurrent()) { this.#destroyNode(node); return false; }

            // Two complementary linear gain ramps share one audio-clock time.
            // Prepare and connect the new voice before touching the last good one.
            const old = playback.current;
            const transition = { startTime: playback.context.currentTime + 0.01, duration: 0.05 };
            playback.current = this.#startVoice(playback, node, request, transition);
            this.#actions.get(old.node)?.deactivate();
            // New reads belong exclusively to the accepted replacement.
            for (const id of playback.subscriptions ?? []) old.voice.releaseSignal?.(id);
            playback.subscriptions?.clear();
            playback.controlNames = new Set(Object.keys(request.parameters ?? {}));
            old.voice.stop(transition);
            await old.finished;
            // Once scheduled the crossfade is committed. Subsequent revisions
            // must use this voice as their starting point.
            return !playback.cancelled;
        } finally { playback.replacing = false; }
    }

    getPlaybackState(soundId) {
        const playback = this.#active.get(soundId);
        if (!playback) return "idle";
        if (playback.cancelled) return "stopping";
        return playback.current ? "playing" : "starting";
    }

    sendRhythmicAction(soundId, generation, index, action) {
        const playback = this.#active.get(soundId), current = playback?.current;
        if (!current || playback.cancelled || playback.replacing || current.generation !== generation || playback.context?.state !== 'running') return false;
        return this.#actions.get(current.node)?.send(index, action) ?? false;
    }

    // Only values of existing controls may change; this never recreates a node.
    setParameters(soundId, parameters) {
        const playback = this.#active.get(soundId);
        if (!playback || playback.cancelled) return false;
        if (!parameters || typeof parameters !== "object" || Array.isArray(parameters))
            throw new Error("Invalid audio parameter update.");
        for (const [name, value] of Object.entries(parameters)) {
            if (!playback.controlNames.has(name) || !Number.isFinite(value))
                throw new Error("Invalid audio parameter: " + name);
        }
        if (playback.current) this.#nodeFactory.setParameters(playback.current.node, parameters);
        else Object.assign(playback.pendingParameters, parameters);
        return true;
    }

    // A snapshot is read at display rate, independently of the sample clock.
    getOutputValues(soundId) {
        const playback = this.#active.get(soundId);
        return !playback || playback.cancelled ? null : playback.current?.monitor.getSnapshot() ?? null;
    }

    readSignal(soundId, generation, subscription, channel) {
        return this.#readSignal(soundId, generation, subscription, channel, false);
    }

    readSignalWindow(soundId, generation, subscription, channel) {
        return this.#readSignal(soundId, generation, subscription, channel, true);
    }

    #readSignal(soundId, generation, subscription, channel, fullWindow) {
        const playback = this.#active.get(soundId);
        const current = playback?.current;
        if (!current || playback.cancelled || current.generation !== generation) return null;
        playback.subscriptions ??= new Set();
        if (!playback.subscriptions.has(subscription) && playback.subscriptions.size >= SignalCapture.MaxLeases)
            return null;
        playback.subscriptions.add(subscription);
        return (fullWindow ? current.voice.readSignalWindow?.(subscription, channel, generation)
            : current.voice.readSignal?.(subscription, channel, generation)) ?? null;
    }

    releaseSignal(soundId, subscription) {
        const playback = this.#active.get(soundId);
        playback?.subscriptions?.delete(subscription);
        playback?.current?.voice.releaseSignal?.(subscription);
    }

    stop(soundId) {
        const playback = this.#active.get(soundId);
        if (playback) {
            playback.cancelled = true;
            for (const instance of playback.voices) this.#actions.get(instance.node)?.deactivate();
            for (const id of playback.subscriptions ?? []) playback.current?.voice.releaseSignal?.(id);
            playback.subscriptions?.clear();
            for (const instance of playback.voices) instance.voice.stop();
        }
    }

    async dispose() {
        this.#disposed = true;
        for (const soundId of this.#active.keys()) {
            this.stop(soundId);
        }

        this.#output.dispose();
        this.#transport?.dispose();
        await this.#contextManager.dispose();
    }

    async #createNode(context, request) {
        const node = await this.#nodeFactory.create(context, request.processor, request.parameters ?? {}, request.source);
        try {
            if (typeof node.getNumOutputs === "function" && node.getNumOutputs() !== (request.outputChannels ?? 1))
                throw new Error("Unexpected Faust output channel count.");
            const phaseChannels = request.transportInputChannels ?? (request.requiresTransport && node.numberOfInputs ? 1 : 0);
            const inputs = request.rhythmicInputs ?? [];
            if (typeof node.getNumInputs === 'function' && node.getNumInputs() !== phaseChannels + inputs.length)
                throw new Error('Unexpected Faust input channel count.');
            if (inputs.length) this.#actions.set(node, await this.#createActions(context, node, inputs, phaseChannels));
            if (request.requiresTransport) {
                if (!this.#transport) throw new Error("Transport signal source is unavailable.");
                await this.#transport.connect(context, node, this.#actions.get(node)?.node ?? node, phaseChannels);
            }
            return node;
        } catch (error) {
            this.#destroyNode(node);
            throw error;
        }
    }

    #destroyNode(node) {
        this.#transport?.disconnect(node);
        this.#disposeActions(node);
        try { node.disconnect(); } finally { node.destroy?.(); }
    }

    #disposeActions(node) {
        this.#actions.get(node)?.dispose();
        this.#actions.delete(node);
    }

    #startVoice(playback, node, request, transition) {
        let voice, monitor;
        try {
            voice = this.#output.createVoice(playback.context, node, request.outputChannels ?? 1);
            monitor = this.#createMonitor(node);
            const instance = { node, voice, monitor, generation: request.generation };
            const lifetime = voice.play(request, transition);
            instance.finished = Promise.resolve(lifetime).finally(() => {
                playback.voices.delete(instance);
                this.#transport?.disconnect(node);
                this.#disposeActions(node);
                try { monitor.dispose(); } finally { voice.dispose(); }
            });
            // A retiring voice may finish before the main loop observes it.
            instance.finished.catch(() => {});
            playback.voices.add(instance);
            return instance;
        } catch (error) {
            try { monitor?.dispose(); }
            finally {
                if (voice) {
                    this.#transport?.disconnect(node);
                    this.#disposeActions(node);
                    voice.dispose();
                } else this.#destroyNode(node);
            }
            throw error;
        }
    }

    #validate(request) {
        if (!request || typeof request.soundId !== "string" || !request.soundId ||
            typeof request.processor !== "string" || !request.processor ||
            !Number.isFinite(request.volume) || request.volume < 0 || request.volume > 1 ||
            (request.outputChannels != null && (!Number.isInteger(request.outputChannels) ||
                request.outputChannels < 1 || request.outputChannels > SignalCapture.MaxChannels)) ||
            (request.source != null && (typeof request.source !== "string" || !request.source.trim())) ||
            (request.transportInputChannels != null && (!Number.isInteger(request.transportInputChannels) || request.transportInputChannels < 0 || request.transportInputChannels > 2)) ||
            (request.rhythmicInputs != null && (!Array.isArray(request.rhythmicInputs) || request.rhythmicInputs.length > 30)) ||
            (request.parameters != null &&
                (typeof request.parameters !== "object" || Array.isArray(request.parameters)))) {
            throw new Error("Invalid audio playback request.");
        }
    }
}

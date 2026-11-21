// A voice owns its channel router and bounded analysis leases. Only channel
// zero can feed the audible gain; other channels terminate at analyzers.
export class SignalCapture {
    #context;
    #source;
    #splitter;
    #channels;
    #leases = new Map();
    #analyzers = new Map();
    #routing;
    #assignment = 0;
    static MaxChannels = 9;
    static MaxLeases = 32;
    static WindowSize = 1024;
    static AnalysisWindowSize = 8192;

    constructor(context, source, channels, routing = null) {
        this.#context = context;
        this.#source = source;
        this.#channels = channels;
        this.#routing = routing;
        if (channels > 1) {
            this.#splitter = context.createChannelSplitter(channels);
            try { source.connect(this.#splitter); }
            catch (error) { this.#splitter.disconnect(); throw error; }
        }
    }

    connectMaster(destination) {
        if (this.#splitter) this.#splitter.connect(destination, 0, 0);
        else this.#source.connect(destination);
    }

    read(subscription, channel, generation) {
        const window = this.readWindow(subscription, channel, generation);
        return window && { ...window, samples: Array.from(window.samples.subarray(-SignalCapture.WindowSize)) };
    }

    // Browser-only window; consumers read synchronously and never retain this
    // reused buffer across capture ticks. The diagnostic interop remains small.
    readWindow(subscription, channel, generation) {
        return this.observe(subscription, channel, generation).frame;
    }

    observe(subscription, selector, generation) {
        const pending = { availability: 2, frame: null };
        const capacity = reason => ({ availability: 4, frame: null, reason, ...this.getDiagnostics() });
        if (!Number.isInteger(selector) || selector < 0 || selector > (this.#routing?.selectorCount ?? this.#channels - 1)) {
            this.release(subscription);
            return { availability: 1, frame: null };
        }
        if (this.#leases.has(subscription) && this.#analyzers.get(this.#leases.get(subscription)).selector !== selector)
            this.release(subscription);
        if (!this.#leases.has(subscription)) {
            if (this.#leases.size >= SignalCapture.MaxLeases) return capacity('leases');
            let channel = selector;
            if (this.#routing && selector !== 0) {
                channel = [...this.#analyzers].find(([, analyzer]) => analyzer.selector === selector)?.[0];
                if (channel === undefined) {
                    for (let slot = 1; slot < this.#channels; slot++) {
                        if (!this.#analyzers.has(slot)) { channel = slot; break; }
                    }
                    if (channel === undefined) return capacity('slots');
                }
            }
            if (!this.#analyzers.has(channel)) {
                let assignment = 0;
                if (this.#routing && channel !== 0) {
                    assignment = ++this.#assignment * (this.#routing.selectorCount + 1) + selector;
                    if (assignment > 16777215) return { availability: 5, frame: null };
                    this.#routing.write(channel, assignment);
                }
                const node = this.#context.createAnalyser();
                node.fftSize = SignalCapture.AnalysisWindowSize;
                const source = this.#splitter ?? this.#source;
                source.connect(node, channel, 0);
                this.#analyzers.set(channel, {
                    node, samples: new Float32Array(node.fftSize), count: 0,
                    selector, assignment,
                    readyTime: assignment ? Infinity : this.#context.currentTime + node.fftSize / this.#context.sampleRate,
                    lastTime: -1
                });
            }
            this.#leases.set(subscription, channel);
            this.#analyzers.get(channel).count++;
        }
        const channel = this.#leases.get(subscription);
        const analyzer = this.#analyzers.get(channel);
        if (analyzer.assignment && this.#routing.ack(channel) !== analyzer.assignment) {
            analyzer.readyTime = Infinity;
            analyzer.lastTime = -1;
            return pending;
        }
        if (analyzer.readyTime === Infinity)
            analyzer.readyTime = this.#context.currentTime + analyzer.node.fftSize / this.#context.sampleRate;
        if (this.#context.state !== "running" || this.#context.currentTime < analyzer.readyTime) return pending;
        if (analyzer.lastTime !== this.#context.currentTime) {
            analyzer.node.getFloatTimeDomainData(analyzer.samples);
            analyzer.lastTime = this.#context.currentTime;
        }
        return { availability: 3, frame: { generation, sampleRate: this.#context.sampleRate, time: this.#context.currentTime,
            samples: analyzer.samples } };
    }

    getDiagnostics() {
        const occupied = this.#analyzers.size - Number(this.#analyzers.has(0));
        return { occupiedSlots: occupied, freeSlots: this.#channels - 1 - occupied, activeLeases: this.#leases.size };
    }

    release(subscription) {
        if (!this.#leases.has(subscription)) return;
        const channel = this.#leases.get(subscription);
        this.#leases.delete(subscription);
        const analyzer = this.#analyzers.get(channel);
        if (--analyzer.count === 0) {
            (this.#splitter ?? this.#source).disconnect(analyzer.node, channel, 0);
            analyzer.node.disconnect();
            this.#analyzers.delete(channel);
            if (this.#routing && channel !== 0) this.#routing.write(channel, 0);
        }
    }

    dispose() {
        for (const subscription of this.#leases.keys()) this.release(subscription);
        this.#splitter?.disconnect();
    }
}

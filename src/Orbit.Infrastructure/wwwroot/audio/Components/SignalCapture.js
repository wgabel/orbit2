// A voice owns its channel router and bounded analysis leases. Only channel
// zero can feed the audible gain; other channels terminate at analyzers.
export class SignalCapture {
    #context;
    #source;
    #splitter;
    #channels;
    #leases = new Map();
    #analyzers = new Map();
    static MaxChannels = 9;
    static MaxLeases = 32;
    static WindowSize = 1024;
    static AnalysisWindowSize = 8192;

    constructor(context, source, channels) {
        this.#context = context;
        this.#source = source;
        this.#channels = channels;
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
        if (!Number.isInteger(channel) || channel < 0 || channel >= this.#channels) return null;
        if (this.#leases.has(subscription) && this.#leases.get(subscription) !== channel)
            this.release(subscription);
        if (!this.#leases.has(subscription)) {
            if (this.#leases.size >= SignalCapture.MaxLeases) return null;
            if (!this.#analyzers.has(channel)) {
                const node = this.#context.createAnalyser();
                node.fftSize = SignalCapture.AnalysisWindowSize;
                const source = this.#splitter ?? this.#source;
                source.connect(node, channel, 0);
                this.#analyzers.set(channel, {
                    node, samples: new Float32Array(node.fftSize), count: 0,
                    readyTime: this.#context.currentTime + node.fftSize / this.#context.sampleRate,
                    lastTime: -1
                });
            }
            this.#leases.set(subscription, channel);
            this.#analyzers.get(channel).count++;
        }
        const analyzer = this.#analyzers.get(channel);
        if (this.#context.state !== "running" || this.#context.currentTime < analyzer.readyTime) return null;
        if (analyzer.lastTime !== this.#context.currentTime) {
            analyzer.node.getFloatTimeDomainData(analyzer.samples);
            analyzer.lastTime = this.#context.currentTime;
        }
        return { generation, sampleRate: this.#context.sampleRate, time: this.#context.currentTime,
            samples: analyzer.samples };
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
        }
    }

    dispose() {
        for (const subscription of this.#leases.keys()) this.release(subscription);
        this.#splitter?.disconnect();
    }
}

import { measureLevels, toDecibels, scopeWindow, MinMaxHistory } from "./scopeAnalysis.js";
import { SpectrumAnalyzer } from "./spectrumAnalysis.js";
import { drawSpectrum } from "./spectrumPlot.js";
import { prepareSignalCanvas } from "./signalPlot.js";

// A browser-owned renderer: .NET resolves the accepted signal binding at a
// sparse cadence; no sample arrays or animation frames cross JS interop.
export async function mountScope(root, audioPath, resolver, lease, settings) {
    const audio = await import(new URL(audioPath, document.baseURI).href);
    return new ScopeView(root, audio, resolver, lease, settings);
}

class ScopeView {
    #root; #audio; #resolver; #lease; #canvas; #timebase; #trigger; #mode;
    #waveformControls; #spectrumControls; #fftSize; #fftWindow; #fftSmoothing;
    #spectrum = null; #spectrumKey = null;
    #status; #peak; #rms; #peakMeter; #rmsMeter; #scale; #history = new MinMaxHistory();
    #binding = null; #lastBinding = -Infinity; #lastDraw = -Infinity;
    #animation = 0; #disposed = false; #visible = false; #observer;
    #resolving = false;
    #lastClear = null;

    constructor(root, audio, resolver, lease, settings) {
        this.#root = root; this.#audio = audio; this.#resolver = resolver; this.#lease = lease;
        this.#canvas = root.querySelector("canvas");
        this.#mode = root.querySelector("[data-analysis-mode]");
        this.#waveformControls = root.querySelector("[data-waveform-controls]");
        this.#spectrumControls = root.querySelector("[data-spectrum-controls]");
        this.#fftSize = root.querySelector("[data-fft-size]");
        this.#fftWindow = root.querySelector("[data-fft-window]");
        this.#fftSmoothing = root.querySelector("[data-fft-smoothing]");
        this.#timebase = root.querySelector("[data-timebase]");
        this.#trigger = root.querySelector("[data-trigger]");
        this.#status = root.querySelector("[data-scope-status]");
        this.#peak = root.querySelector("[data-peak]");
        this.#rms = root.querySelector("[data-rms]");
        this.#peakMeter = root.querySelector("[data-peak-meter]");
        this.#rmsMeter = root.querySelector("[data-rms-meter]");
        this.#scale = root.querySelector("[data-scale]");
        // Restore the layout configuration before the first analysis frame.
        this.#mode.value = settings.mode;
        this.#timebase.value = settings.timebase;
        this.#trigger.value = settings.trigger;
        this.#fftSize.value = settings.fftSize;
        this.#fftWindow.value = settings.window;
        this.#fftSmoothing.value = settings.smoothing;
        this.#root.addEventListener("change", this.#settingsChanged);
        this.#modeChanged();
        this.#observer = new IntersectionObserver(entries => {
            this.#visible = entries[0].isIntersecting;
            this.#visibilityChanged();
        });
        this.#observer.observe(root);
        document.addEventListener("visibilitychange", this.#visibilityChanged);
        this.#visibilityChanged();
    }

    #settingsChanged = event => {
        if (event.target === this.#mode) this.#modeChanged();
        else if (![this.#timebase, this.#trigger, this.#fftSize, this.#fftWindow, this.#fftSmoothing].includes(event.target)) return;
        // One coherent configuration snapshot, including rapid consecutive changes.
        this.#resolver.invokeMethodAsync("ChangeSettings", this.#mode.value, this.#timebase.value,
            this.#trigger.value, this.#fftSize.value, this.#fftWindow.value, this.#fftSmoothing.value)
            .catch(error => { if (!this.#disposed) console.error("Scope settings update failed", error); });
    };

    #modeChanged = () => {
        const spectrum = this.#mode.value === "spectrum";
        const level = this.#mode.value === "level";
        this.#root.dataset.viewMode = this.#mode.value;
        this.#waveformControls.hidden = spectrum || level;
        this.#spectrumControls.hidden = !spectrum;
        this.#canvas.hidden = level;
        this.#canvas.setAttribute("aria-label", spectrum ? "Live signal spectrum" : "Live signal waveform");
        this.#history.clear();
        this.#resetSpectrum();
        this.#lastClear = null;
        this.#clear("Waiting for signal");
    };

    #resetSpectrum() {
        this.#spectrum = null;
        this.#spectrumKey = null;
    }

    #active() { return !this.#disposed && this.#visible && !document.hidden && this.#root.isConnected; }
    #visibilityChanged = () => {
        if (!this.#active()) {
            cancelAnimationFrame(this.#animation);
            this.#animation = 0;
            this.#release();
            this.#history.clear();
            this.#clear("Paused");
        } else if (!this.#animation) {
            this.#lastBinding = -Infinity;
            this.#animation = requestAnimationFrame(this.#tick);
        }
    };

    #release() {
        if (this.#binding?.soundId) this.#audio.releaseSignal(this.#binding.soundId, this.#lease, this.#binding.generation);
        this.#binding = null;
    }

    async #resolve() {
        this.#resolving = true;
        try {
            const next = await this.#resolver.invokeMethodAsync("ResolveBinding");
            if (!this.#active()) return;
            const previous = this.#binding;
            if (!previous || next.generation !== previous.generation || next.channel !== previous.channel ||
                next.soundId !== previous.soundId || next.availability !== previous.availability) {
                this.#release();
                this.#history.clear();
                this.#clear("Waiting for signal");
                this.#binding = next;
            }
        } catch {
            if (!this.#disposed) { this.#release(); this.#history.clear(); this.#clear("Signal unavailable"); }
        } finally { this.#resolving = false; }
    }

    #tick = timestamp => {
        this.#animation = 0;
        if (!this.#active()) { this.#visibilityChanged(); return; }
        this.#animation = requestAnimationFrame(this.#tick);
        if (timestamp - this.#lastBinding >= 250 && !this.#resolving) {
            this.#lastBinding = timestamp;
            void this.#resolve();
        }
        if (timestamp - this.#lastDraw < 1000 / 30) return;
        this.#lastDraw = timestamp;
        const binding = this.#binding;
        if (!binding || binding.availability !== 3) {
            this.#clear(binding?.availability === 0 ? "Playback stopped" :
                binding?.availability === 1 ? "Signal unrouted or inactive" :
                binding?.availability === 5 ? "Capture unsupported" : "Waiting for signal");
            return;
        }
        let result;
        try { result = this.#audio.readObservationWindow(binding.soundId, binding.generation, this.#lease, binding.channel); }
        catch { this.#clear("Capture unavailable"); return; }
        const frame = result.frame;
        if (!frame || frame.generation !== binding.generation) {
            this.#history.clear();
            this.#clear(result.availability === 4 ? (result.reason === 'slots' ? `Tap capacity reached · ${result.occupiedSlots}/${result.occupiedSlots + result.freeSlots} occupied` : 'Capture view limit reached') :
                result.availability === 5 ? "Capture unsupported" : "Waiting for signal");
            return;
        }
        const levels = measureLevels(frame.samples, Math.max(0, frame.samples.length - Math.round(frame.sampleRate * 0.02)));
        const format = value => value > 0 ? `${toDecibels(value).toFixed(1)} dBFS` : "−∞ dBFS";
        this.#peak.textContent = format(levels.peak);
        this.#rms.textContent = format(levels.rms);
        this.#peakMeter.value = Math.max(-60, Math.min(0, toDecibels(levels.peak)));
        this.#rmsMeter.value = Math.max(-60, Math.min(0, toDecibels(levels.rms)));
        this.#peak.classList.toggle("scope-over", levels.peak >= 1);
        this.#lastClear = null;
        if (this.#mode.value === "level") {
            this.#status.textContent = "Live · level meter";
            this.#scale.textContent = "20 ms";
            return;
        }
        if (this.#mode.value === "spectrum") {
            this.#renderSpectrum(frame);
            return;
        }
        this.#history.append(frame.samples, frame.sampleRate, frame.time);
        const cycle = this.#timebase.value === "cycle";
        const configured = Number(this.#root.dataset.cycleSeconds);
        const seconds = cycle ? (configured > 0 && Number.isFinite(configured) ? configured : 0.02) : Number(this.#timebase.value);
        const slow = seconds >= 1 || (cycle && seconds * frame.sampleRate > frame.samples.length);
        this.#trigger.disabled = slow;
        const window = scopeWindow(frame.samples, frame.sampleRate, slow ? 0.02 : seconds, this.#trigger.value);
        this.#status.textContent = slow ? "History · 10 ms min/max" :
            this.#trigger.value === "free" ? "Live · free running" : window.triggered ? "Live · triggered" : "Live · waiting for crossing";
        let maximum = slow ? 1 : Math.max(1, measureLevels(window.samples).peak);
        if (slow) this.#history.visit(seconds, (_, min, max) => { maximum = Math.max(maximum, Math.abs(min), Math.abs(max)); });
        const scale = 2 ** Math.ceil(Math.log2(maximum));
        this.#scale.textContent = `±${scale} · ${slow ? Number(seconds.toPrecision(4)) + " s" : (window.seconds * 1000).toFixed(2) + " ms"}${cycle ? " · configured cycle" : ""}`;
        this.#scale.title = this.#scale.textContent;
        this.#draw(scale, slow, seconds, window.samples);
    };

    #renderSpectrum(frame) {
        const size = Number(this.#fftSize.value);
        const window = this.#fftWindow.value;
        const smoothing = Number(this.#fftSmoothing.value);
        const key = `${size}:${window}:${smoothing}`;
        if (this.#spectrumKey !== key) {
            this.#spectrum = new SpectrumAnalyzer(size, window, smoothing);
            this.#spectrumKey = key;
        }
        const spectrum = this.#spectrum.analyze(frame.samples, frame.sampleRate, frame.time);
        this.#status.textContent = spectrum.peakDecibels >= -100
            ? `Spectrum · peak bin ${(spectrum.peakBin * spectrum.binWidth).toFixed(1)} Hz · ${spectrum.peakDecibels.toFixed(1)} dBFS`
            : "Spectrum · no peak above −100 dBFS";
        this.#scale.textContent = `${spectrum.binWidth.toFixed(2)} Hz/bin`;
        drawSpectrum(this.#canvas, this.#root, spectrum);
    }

    #prepare() {
        const { ctx, width, height, colors } = prepareSignalCanvas(this.#canvas, this.#root);
        ctx.strokeStyle = colors.grid;
        ctx.beginPath();
        for (let i = 1; i < 4; i++) {
            ctx.moveTo(width * i / 4, 0); ctx.lineTo(width * i / 4, height);
            ctx.moveTo(0, height * i / 4); ctx.lineTo(width, height * i / 4);
        }
        ctx.stroke();
        ctx.strokeStyle = colors.trace;
        return { ctx, width, height };
    }

    #draw(scale, slow, seconds, samples) {
        const { ctx, width, height } = this.#prepare();
        const y = sample => height * (0.5 - sample / (2 * scale));
        ctx.beginPath();
        if (slow) {
            this.#history.visit(seconds, (position, min, max) => {
                const x = position * (width - 1);
                ctx.moveTo(x, y(min)); ctx.lineTo(x, y(max));
                // Draw constant-valued bins too.
                if (min === max) ctx.lineTo(x + 1, y(max));
            });
        } else if (samples.length <= width) {
            for (let i = 0; i < samples.length; i++) {
                const x = i * (width - 1) / (samples.length - 1);
                if (i === 0) ctx.moveTo(x, y(samples[i])); else ctx.lineTo(x, y(samples[i]));
            }
        } else {
            // Min/max per pixel preserves peaks at short timebases too.
            for (let x = 0; x < width; x++) {
                let min = Infinity, max = -Infinity;
                for (let i = Math.floor(x * samples.length / width); i < Math.floor((x + 1) * samples.length / width); i++) {
                    min = Math.min(min, samples[i]); max = Math.max(max, samples[i]);
                }
                ctx.moveTo(x, y(min)); ctx.lineTo(x, y(max));
            }
        }
        ctx.stroke();
    }

    #clear(message) {
        this.#resetSpectrum();
        if (this.#lastClear === message) return;
        this.#lastClear = message;
        this.#status.textContent = message;
        this.#peak.textContent = this.#rms.textContent = "—";
        this.#peakMeter.value = this.#rmsMeter.value = -60;
        this.#scale.textContent = "—";
        this.#peak.classList.remove("scope-over");
        if (this.#mode.value === "spectrum") drawSpectrum(this.#canvas, this.#root, null);
        else if (this.#mode.value !== "level") this.#prepare();
    }

    dispose() {
        this.#disposed = true;
        cancelAnimationFrame(this.#animation);
        this.#observer.disconnect();
        this.#root.removeEventListener("change", this.#settingsChanged);
        document.removeEventListener("visibilitychange", this.#visibilityChanged);
        this.#release();
    }
}

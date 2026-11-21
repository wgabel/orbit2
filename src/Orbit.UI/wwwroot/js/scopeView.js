import { measureLevels, toDecibels, scopeWindow, MinMaxHistory } from "./scopeAnalysis.js";

// A browser-owned renderer: .NET resolves the accepted signal binding at a
// sparse cadence; no sample arrays or animation frames cross JS interop.
export async function mountScope(root, audioPath, resolver, lease) {
    const audio = await import(new URL(audioPath, document.baseURI).href);
    return new ScopeView(root, audio, resolver, lease);
}

class ScopeView {
    #root; #audio; #resolver; #lease; #canvas; #context; #timebase; #trigger;
    #status; #peak; #rms; #peakMeter; #rmsMeter; #scale; #history = new MinMaxHistory();
    #binding = null; #lastBinding = -Infinity; #lastDraw = -Infinity;
    #animation = 0; #disposed = false; #visible = true; #observer;
    #resolving = false;
    #lastClear = null;

    constructor(root, audio, resolver, lease) {
        this.#root = root; this.#audio = audio; this.#resolver = resolver; this.#lease = lease;
        this.#canvas = root.querySelector("canvas");
        this.#context = this.#canvas.getContext("2d");
        this.#timebase = root.querySelector("[data-timebase]");
        this.#trigger = root.querySelector("[data-trigger]");
        this.#status = root.querySelector("[data-scope-status]");
        this.#peak = root.querySelector("[data-peak]");
        this.#rms = root.querySelector("[data-rms]");
        this.#peakMeter = root.querySelector("[data-peak-meter]");
        this.#rmsMeter = root.querySelector("[data-rms-meter]");
        this.#scale = root.querySelector("[data-scale]");
        this.#observer = new IntersectionObserver(entries => {
            this.#visible = entries[0].isIntersecting;
            this.#visibilityChanged();
        });
        this.#observer.observe(root);
        document.addEventListener("visibilitychange", this.#visibilityChanged);
        this.#visibilityChanged();
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
        if (this.#binding?.soundId) this.#audio.releaseSignal(this.#binding.soundId, this.#lease);
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
                binding?.availability === 1 ? "Tap unavailable" : "Waiting for signal");
            return;
        }
        let frame;
        try { frame = this.#audio.readSignalWindow(binding.soundId, binding.generation, this.#lease, binding.channel); }
        catch { this.#clear("Capture unavailable"); return; }
        if (!frame || frame.generation !== binding.generation) {
            this.#history.clear();
            this.#clear("Waiting for signal");
            return;
        }
        this.#history.append(frame.samples, frame.sampleRate, frame.time);
        const seconds = Number(this.#timebase.value);
        const slow = seconds >= 1;
        this.#trigger.disabled = slow;
        const window = scopeWindow(frame.samples, frame.sampleRate, slow ? 0.02 : seconds, this.#trigger.value);
        const levels = measureLevels(frame.samples, Math.max(0, frame.samples.length - Math.round(frame.sampleRate * 0.02)));
        const format = value => value > 0 ? `${toDecibels(value).toFixed(1)} dBFS` : "−∞ dBFS";
        this.#peak.textContent = format(levels.peak);
        this.#rms.textContent = format(levels.rms);
        this.#peakMeter.value = Math.max(-60, Math.min(0, toDecibels(levels.peak)));
        this.#rmsMeter.value = Math.max(-60, Math.min(0, toDecibels(levels.rms)));
        this.#peak.classList.toggle("scope-over", levels.peak >= 1);
        this.#status.textContent = slow ? "History · 10 ms min/max" :
            this.#trigger.value === "free" ? "Live · free running" : window.triggered ? "Live · triggered" : "Live · waiting for crossing";
        let maximum = slow ? 1 : Math.max(1, measureLevels(window.samples).peak);
        if (slow) this.#history.visit(seconds, (_, min, max) => { maximum = Math.max(maximum, Math.abs(min), Math.abs(max)); });
        const scale = 2 ** Math.ceil(Math.log2(maximum));
        this.#scale.textContent = `±${scale} · ${slow ? seconds + " s" : (window.seconds * 1000).toFixed(1) + " ms"}`;
        this.#lastClear = null;
        this.#draw(scale, slow, seconds, window.samples);
    };

    #prepare() {
        const ratio = Math.min(2, window.devicePixelRatio || 1);
        const width = Math.max(1, Math.round(this.#canvas.clientWidth * ratio));
        const height = Math.max(1, Math.round(this.#canvas.clientHeight * ratio));
        if (this.#canvas.width !== width) this.#canvas.width = width;
        if (this.#canvas.height !== height) this.#canvas.height = height;
        const style = getComputedStyle(this.#root);
        const ctx = this.#context;
        ctx.fillStyle = style.getPropertyValue("--semantic-color-canvas").trim();
        ctx.fillRect(0, 0, width, height);
        ctx.strokeStyle = style.getPropertyValue("--semantic-color-border").trim();
        ctx.lineWidth = ratio;
        ctx.beginPath();
        for (let i = 1; i < 4; i++) {
            ctx.moveTo(width * i / 4, 0); ctx.lineTo(width * i / 4, height);
            ctx.moveTo(0, height * i / 4); ctx.lineTo(width, height * i / 4);
        }
        ctx.stroke();
        ctx.strokeStyle = style.getPropertyValue("--semantic-color-modulation").trim();
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
        if (this.#lastClear === message) return;
        this.#lastClear = message;
        this.#status.textContent = message;
        this.#peak.textContent = this.#rms.textContent = "—";
        this.#peakMeter.value = this.#rmsMeter.value = -60;
        this.#scale.textContent = "—";
        this.#peak.classList.remove("scope-over");
        this.#prepare();
    }

    dispose() {
        this.#disposed = true;
        cancelAnimationFrame(this.#animation);
        this.#observer.disconnect();
        document.removeEventListener("visibilitychange", this.#visibilityChanged);
        this.#release();
    }
}

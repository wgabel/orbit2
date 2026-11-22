// Foreground-only, bounded UI diagnostics. No audio, DOM layout reads or interop per frame.
const WINDOW_MS = 5000;
const MAX_FRAMES = 4096;
const MAX_ENTRIES = 512;
const MAX_HISTORY = 120;

class Ring {
    constructor(capacity) { this.items = new Array(capacity); this.clear(); }
    clear() { this.items.fill(undefined); this.next = 0; this.length = 0; }
    push(value) {
        this.items[this.next] = value;
        this.next = (this.next + 1) % this.items.length;
        this.length = Math.min(this.length + 1, this.items.length);
    }
    values() {
        return Array.from({ length: this.length }, (_, i) =>
            this.items[(this.next - this.length + i + this.items.length) % this.items.length]);
    }
}

function percentile(sorted, fraction) {
    return sorted.length ? sorted[Math.ceil(sorted.length * fraction) - 1] : null;
}

function eventSummary(ring, cutoff, now) {
    const entries = ring.values().filter(e => e.start + e.duration > cutoff && e.start + e.duration <= now);
    return {
        count: entries.length,
        totalDurationMs: entries.reduce((sum, e) => sum + e.duration, 0),
        maxDurationMs: entries.length ? Math.max(...entries.map(e => e.duration)) : null,
        over50Ms: entries.reduce((sum, e) => sum + Math.max(0, e.duration - 50), 0)
    };
}

export function createEditorPerformanceMonitor(environment = globalThis) {
    const document = environment.document;
    const now = () => environment.performance.now();
    const hasClock = typeof environment.performance?.now === "function";
    const hasFrames = hasClock && typeof environment.requestAnimationFrame === "function"
        && typeof environment.cancelAnimationFrame === "function";
    const frames = new Ring(MAX_FRAMES);
    const history = new Ring(MAX_HISTORY);
    const entryTypes = ["longtask", "long-animation-frame"];
    const entries = entryTypes.map(() => new Ring(MAX_ENTRIES));
    let observers = [];
    let supported = [false, false];
    let epoch = 0, generation = 0, frameId = null, previous = null;
    let lastHistory = -Infinity, visible = false, disposed = false;

    function stop() {
        generation++;
        visible = false;
        if (frameId !== null) environment.cancelAnimationFrame(frameId);
        frameId = null;
        for (const observer of observers) observer.disconnect();
        observers = [];
        supported = [false, false];
        previous = null;
        frames.clear();
        entries.forEach(ring => ring.clear());
        history.clear();
        lastHistory = -Infinity;
    }

    function start() {
        if (disposed || visible || !hasClock || document?.hidden) return;
        visible = true;
        epoch = now();
        const current = ++generation;
        if (hasFrames) {
            const onFrame = timestamp => {
                if (!visible || disposed || current !== generation) return;
                if (Number.isFinite(timestamp) && timestamp >= epoch) {
                    if (previous !== null && timestamp > previous)
                        frames.push({ start: previous, end: timestamp });
                    previous = timestamp;
                }
                frameId = environment.requestAnimationFrame(onFrame);
            };
            frameId = environment.requestAnimationFrame(onFrame);
        }
        const Observer = environment.PerformanceObserver;
        for (let i = 0; i < entryTypes.length; i++) {
            const type = entryTypes[i];
            if (!Observer?.supportedEntryTypes?.includes(type)) continue;
            let observer;
            try {
                observer = new Observer(list => {
                    if (!visible || disposed || current !== generation) return;
                    for (const entry of list.getEntries()) {
                        if (entry.entryType === type && Number.isFinite(entry.startTime) &&
                            entry.startTime >= epoch && Number.isFinite(entry.duration) && entry.duration >= 0)
                            entries[i].push({ start: entry.startTime, duration: entry.duration });
                    }
                });
                observer.observe({ type, buffered: false });
                observers.push(observer);
                supported[i] = true;
            } catch {
                observer?.disconnect();
            }
        }
    }

    function onVisibility() {
        if (document?.hidden) stop();
        else start();
    }

    document?.addEventListener("visibilitychange", onVisibility);
    start();

    return {
        read() {
            if (disposed || !hasClock) return { state: "unavailable" };
            // Guard also covers a visibility transition before its event is delivered.
            if (document?.hidden) {
                if (visible) stop();
                return { state: "paused" };
            }
            if (!visible) start();
            const time = now();
            const cutoff = Math.max(epoch, time - WINDOW_MS);
            // Include completed intervals by end time: a stall longer than the whole window must not vanish.
            const recent = frames.values().filter(frame => frame.end > cutoff && frame.end <= time);
            const gaps = recent.map(frame => frame.end - frame.start).sort((a, b) => a - b);
            const duration = gaps.reduce((sum, gap) => sum + gap, 0);
            const fps = duration > 0 ? gaps.length * 1000 / duration : null;
            const p95 = percentile(gaps, 0.95);
            if (fps !== null && time - lastHistory >= 1000) {
                history.push({ fps, p95 });
                lastHistory = time;
            }
            const previousSamples = history.values();
            return {
                state: !hasFrames ? "unavailable" : fps === null ? "collecting" : "active",
                windowSeconds: Math.max(0, time - cutoff) / 1000,
                frameIntervals: gaps.length,
                fps,
                medianGapMs: percentile(gaps, 0.5),
                p95GapMs: p95,
                maxGapMs: gaps.length ? gaps.at(-1) : null,
                longTasks: supported[0] ? eventSummary(entries[0], cutoff, time) : null,
                longAnimationFrames: supported[1] ? eventSummary(entries[1], cutoff, time) : null,
                historySamples: previousSamples.length,
                historyLowestFps: previousSamples.length ? Math.min(...previousSamples.map(s => s.fps)) : null,
                historyWorstP95Ms: previousSamples.length ? Math.max(...previousSamples.map(s => s.p95)) : null
            };
        },
        dispose() {
            if (disposed) return;
            disposed = true;
            document?.removeEventListener("visibilitychange", onVisibility);
            stop();
        }
    };
}

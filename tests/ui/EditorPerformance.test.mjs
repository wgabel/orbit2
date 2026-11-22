import test from "node:test";
import assert from "node:assert/strict";
import { createEditorPerformanceMonitor } from "../../src/Orbit.UI/wwwroot/js/editorPerformance.js";

function fake(types = ["longtask", "long-animation-frame"]) {
    let time = 0, id = 0;
    const pending = new Map(), listeners = new Set(), observers = [];
    const document = {
        hidden: false,
        addEventListener(type, handler) { assert.equal(type, "visibilitychange"); listeners.add(handler); },
        removeEventListener(type, handler) { listeners.delete(handler); }
    };
    class PerformanceObserver {
        static supportedEntryTypes = types;
        constructor(callback) { this.callback = callback; observers.push(this); }
        observe({ type, buffered }) { assert.equal(buffered, false); this.type = type; this.connected = true; }
        disconnect() { this.connected = false; }
    }
    const environment = {
        document, performance: { now: () => time }, PerformanceObserver,
        requestAnimationFrame(fn) { pending.set(++id, fn); return id; },
        cancelAnimationFrame(key) { pending.delete(key); }
    };
    return {
        environment, observers, listeners, pending,
        advance(ms) { time += ms; },
        frame(ms) {
            time += ms;
            const callbacks = [...pending.values()];
            pending.clear();
            for (const callback of callbacks) callback(time);
        },
        hide(hidden) { document.hidden = hidden; for (const listener of listeners) listener(); },
        emit(type, startTime, duration) {
            for (const observer of observers.filter(o => o.connected && o.type === type))
                observer.callback({ getEntries: () => [{ entryType: type, startTime, duration }] });
        }
    };
}

test("FPS uses observed intervals, and median/p95/max preserve real frame gaps", () => {
    const f = fake();
    const monitor = createEditorPerformanceMonitor(f.environment);
    assert.equal(monitor.read().state, "collecting");
    f.frame(10);
    for (let i = 0; i < 19; i++) f.frame(10);
    f.frame(90);
    const sample = monitor.read();
    assert.equal(sample.state, "active");
    assert.equal(sample.frameIntervals, 20);
    assert.equal(sample.fps, 20000 / 280);
    assert.equal(sample.medianGapMs, 10);
    assert.equal(sample.p95GapMs, 10);
    assert.equal(sample.maxGapMs, 90);
    assert.equal(sample.historySamples, 1);
    assert.equal(monitor.read().historySamples, 1);
    monitor.dispose();
});

test("window expires old gaps and uses measured high-refresh cadence", () => {
    const f = fake();
    const monitor = createEditorPerformanceMonitor(f.environment);
    f.frame(1); f.frame(300);
    for (let i = 0; i < 640; i++) f.frame(8);
    const sample = monitor.read();
    assert.equal(sample.windowSeconds, 5);
    assert.equal(sample.frameIntervals, 625);
    assert.equal(sample.fps, 125);
    assert.equal(sample.maxGapMs, 8);
    monitor.dispose();
});

test("entry types are independently feature detected; null is different from zero", () => {
    const f = fake(["longtask"]);
    const monitor = createEditorPerformanceMonitor(f.environment);
    assert.equal(monitor.read().longAnimationFrames, null);
    assert.deepEqual(monitor.read().longTasks, { count: 0, totalDurationMs: 0, maxDurationMs: null, over50Ms: 0 });
    f.advance(200);
    f.emit("longtask", 10, 70);
    f.emit("longtask", 100, 60);
    f.emit("longtask", -1, 70);
    f.emit("longtask", 0, NaN);
    assert.deepEqual(monitor.read().longTasks, { count: 2, totalDurationMs: 130, maxDurationMs: 70, over50Ms: 30 });
    f.advance(5100);
    assert.equal(monitor.read().longTasks.count, 0);
    monitor.dispose();
});

test("hidden intervals never contaminate gaps, tasks or history and stale callbacks are ignored", () => {
    const f = fake();
    const monitor = createEditorPerformanceMonitor(f.environment);
    f.frame(10); f.frame(10);
    const late = [...f.pending.values()][0];
    const oldObserver = f.observers[0];
    f.hide(true);
    assert.equal(f.pending.size, 0);
    assert.equal(oldObserver.connected, false);
    assert.deepEqual(monitor.read(), { state: "paused" });
    f.advance(30000);
    f.hide(false);
    late(30020);
    oldObserver.callback({ getEntries: () => [{ entryType: "longtask", startTime: 30020, duration: 100 }] });
    assert.equal(f.pending.size, 1);
    assert.equal(monitor.read().state, "collecting");
    assert.equal(monitor.read().historySamples, 0);
    f.frame(8); f.frame(8);
    assert.equal(monitor.read().fps, 125);
    assert.equal(monitor.read().longTasks.count, 0);
    monitor.dispose();
    monitor.dispose();
    assert.equal(f.pending.size, 0);
    assert.equal(f.listeners.size, 0);
    assert.ok(f.observers.every(o => !o.connected));
    assert.deepEqual(monitor.read(), { state: "unavailable" });
});

test("history retains only the 120 most recent foreground samples", () => {
    const f = fake();
    const monitor = createEditorPerformanceMonitor(f.environment);
    f.frame(1); f.frame(1000);
    assert.equal(monitor.read().historyLowestFps, 1);
    for (let i = 0; i < 130; i++) {
        for (let j = 0; j < 50; j++) f.frame(20);
        monitor.read();
    }
    const sample = monitor.read();
    assert.equal(sample.historySamples, 120);
    assert.equal(sample.historyLowestFps, 50);
    assert.equal(sample.historyWorstP95Ms, 20);
    monitor.dispose();
});

test("no rAF still permits independent long animation frame measurements", () => {
    const f = fake(["long-animation-frame"]);
    delete f.environment.requestAnimationFrame;
    const monitor = createEditorPerformanceMonitor(f.environment);
    f.advance(80);
    f.emit("long-animation-frame", 10, 70);
    const sample = monitor.read();
    assert.equal(sample.state, "unavailable");
    assert.equal(sample.fps, null);
    assert.equal(sample.longTasks, null);
    assert.equal(sample.longAnimationFrames.count, 1);
    monitor.dispose();
});

test("observer construction failure does not interrupt the other measurements", () => {
    const f = fake();
    const Original = f.environment.PerformanceObserver;
    f.environment.PerformanceObserver = class extends Original {
        observe(options) {
            if (options.type === "longtask") throw new Error("unsupported");
            super.observe(options);
        }
    };
    const monitor = createEditorPerformanceMonitor(f.environment);
    f.frame(10); f.frame(10);
    assert.equal(monitor.read().fps, 100);
    assert.equal(monitor.read().longTasks, null);
    assert.equal(monitor.read().longAnimationFrames.count, 0);
    monitor.dispose();
});

test("completed foreground stalls longer than the rolling window remain visible", () => {
    const f = fake();
    const monitor = createEditorPerformanceMonitor(f.environment);
    f.frame(10);
    f.frame(7000);
    f.emit("longtask", 10, 7000);
    const sample = monitor.read();
    assert.equal(sample.maxGapMs, 7000);
    assert.equal(sample.fps, 1000 / 7000);
    assert.equal(sample.longTasks.maxDurationMs, 7000);
    f.advance(5001);
    assert.equal(monitor.read().maxGapMs, null);
    assert.equal(monitor.read().longTasks.count, 0);
    monitor.dispose();
});

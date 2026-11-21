import test from "node:test";
import assert from "node:assert/strict";
import { SignalCapture } from "../../src/Orbit.Infrastructure/wwwroot/audio/Components/SignalCapture.js";

test("master isolation, shared analyzer leases, bounded windows and teardown", () => {
    const links = [];
    const makeNode = () => ({
        connect(destination, output = 0) { links.push([this, destination, output]); },
        disconnect(destination) {
            for (let i = links.length - 1; i >= 0; i--)
                if (links[i][0] === this && (!destination || links[i][1] === destination)) links.splice(i, 1);
        }
    });
    const source = makeNode(), splitter = makeNode(), gain = makeNode();
    let count = 0;
    const context = {
        currentTime: 0, sampleRate: 48000, state: "running",
        createChannelSplitter(channels) { assert.equal(channels, 3); return splitter; },
        createAnalyser() {
            count++;
            return { ...makeNode(), getFloatTimeDomainData(samples) { samples.fill(0.75); } };
        }
    };
    const capture = new SignalCapture(context, source, 3);
    capture.connectMaster(gain);
    assert.deepEqual(links, [[source, splitter, 0], [splitter, gain, 0]]);
    assert.equal(capture.read("first", 2, "generation"), null);
    assert.equal(capture.read("second", 2, "generation"), null);
    assert.equal(count, 1);
    context.currentTime = 0.2;
    const frame = capture.read("first", 2, "generation");
    assert.equal(frame.samples.length, 1024);
    assert.equal(frame.samples[0], 0.75);
    assert.equal(frame.generation, "generation");
    const full = capture.readWindow("second", 2, "generation");
    assert.equal(full.samples.length, SignalCapture.AnalysisWindowSize);
    assert.equal(full.samples, capture.readWindow("first", 2, "generation").samples);
    assert.equal(count, 1);
    assert.equal(links.at(-1)[2], 2);
    capture.release("first");
    assert.equal(links.length, 3);
    capture.release("second");
    assert.equal(links.length, 2);
    for (let i = 0; i < SignalCapture.MaxLeases; i++) capture.read(i, 1, "generation");
    assert.equal(capture.read("overflow", 2, "generation"), null);
    assert.equal(count, 2);
    capture.dispose();
    assert.deepEqual(links, [[source, splitter, 0]]);
});

test('dynamic taps allocate only on demand, share aliases, refuse ninth, and wait for fresh assignment', () => {
    const writes = [], acks = new Map();
    const makeNode = () => ({ connect() {}, disconnect() {} });
    const context = { currentTime: 0, sampleRate: 48000, state: 'running',
        createChannelSplitter: () => makeNode(),
        createAnalyser: () => ({ ...makeNode(), getFloatTimeDomainData(samples) { samples.fill(0.25); } }) };
    const routing = { selectorCount: 15, write: (slot, token) => writes.push([slot, token]), ack: slot => acks.get(slot) };
    const capture = new SignalCapture(context, makeNode(), 9, routing);
    const observe = (lease, selector) => capture.observe(lease, selector, 'generation');
    assert.deepEqual(writes, []);
    assert.deepEqual(capture.getDiagnostics(), { occupiedSlots: 0, freeSlots: 8, activeLeases: 0 });
    for (let selector = 1; selector <= 15; selector++) {
        assert.equal(observe('individual', selector).availability, 2);
        const [slot, token] = writes.at(-1);
        assert.equal(slot, 1);
        assert.equal(token % 16, selector);
        context.currentTime += 1;
        assert.equal(observe('individual', selector).availability, 2); // previous ACK is stale
        acks.set(slot, token);
        assert.equal(observe('individual', selector).availability, 2); // fresh full buffer required
        context.currentTime += 0.2;
        assert.equal(observe('individual', selector).availability, 3);
        capture.release('individual');
        assert.deepEqual(writes.at(-1), [1, 0]);
    }
    for (let i = 1; i <= 8; i++) assert.equal(observe(`view${i}`, i).availability, 2);
    const count = writes.length;
    assert.equal(observe('alias', 1).availability, 2);
    assert.equal(writes.length, count);
    assert.deepEqual(observe('ninth', 15), { availability: 4, frame: null, reason: 'slots', occupiedSlots: 8, freeSlots: 0, activeLeases: 9 });
    assert.equal(observe('master', 0).availability, 2);
    assert.equal(observe('unrouted', 16).availability, 1);
    capture.release('view1');
    assert.equal(observe('ninth', 15).availability, 4);
    capture.release('alias');
    assert.equal(observe('ninth', 15).availability, 2);
    assert.equal(writes.at(-1)[0], 1);
    capture.dispose();
    assert.deepEqual(writes.slice(-8).map(([, token]) => token), Array(8).fill(0));
});

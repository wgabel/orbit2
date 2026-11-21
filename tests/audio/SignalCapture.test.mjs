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
    context.currentTime = 0.1;
    const frame = capture.read("first", 2, "generation");
    assert.equal(frame.samples.length, 1024);
    assert.equal(frame.samples[0], 0.75);
    assert.equal(frame.generation, "generation");
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

// Test the exact same sample-frame calculation used by the transport worklet.
import { test } from "node:test";
import assert from "node:assert/strict";
import { beatAtFrame, toTransportAnchor } from "../../src/Orbit.Infrastructure/wwwroot/audio/Components/TransportPhase.js";

for (const sampleRate of [44100, 48000]) {
    test(`transport phase and wrap at ${sampleRate} Hz`, () => {
        const anchor = toTransportAnchor({ beatsPerMinute: 120, beatPosition: 11,
            isPlaying: true, clock: { currentSample: sampleRate, sampleRate } });
        assert.equal(beatAtFrame(anchor, sampleRate, sampleRate), 11);
        assert.equal(beatAtFrame(anchor, sampleRate * 1.5, sampleRate), 0);
        assert.equal(beatAtFrame(anchor, sampleRate * 2, sampleRate), 1);
        const changed = toTransportAnchor({ beatsPerMinute: 60, beatPosition: 13,
            isPlaying: true, clock: { currentSample: sampleRate * 2, sampleRate } });
        assert.equal(beatAtFrame(changed, sampleRate * 2, sampleRate), 1);
        assert.equal(beatAtFrame(changed, sampleRate * 3, sampleRate), 2);
        const stopped = toTransportAnchor({ beatsPerMinute: 60, beatPosition: 14, isPlaying: false });
        assert.equal(beatAtFrame(stopped, sampleRate * 100, sampleRate), 2);
    });
}

test("invalid anchors cannot reach the worklet", () => {
    assert.throws(() => toTransportAnchor({ beatsPerMinute: 0, beatPosition: 0, isPlaying: false }));
    assert.throws(() => toTransportAnchor({ beatsPerMinute: 120, beatPosition: 0, isPlaying: true }));
});

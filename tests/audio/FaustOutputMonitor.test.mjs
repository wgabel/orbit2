// Test passive meter adaptation without a browser or audio device.
import test from "node:test";
import assert from "node:assert/strict";
import { FaustOutputMonitor } from "../../src/Orbit.Infrastructure/wwwroot/audio/Faust/FaustOutputMonitor.js";

test("keeps finite latest readings, returns independent snapshots and detaches", () => {
    let handler;
    const node = { setOutputParamHandler: callback => { handler = callback; } };
    const monitor = new FaustOutputMonitor(node);
    assert.deepEqual(monitor.getSnapshot(), {});
    handler("/frequency", 440);
    handler("/frequency", 480);
    handler("/frequency", NaN);
    handler("/gain", 0.5);
    handler("/OrbitGraph/orbit_tap_ack_1", 123);
    assert.equal(monitor.getTapAcknowledgement(1), 123);
    assert.equal(monitor.getTapAcknowledgement(2), undefined);
    const snapshot = monitor.getSnapshot();
    assert.deepEqual(snapshot, { "/frequency": 480, "/gain": 0.5 });
    snapshot["/frequency"] = 0;
    assert.equal(monitor.getSnapshot()["/frequency"], 480);
    const oldHandler = handler;
    monitor.dispose();
    assert.equal(handler, null);
    oldHandler("/frequency", 100);
    assert.deepEqual(monitor.getSnapshot(), {});
    assert.equal(monitor.getTapAcknowledgement(1), undefined);
});

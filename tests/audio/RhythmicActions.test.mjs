import test from 'node:test';
import assert from 'node:assert/strict';
import { RhythmicActionQueue, MAX_ACTIONS } from '../../src/Orbit.Infrastructure/wwwroot/audio/Components/RhythmicActionQueue.js';
import { RhythmicActionSource } from '../../src/Orbit.Infrastructure/wwwroot/audio/Components/RhythmicActionSource.js';

test('rapid commands preserve FIFO, independent lanes and zero samples across block boundaries', () => {
    const queue = new RhythmicActionQueue(2);
    for (const command of [1, 1, 2, 1]) assert.ok(queue.push(0, command));
    assert.ok(queue.push(1, 2));
    const output = [new Float32Array(3).fill(99), new Float32Array(3), new Float32Array(3)];
    assert.deepEqual([...queue.render(output, 1)], [2, 1]);
    assert.deepEqual(output.map(a => [...a]), [[99,99,99], [1,0,1], [2,0,0]]);
    assert.deepEqual([...queue.render(output, 1)], [1, 0]);
    assert.deepEqual([...output[1]], [0,2,0]);
    assert.deepEqual([...queue.render(output, 1)], [1, 0]);
    assert.deepEqual([...output[1]], [1,0,0]);
});

test('bounded action queues reject overflow and clear on deactivation', () => {
    const queue = new RhythmicActionQueue(1);
    for (let i = 0; i < MAX_ACTIONS; i++) assert.ok(queue.push(0, 1));
    assert.equal(queue.push(0, 1), false);
    assert.equal(queue.push(1, 1), false);
    assert.equal(queue.push(0, 3), false);
    queue.clear();
    const output = [new Float32Array(128)];
    assert.deepEqual([...queue.render(output, 0)], [0]);
    assert.ok(output[0].every(x => x === 0));
});

test('host bounds pending messages, validates allowed actions and rejects retired sources', () => {
    const messages = [];
    const port = { postMessage: message => messages.push(message), close() {} };
    const source = new RhythmicActionSource({port, disconnect() {}}, [{allowClose: false}, {allowClose: true}]);
    assert.equal(source.send(0, 2), false);
    assert.equal(source.send(1, 2), true);
    for (let i = 0; i < MAX_ACTIONS; i++) assert.equal(source.send(0, 1), true);
    assert.equal(source.send(0, 1), false);
    port.onmessage({data: [1, 1]});
    assert.equal(source.send(0, 1), true);
    source.dispose();
    assert.deepEqual(messages.at(-1), {clear: true});
    assert.equal(source.send(1, 1), false);
});

test('pitch lanes accept only bounded note/reset codes, seed a replacement and preserve rhythmic actions', () => {
    const inputs = [{isPitch: true, initialAction: 70}, {allowClose: false}, {allowClose: true}];
    const queue = new RhythmicActionQueue(inputs);
    assert.equal(queue.push(0, 130), false);
    assert.equal(queue.push(0, 0), false);
    assert.equal(queue.push(1, 70), false);
    assert.equal(queue.push(1, 2), false);
    assert.ok(queue.push(0, 128));
    assert.ok(queue.push(0, 129));
    assert.ok(queue.push(2, 2));
    const output = inputs.map(() => new Float32Array(6));
    assert.deepEqual([...queue.render(output, 0)], [3, 0, 1]);
    assert.deepEqual(output.map(a => [...a]), [[70,0,128,0,129,0],[0,0,0,0,0,0],[2,0,0,0,0,0]]);
    const source = new RhythmicActionSource({port: {postMessage() {}, close() {}}, disconnect() {}}, inputs);
    assert.equal(source.pending[0], 1);
    assert.equal(source.send(1, 70), false);
    assert.equal(source.send(0, 129), true);
    assert.equal(source.send(0, 129.5), false);
});

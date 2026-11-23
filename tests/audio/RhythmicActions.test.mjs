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
    assert.equal(queue.push(0, 131), false);
    assert.equal(queue.push(0, 0), false);
    assert.equal(queue.push(1, 70), false);
    assert.equal(queue.push(1, 2), false);
    assert.ok(queue.push(0, 128));
    assert.ok(queue.push(0, 129));
    assert.ok(queue.push(2, 2));
    const output = inputs.map(() => new Float32Array(6));
    assert.deepEqual([...queue.render(output, 0)], [3, 0, 1]);
    assert.deepEqual(output.map(a => [...a]), [[70,0,128,0,129,0],[0,0,0,0,0,0],[2,0,0,0,0,0]]);
    assert.equal(queue.push(0, 130), true); // new Note-mode gate-release word
    const source = new RhythmicActionSource({port: {postMessage() {}, close() {}}, disconnect() {}}, inputs);
    assert.equal(source.pending[0], 1);
    assert.equal(source.send(1, 70), false);
    assert.equal(source.send(0, 129), true);
    assert.equal(source.send(0, 130), true);
    assert.equal(source.send(0, 129.5), false);
});

test('note lanes preserve priority, fallback velocity, reset and overload release', () => {
    const queue = new RhythmicActionQueue([{isNote:true}]);
    const on = (note, v) => note+1+512*v, off = note => 130+note;
    const a = on(60, 31), b = on(64, 100), c = on(67, 127);
    for (const code of [a,b,c,off(64),off(67),off(60),a,129]) assert.ok(queue.push(0,code));
    const output = new Float32Array(16);
    assert.deepEqual([...queue.render([output],0)], [8]);
    assert.deepEqual([...output], [131072+a,0,131072+b,0,131072+c,0,0,0,a,0,129,0,131072+a,0,129,0]);
    for (let i=0;i<MAX_ACTIONS;i++) assert.ok(queue.push(0,a));
    assert.equal(queue.push(0,b), false);
    assert.ok(queue.push(0,off(60)));
    assert.deepEqual([...queue.render([output],0)], [MAX_ACTIONS+1]);
    assert.equal(output[0],129);
    assert.equal(output.slice(1).every(v => v===0),true);
    assert.equal(queue.push(0,512+129),false);
});

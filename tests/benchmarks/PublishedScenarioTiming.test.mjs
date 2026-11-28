import test from 'node:test';
import assert from 'node:assert/strict';
import { createPublishedScenarioTimer } from '../browser/PublishedScenarioTiming.mjs';

function fixture() {
    let time = 100;
    const messages = [];
    const timer = createPublishedScenarioTimer({ now: () => time, log: message => messages.push(message) });
    return { timer, messages, advance: ms => { time += ms; } };
}

test('scenario boundaries record each sequential stage and total exactly once', () => {
    const { timer, messages, advance } = fixture();
    timer.enter('startup');
    advance(12);
    timer.enter('playback');
    advance(34);
    timer.finish('passed');
    timer.finish('failed'); // finally must not replace a successful result.
    assert.deepEqual(messages, [
        'PUBLISHED SCENARIO WALL {"scenario":"startup","status":"passed","wallMs":12}',
        'PUBLISHED SCENARIO WALL {"scenario":"playback","status":"passed","wallMs":34}',
        'PUBLISHED SCENARIO TOTAL {"status":"passed","scenarios":2,"wallMs":46}'
    ]);
});

test('failed run records completed stages and the active failed stage', () => {
    const { timer, messages, advance } = fixture();
    timer.enter('startup');
    advance(7);
    timer.enter('mobile');
    advance(19);
    timer.finish('failed');
    assert.deepEqual(messages, [
        'PUBLISHED SCENARIO WALL {"scenario":"startup","status":"passed","wallMs":7}',
        'PUBLISHED SCENARIO WALL {"scenario":"mobile","status":"failed","wallMs":19}',
        'PUBLISHED SCENARIO TOTAL {"status":"failed","scenarios":2,"wallMs":26}'
    ]);
});

test('rejects duplicate names, invalid completion and markers after finish', () => {
    const { timer } = fixture();
    assert.throws(() => timer.finish('passed'));
    timer.enter('startup');
    assert.throws(() => timer.enter('startup'));
    assert.throws(() => timer.finish('skipped'));
    timer.finish('passed');
    assert.throws(() => timer.enter('next'));
});

import test from 'node:test';
import assert from 'node:assert/strict';
import { PitchPatternScheduler, validPitchPattern } from '../../src/Orbit.Infrastructure/wwwroot/audio/Components/PitchPatternScheduler.js';
import { positionAtFrame, toTransportAnchor } from '../../src/Orbit.Infrastructure/wwwroot/audio/Components/TransportPhase.js';

const pattern = (overrides = {}) => ({loop: true, count: 4, free: false, ticks: 160, seconds: 20,
    steps: [46, 0, 53, 0, ...Array(60).fill(0)], ...overrides});
function clock(beat, seconds, generation = 0) {
    return [0, 0, Math.floor(beat), beat % 1, Math.floor(seconds), seconds % 1, generation].map(v => Float32Array.of(v));
}
function sample(scheduler, beat, seconds, generation = 0, manual = 0) {
    const output = Float32Array.of(manual);
    scheduler.render(output, clock(beat, seconds, generation));
    return output[0];
}
test('40-beat steps cross the legacy 12-beat wrap, hold, wrap, reset, and freeze', () => {
    const s = new PitchPatternScheduler(pattern());
    assert.equal(sample(s, 0, 0), 256 + 46);
    for (const beat of [11.999,12,24,36,39.999]) assert.equal(sample(s, beat, beat/2), 0);
    assert.equal(sample(s, 40, 20), 512);
    assert.equal(sample(s, 40, 20), 0);
    assert.equal(sample(s, 80, 40), 768 + 53);
    assert.equal(sample(s, 120, 60), 1024);
    assert.equal(sample(s, 160, 80), 256 + 46);
    assert.equal(sample(s, 0, 0, 1), 256 + 46);
});
test('free time is independent of tempo; long seek resolves one current slot only', () => {
    const s = new PitchPatternScheduler(pattern({free:true}));
    assert.equal(sample(s, 0, 0), 302);
    assert.equal(sample(s, 55, 19.999), 0);
    assert.equal(sample(s, 55.001, 20), 512);
    assert.equal(sample(s, 10000, 40060), 1024 + 53);
    assert.equal(sample(s, 10000, 40060), 0);
    assert.equal(sample(s, 10000, 40060, 0, 61), 61);
});
test('leading Holds, inactive slots, atomic edits, and stop preserve current pitch', () => {
    const s = new PitchPatternScheduler(pattern({steps:[0,46,0,0,...Array(60).fill(0)]}));
    assert.equal(sample(s,0,0),256);
    assert.equal(sample(s,40,20),512+46);
    assert.equal(sample(s,80,40),768);
    assert.equal(sample(s,120,60),1024);
    assert.equal(sample(s,160,80),256); // previous pitch holds on wrap
    assert.equal(sample(s,0,0,1),256+129);
    assert.equal(s.setPattern(pattern({count:0})),false);
    assert.equal(s.setPattern(pattern({loop:false})),true);
    assert.equal(sample(s,0,0,1,61),65*256+61);
    assert.equal(sample(s,0,0,1),0);
    assert.equal(validPitchPattern(pattern({steps:[129]})),false);
});
test('committed slot edits wait for future boundaries without replaying the current step', () => {
    const s = new PitchPatternScheduler(pattern());
    assert.equal(sample(s, 0, 0), 256 + 46);
    assert.equal(s.setPattern(pattern({steps:[70, 61, 53, 0, ...Array(60).fill(0)]})),true);
    assert.equal(sample(s, 5, 2.5), 0);
    assert.equal(sample(s, 40, 20), 512 + 61);
    assert.equal(sample(s, 160, 80), 256 + 70);
    // Reset and timing changes still resolve the current slot immediately.
    assert.equal(sample(s, 0, 0, 1), 256 + 70);
    assert.equal(s.setPattern(pattern({ticks:80})),true);
    assert.equal(sample(s, 0, 0, 1), 256 + 46);
});
for (const rate of [44100,48000]) test(`fractional sample timing survives long absolute positions at ${rate}`, () => {
    const anchor = toTransportAnchor({beatsPerMinute:120, beatPosition:40000, elapsedSeconds:20000, isPlaying:true,
        clock:{currentSample:0, sampleRate:rate}});
    const s = new PitchPatternScheduler(pattern({free:true, seconds:.01}));
    const n = rate * .04;
    const channels = Array.from({length:7},()=>new Float32Array(n));
    for(let i=0;i<n;i++) {
        const {beat,seconds} = positionAtFrame(anchor,i,rate);
        channels[2][i]=Math.floor(beat); channels[3][i]=beat%1;
        channels[4][i]=Math.floor(seconds); channels[5][i]=seconds%1;
    }
    const output = new Float32Array(n);
    s.render(output,channels);
    assert.deepEqual([...output.entries()].filter(([,v])=>v).map(([i])=>i),[0,rate*.01,rate*.02,rate*.03]);
});

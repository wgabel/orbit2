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
    assert.equal(validPitchPattern(pattern({steps:[129]})),false); // capacity remains fixed
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

test('note-mode emits trigger onset, tie, rest and sample-clocked gate release', () => {
    const s = new PitchPatternScheduler(pattern({notes:true, count:4, ticks:4, gateFraction:.5,
        steps:[46,0,129,53,...Array(60).fill(0)]}));
    assert.equal(sample(s,0,0),256+46);
    assert.equal(sample(s,.5,.25),0); // step 1 ties into step 2
    assert.equal(sample(s,1,.5),512);
    assert.equal(sample(s,1.5,.75),512+130);
    assert.equal(sample(s,2,1),768+130);
    assert.equal(sample(s,3,1.5),1024+53);
    assert.equal(sample(s,3.5,1.75),1024+130);
    assert.equal(sample(s,4,2),256+46);
});
test('legacy pitch-only patterns do not emit note release actions', () => {
    const s = new PitchPatternScheduler(pattern({count:2,ticks:4,steps:[46,129,...Array(62).fill(0)]}));
    assert.equal(sample(s,0,0),256+46);
    assert.equal(sample(s,.5,.25),0);
    assert.equal(sample(s,1,.5),512);
});

test('per-note length overrides differ, ties inherit onset length and edits wait for the next onset', () => {
    const lengths = [.25, 0, .75, 0, ...Array(60).fill(0)];
    const p = pattern({notes:true, count:4, ticks:4, gateFraction:.5,
        gateFractions:lengths, steps:[46,0,53,129,...Array(60).fill(0)]});
    const s = new PitchPatternScheduler(p);
    assert.equal(sample(s,0,0),256+46);
    assert.equal(sample(s,.25,.125),0); // tie prevents early release
    assert.equal(sample(s,1,.5),512);
    assert.equal(s.setPattern({...p,gateFractions:[.75,...lengths.slice(1)]}),true);
    assert.equal(sample(s,1.25,.625),512+130); // original onset length
    assert.equal(sample(s,2,1),768+53);
    assert.equal(sample(s,2.5,1.25),0);
    assert.equal(sample(s,2.75,1.375),768+130);
    assert.equal(sample(s,3,1.5),1024+130);
    assert.equal(sample(s,4,2),256+46);
    assert.equal(sample(s,5,2.5),512);
    assert.equal(sample(s,5.25,2.625),0);
    assert.equal(sample(s,5.75,2.875),512+130);
    assert.equal(validPitchPattern({...p,gateFractions:[.5]}),false);
    assert.equal(validPitchPattern({...p,gateFractions:[-1,...lengths.slice(1)]}),false);
    assert.equal(validPitchPattern({...p,gateFractions:[NaN,...lengths.slice(1)]}),false);
});
test('zero duration override inherits the pattern default and reset clears a tied gate', () => {
    const s = new PitchPatternScheduler(pattern({notes:true,count:3,ticks:4,gateFraction:.5,
        gateFractions:Array(64).fill(0),steps:[46,53,0,...Array(61).fill(0)]}));
    assert.equal(sample(s,0,0),256+46);
    assert.equal(sample(s,.5,.25),256+130);
    assert.equal(sample(s,1,.5),512+53);
    assert.equal(sample(s,2,1),768);
    assert.equal(sample(s,2,1,1),768+130);
});

test('velocity overrides are packed only on articulated onsets, including explicit zero', () => {
    const velocities = [.2, .99, 0, -1, ...Array(60).fill(-1)];
    const p = pattern({notes:true,count:4,ticks:4,gateFraction:.5,
        velocityOverrides:velocities,steps:[46,0,46,53,...Array(60).fill(0)]});
    const s = new PitchPatternScheduler(p);
    assert.equal(sample(s,0,0),21*32768+256+46);
    assert.equal(sample(s,1,.5),512); // tie never transmits its stored velocity
    assert.equal(s.setPattern({...p,velocityOverrides:[1,...velocities.slice(1)]}),true);
    assert.equal(sample(s,1.25,.625,0,61),61); // manual action is not packed
    assert.equal(sample(s,1.5,.75),512+130);
    assert.equal(sample(s,2,1),32768+768+46); // repeated pitch, explicit zero
    assert.equal(sample(s,2.5,1.25),768+130);
    assert.equal(sample(s,3,1.5),1024+53); // inherits the existing default
    assert.equal(sample(s,4,2),101*32768+256+46);
    assert.equal(sample(s,0,0,1),101*32768+256+46);
    const drone = new PitchPatternScheduler({...p,notes:false});
    assert.equal(sample(drone,0,0),256+46);
    for (const invalid of [-.5, 1.01, .123, NaN, Infinity, null])
        assert.equal(validPitchPattern({...p,velocityOverrides:[invalid,...velocities.slice(1)]}),false);
    assert.equal(validPitchPattern({...p,velocityOverrides:[.5]}),false);
    assert.equal(validPitchPattern({...p,velocityOverrides:null}),true);
});

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

test('velocity follows shared beat positions across tempo changes and discontinuous seeks', () => {
    const p = pattern({notes:true,count:4,ticks:4,gateFraction:.5,
        steps:[46,0,53,0,...Array(60).fill(0)],
        velocityOverrides:[.3,.9,.7,.1,...Array(60).fill(-1)]});
    const s = new PitchPatternScheduler(p);
    assert.equal(sample(s,0,0),31*32768+256+46);
    assert.equal(sample(s,.75,.375),0);
    assert.equal(sample(s,1,.75),512); // beat boundary, despite a change in elapsed time
    assert.equal(sample(s,1.5,1.25),512+130);
    assert.equal(sample(s,2,1.75),71*32768+768+53);
    assert.equal(sample(s,2.25,2),0);
    assert.equal(sample(s,7,4),1024+130); // seek into a tie: no stale held note
    assert.equal(sample(s,8,4.5),31*32768+256+46);
});

test('signed timing offsets preserve order, gate duration, velocity and rests', () => {
    const p = pattern({notes:true,count:4,ticks:4,gateFraction:.5,
        steps:[46,46,129,53,...Array(60).fill(0)],
        timingOffsets:[.2,-.2,.45,.1,...Array(60).fill(0)],
        velocityOverrides:[.2,0,-1,.8,...Array(60).fill(-1)]});
    const s = new PitchPatternScheduler(p);
    assert.equal(sample(s,0,0),256+130);
    assert.equal(sample(s,.19,.095),0);
    assert.equal(sample(s,.2,.1),21*32768+256+46);
    assert.equal(sample(s,.7,.35),256+130);
    assert.equal(sample(s,.79,.395),0);
    assert.equal(sample(s,.8,.4),32768+512+46);
    assert.equal(sample(s,1, .5),0);
    assert.equal(sample(s,1.3,.65),512+130);
    assert.equal(sample(s,2,1),768+130);
    assert.equal(sample(s,3,1.5),0);
    assert.equal(sample(s,3.1,1.55),81*32768+1024+53);
    assert.equal(sample(s,3.6,1.8),1024+130);
    assert.equal(sample(s,4,2),0);
    assert.equal(sample(s,4.2,2.1),21*32768+256+46);
    assert.equal(sample(s,0,0,1),256+130);
    assert.equal(sample(s,.2,.1,1),21*32768+256+46);
});
test('early first note clips at reset, anticipates wrap, and shifted ties retain duration', () => {
    const p = pattern({notes:true,count:4,ticks:4,gateFraction:.25,
        steps:[46,0,53,129,...Array(60).fill(0)],
        timingOffsets:[-.4,.45,-.4,0,...Array(60).fill(0)]});
    const s = new PitchPatternScheduler(p);
    assert.equal(sample(s,0,0),256+46);
    assert.equal(sample(s,1,.5),512);
    assert.equal(sample(s,1.25,.625),512+130);
    assert.equal(sample(s,1.6,.8),768+53);
    assert.equal(sample(s,1.85,.925),768+130);
    assert.equal(sample(s,3,1.5),1024+130);
    assert.equal(sample(s,3.6,1.8),256+46);
    assert.equal(sample(s,4,2),0);
    assert.equal(sample(s,4.85,2.425),256+130); // shifted note ends before final tie boundary
    assert.equal(sample(s,5,2.5),512);
    assert.equal(sample(s,5.2,2.6,1),512+130); // reset into tie releases
});
test('live timing edits neither duplicate a sounded onset nor replay one moved into the past', () => {
    const p = pattern({notes:true,count:3,ticks:4,gateFraction:.5,
        steps:[46,53,58,...Array(61).fill(0)],
        timingOffsets:[0,-.2,.2,...Array(61).fill(0)]});
    const s = new PitchPatternScheduler(p);
    assert.equal(sample(s,0,0),256+46);
    assert.equal(sample(s,.5,.25),256+130);
    assert.equal(sample(s,.8,.4),512+53);
    const edited = {...p,timingOffsets:[0,.2,.2,...Array(61).fill(0)]};
    assert.equal(s.setPattern(edited),true);
    assert.equal(sample(s,.9,.45),0);
    assert.equal(sample(s,1.2,.6),0);
    assert.equal(sample(s,1.3,.65),512+130);
    assert.equal(sample(s,2.1,1.05),0);
    assert.equal(s.setPattern({...edited,timingOffsets:[0,.2,-.2,...Array(61).fill(0)]}),true);
    assert.equal(sample(s,2.11,1.055),768+130);
    assert.equal(sample(s,2.2,1.1),0);
    assert.equal(sample(s,3,1.5),256+46);
});
test('offset validation and zero-offset/pitch-only compatibility', () => {
    const p = pattern({notes:true,count:4,ticks:4,steps:[46,0,53,129,...Array(60).fill(0)]});
    for (const v of [-.46,.46,.123,NaN,Infinity,null])
        assert.equal(validPitchPattern({...p,timingOffsets:[v,...Array(63).fill(0)]}),false);
    assert.equal(validPitchPattern({...p,timingOffsets:[0]}),false);
    assert.equal(validPitchPattern({...p,timingOffsets:null}),true);
    const legacy = new PitchPatternScheduler(p);
    const zero = new PitchPatternScheduler({...p,timingOffsets:Array(64).fill(0)});
    const drone = new PitchPatternScheduler({...p,notes:false,timingOffsets:Array(64).fill(.45)});
    const oldDrone = new PitchPatternScheduler({...p,notes:false});
    for (const beat of [0,.5,1,1.9,2,2.9,3,4,5,8,0]) {
        assert.equal(sample(legacy,beat,beat/2),sample(zero,beat,beat/2));
        assert.equal(sample(drone,beat,beat/2),sample(oldDrone,beat,beat/2));
    }
});
for (const rate of [44100,48000]) test(`free and beat offsets schedule at sample resolution at ${rate}`, () => {
    for (const free of [true,false]) {
        const p = pattern({notes:true,free,count:3,ticks:1,seconds:.1,gateFraction:.25,
            steps:[46,53,58,...Array(61).fill(0)],timingOffsets:[.2,-.2,.4,...Array(61).fill(0)]});
        const duration = free ? .1 : .125; // 120 BPM sixteenth
        const n = Math.ceil(rate * duration * 3.5);
        const channels = Array.from({length:7},()=>new Float32Array(n));
        for(let i=0;i<n;i++) {
            const seconds=i/rate,beat=seconds*2;
            channels[2][i]=Math.floor(beat); channels[3][i]=beat%1;
            channels[4][i]=Math.floor(seconds); channels[5][i]=seconds%1;
        }
        const output = new Float32Array(n);
        new PitchPatternScheduler(p).render(output,channels);
        const onsets = [...output.entries()].filter(([,v])=>v%256>=1&&v%256<=128).map(([i])=>i);
        assert.deepEqual(onsets,[.2,.8,2.4,3.2].map(u=>Math.ceil(u*duration*rate-1e-7)));
    }
});

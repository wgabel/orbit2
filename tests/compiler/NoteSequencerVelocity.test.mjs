import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { PitchPatternScheduler } from '../../src/Orbit.Infrastructure/wwwroot/audio/Components/PitchPatternScheduler.js';

const fixture = JSON.parse(await readFile(join(process.env.ORBIT_GRAPH_PROBE_DIR, 'note-sequencer.json'), 'utf8'));
const faust = process.env.ORBIT_FAUST_DIR;
const { instantiateFaustModuleFromFile, LibFaust, FaustCompiler, FaustMonoDspGenerator } = await import(pathToFileURL(join(faust, 'dist/esm/index.js')));
const compiler = new FaustCompiler(new LibFaust(await instantiateFaustModuleFromFile(join(faust, 'libfaust-wasm/libfaust-wasm.js'))));
const factory = await compiler.createMonoDSPFactory('NoteSequencer', fixture.source, '');

for (const rate of [44100, 48000]) test(`Scheduled velocity, gate, trigger and pitch stay sample-aligned at ${rate}`, async () => {
    assert.equal(fixture.inputs, 1);
    const dsp = await new FaustMonoDspGenerator().createOfflineProcessor(rate, 128, { ...factory, soundfiles: {} });
    for (const [name, value] of Object.entries(fixture.values)) {
        const path = dsp.getParams().find(p => p.endsWith('/' + name));
        assert.ok(path, name); dsp.setParamValue(path, value);
    }
    const step = rate / 100;
    const n = Math.ceil(8 * step / 128) * 128;
    const resetFrame = 7 * step;
    const manualFrame = resetFrame + Math.floor(step / 4);
    const clock = Array.from({length:7}, () => new Float32Array(n));
    for (let i = 0; i < n; i++) {
        const seconds = (i < resetFrame ? i : i - resetFrame) / rate;
        clock[4][i] = Math.floor(seconds); clock[5][i] = seconds % 1;
        clock[6][i] = i < resetFrame ? 0 : 1;
    }
    const pattern = {loop:true, free:true, count:6, ticks:4, seconds:.01, notes:true, gateFraction:.5,
        steps:[70,0,70,70,129,82,...Array(58).fill(0)],
        velocityOverrides:[.2,.99,.73,0,-1,-1,...Array(58).fill(-1)]};
    const scheduler = new PitchPatternScheduler(pattern);
    const action = new Float32Array(n);
    action[manualFrame] = 72;
    scheduler.render(action, clock);
    const result = dsp.render([action], n);
    const { 'pitch-out': p, 'gate-out': g, 'trigger-out': t, 'velocity-out': v } = fixture.channels;
    const pulses = [...result[t].entries()].filter(([,value]) => value !== 0).map(([i]) => i);
    assert.deepEqual(pulses, [0, 2*step, 3*step, 5*step, 6*step, resetFrame, manualFrame]);
    const check = (frame, pitch, gate, velocity) => {
        assert.equal(result[p][frame], pitch, `pitch at ${frame}`);
        assert.equal(result[g][frame], gate, `gate at ${frame}`);
        assert.ok(Math.abs(result[v][frame] - velocity) < 1e-6, `velocity at ${frame}: ${result[v][frame]}`);
    };
    check(0,0,1,.2);
    check(step,0,1,.2); // tie ignores its own stored velocity
    check(Math.floor(1.75*step),0,0,0);
    check(2*step,0,1,.73); // repeated pitch triggers independently
    check(3*step,0,1,0); // explicit zero still emits a gate and trigger
    check(4*step,0,0,0);
    check(5*step,12,1,.8); // inherited pattern-wide velocity
    check(6*step,0,1,.2); // loop wraps
    check(resetFrame,0,1,.2);
    check(manualFrame,2,1,.8); // unencoded manual onset restores default
    assert.ok(result[0].every(Number.isFinite));
});

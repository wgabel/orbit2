import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { RhythmicActionQueue } from '../../src/Orbit.Infrastructure/wwwroot/audio/Components/RhythmicActionQueue.js';

const fixture = JSON.parse(await readFile(join(process.env.ORBIT_GRAPH_PROBE_DIR, 'note-source.json'), 'utf8'));
const faust = process.env.ORBIT_FAUST_DIR;
const { instantiateFaustModuleFromFile, LibFaust, FaustCompiler, FaustMonoDspGenerator } = await import(pathToFileURL(join(faust, 'dist/esm/index.js')));
const compiler = new FaustCompiler(new LibFaust(await instantiateFaustModuleFromFile(join(faust, 'libfaust-wasm/libfaust-wasm.js'))));
const factory = await compiler.createMonoDSPFactory('NoteSource', fixture.source, '');
const on = (note, velocity) => note + 1 + 512 * velocity;
const off = note => 130 + note;

for (const rate of [44100, 48000]) {
    test(`One note lane drives sample-aligned pitch, gate, trigger, velocity at ${rate}`, async () => {
        assert.equal(fixture.inputs, 1);
        const dsp = await new FaustMonoDspGenerator().createOfflineProcessor(rate, 128, { ...factory, soundfiles: {} });
        for (const [name, value] of Object.entries(fixture.values)) {
            const path = dsp.getParams().find(p => p.endsWith('/' + name));
            assert.ok(path, name); dsp.setParamValue(path, value);
        }
        const queue = new RhythmicActionQueue([{ isNote: true }]);
        const words = new Float32Array(1024);
        // A4, overlapping A5, release active A5 (fallback), repeat A4, then release.
        for (const [frame, code] of [[128,on(69,64)],[256,on(81,127)],[384,off(81)], [512,on(69,32)], [640,off(69)], [768,on(72,96)], [896,129]]) {
            assert.ok(queue.push(0, code)); queue.render([words.subarray(frame,frame+128)], 0);
        }
        const outputs = dsp.render([words], words.length);
        const pitch = outputs[fixture.channels['pitch-out']], gate = outputs[fixture.channels['gate-out']], trigger = outputs[fixture.channels['trigger-out']], velocity = outputs[fixture.channels['velocity-out']];
        const expected = [[0,0,0,0],[128,0,1,64],[256,12,1,127],[384,0,1,64],[512,0,1,32],[640,0,0,32],[768,3,1,96],[896,3,0,96]];
        for (let i = 0; i < expected.length; i++) {
            const [start, p, g, v] = expected[i], end = expected[i+1]?.[0] ?? words.length;
            for (let frame = start; frame < end; frame++) {
                assert.equal(pitch[frame], p, `pitch frame ${frame}`);
                assert.equal(gate[frame], g, `gate frame ${frame}`);
                assert.ok(Math.abs(velocity[frame] - v / 127) < 1e-6, `velocity frame ${frame}`);
                assert.equal(trigger[frame], [128,256,512,768].includes(frame) ? 1 : 0, `trigger frame ${frame}`);
            }
        }
        assert.ok(outputs[0].every(Number.isFinite));
    });
}

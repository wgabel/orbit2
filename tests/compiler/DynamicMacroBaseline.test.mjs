import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const directory = process.env.ORBIT_GRAPH_PROBE_DIR;
const fixture = JSON.parse(await readFile(join(directory, 'dynamic-macro-baseline.json'), 'utf8'));
const faust = process.env.ORBIT_FAUST_DIR;
const { instantiateFaustModuleFromFile, LibFaust, FaustCompiler, FaustMonoDspGenerator } =
    await import(pathToFileURL(join(faust, 'dist/esm/index.js')));
const compiler = new FaustCompiler(new LibFaust(await instantiateFaustModuleFromFile(join(faust, 'libfaust-wasm/libfaust-wasm.js'))));
const factories = new Map();

async function render(rate, program, macro) {
    if (!factories.has(program.source))
        factories.set(program.source, await compiler.createMonoDSPFactory('DynamicMacroReference', program.source, ''));
    const dsp = await new FaustMonoDspGenerator().createOfflineProcessor(rate, fixture.blockSize,
        { ...factories.get(program.source), soundfiles: {} });
    for (const [name, value] of Object.entries(program.values)) {
        const path = dsp.getParams().find(p => p.endsWith('/' + name));
        assert.ok(path, name);
        dsp.setParamValue(path, value);
    }
    if (macro) assert.ok(dsp.getParams().some(p => p.endsWith('/' + macro)), 'Macro slider eliminated');
    const [samples] = dsp.render([], fixture.frames);
    assert.equal(samples.length, fixture.frames);
    for (const sample of samples) assert.ok(Number.isFinite(sample) && Math.abs(sample) <= 1.0001);
    return samples;
}

function measure(samples, rate, start, end) {
    const first = Math.round(start * rate);
    const last = Math.round(end * rate);
    let sum = 0;
    const crossings = [];
    for (let i = first; i < last; i++) {
        const v = samples[i], previous = samples[i - 1];
        sum += v * v;
        if (previous <= 0 && v > 0) crossings.push(i - 1 - previous / (v - previous));
    }
    assert.ok(crossings.length > 2, 'Expected audible carrier');
    return { frequency: (crossings.length - 1) * rate / (crossings.at(-1) - crossings[0]),
        rms: Math.sqrt(sum / (last - first)) };
}

test('connected macro numerical behavior at both sample rates', async () => {
    const results = [];
    for (const rate of [44100, 48000]) {
        for (const item of fixture.cases) {
            const samples = await render(rate, item.candidate, item.macro);
            const windows = [[.2,.35],[.7,.85],[1.2,1.35],[1.7,1.85]];
            const measured = windows.map(([a,b]) => measure(samples,rate,a,b));
            if (item.name === 'disconnected') {
                const reference = await render(rate,item.reference);
                assert.deepEqual(samples, reference);
            }
            if (item.constantOnly) {
                for (const row of measured) {
                    assert.ok(Math.abs(row.frequency - 330) < 1);
                    assert.ok(Math.abs(row.rms - .125 / Math.sqrt(2)) < .004);
                }
            } else {
                const lo = Math.max(0,.5-Math.abs(item.depth));
                const hi = Math.min(1,.5+Math.abs(item.depth));
                const expected = [lo,hi].map(u => ({frequency:220+220*u*u,rms:(.6-.4*u)*.5/Math.sqrt(2)}));
                for (const row of measured) {
                    assert.ok(expected.some(e => Math.abs(row.frequency-e.frequency)<1 &&
                        Math.abs(row.rms-e.rms)<.004), JSON.stringify({item:item.name,rate,row,expected}));
                }
                if (lo !== hi) {
                    assert.ok(measured[0].frequency !== measured[1].frequency);
                    assert.ok(Math.abs(measured[0].frequency-measured[2].frequency)<1);
                    assert.ok(Math.abs(measured[1].frequency-measured[3].frequency)<1);
                }
            }
            results.push({name:item.name,rate,measured});
            console.log('DYNAMIC_MACRO_BASELINE '+JSON.stringify(results.at(-1)));
        }
        const signed=results.find(r=>r.rate===rate && r.name==='signed');
        const inverted=results.find(r=>r.rate===rate && r.name==='inverted');
        assert.ok(Math.abs(signed.measured[0].frequency-inverted.measured[1].frequency)<1);
        assert.ok(Math.abs(signed.measured[1].frequency-inverted.measured[0].frequency)<1);
    }
    await writeFile(join(directory,'dynamic-macro-report.json'),JSON.stringify({
        schemaVersion:1,revision:process.env.GITHUB_SHA ?? null,faustVersion:fixture.faustVersion,results
    },null,2));
});

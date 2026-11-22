import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const fixtures = JSON.parse(await readFile(join(process.env.ORBIT_GRAPH_PROBE_DIR, 'grouping.json'), 'utf8'));
const faust = process.env.ORBIT_FAUST_DIR;
const { instantiateFaustModuleFromFile, LibFaust, FaustCompiler, FaustMonoDspGenerator } =
    await import(pathToFileURL(join(faust, 'dist/esm/index.js')));
const compiler = new FaustCompiler(new LibFaust(await instantiateFaustModuleFromFile(join(faust, 'libfaust-wasm/libfaust-wasm.js'))));
const factories = [];
for (const [index, fixture] of fixtures.entries())
    factories.push(await compiler.createMonoDSPFactory(`Grouping${index}`, fixture.source, ''));

for (const rate of [44100, 48000]) {
    test(`Grouping retains deterministic weighted audio at ${rate} Hz`, async () => {
        const rendered = [];
        for (const [index, fixture] of fixtures.entries()) {
            const dsp = await new FaustMonoDspGenerator().createOfflineProcessor(rate, 128, { ...factories[index], soundfiles: {} });
            for (const [name, value] of Object.entries(fixture.values)) {
                const path = dsp.getParams().find(p => p.endsWith('/' + name));
                assert.ok(path, name);
                dsp.setParamValue(path, value);
            }
            rendered.push(dsp.render([], 32768)[0]);
        }
        const [before, after] = rendered;
        assert.ok(before.some(sample => Math.abs(sample) > .01), 'The reference must be audible');
        let maxError = 0;
        for (let i = 0; i < before.length; i++) {
            assert.ok(Number.isFinite(before[i]) && Number.isFinite(after[i]));
            maxError = Math.max(maxError, Math.abs(before[i] - after[i]));
        }
        assert.ok(maxError < 1e-6, `Max sample error: ${maxError}`);
    });
}

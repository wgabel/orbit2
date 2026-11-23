import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { RhythmicActionQueue } from '../../src/Orbit.Infrastructure/wwwroot/audio/Components/RhythmicActionQueue.js';

const root = process.env.ORBIT_GRAPH_PROBE_DIR;
const faust = process.env.ORBIT_FAUST_DIR;
const { instantiateFaustModuleFromFile, LibFaust, FaustCompiler, FaustMonoDspGenerator } = await import(pathToFileURL(join(faust, 'dist/esm/index.js')));
const compiler = new FaustCompiler(new LibFaust(await instantiateFaustModuleFromFile(join(faust, 'libfaust-wasm/libfaust-wasm.js'))));
const json = async name => JSON.parse(await readFile(join(root, name + '.json'), 'utf8'));
const cores = await json('physical-instrument-cores');
const factories = {};
for(const [key,source] of Object.entries(cores)) factories[key] = await compiler.createMonoDSPFactory(key,source,'-single');
const nframes = (rate, seconds) => Math.ceil(rate * seconds / 128) * 128;
const fill = (n,v) => new Float32Array(n).fill(v);
const peak = x => x.reduce((m,v) => { assert.ok(Number.isFinite(v)); return Math.max(m, Math.abs(v)); },0);
const rms = x => Math.sqrt(x.reduce((s,v)=>s+v*v,0)/x.length);
const tone = (x, rate, f, offset, length) => {
    let a=0,b=0;
    for (let i=offset;i<offset+length;i++) { a+=x[i]*Math.cos(2*Math.PI*f*i/rate); b+=x[i]*Math.sin(2*Math.PI*f*i/rate); }
    return 2*Math.hypot(a,b)/length;
};
async function render(rate, factory, inputs, controls = {}) {
    const dsp = await new FaustMonoDspGenerator().createOfflineProcessor(rate,128,{...factory,soundfiles:{}});
    try {
        for(const [key,value] of Object.entries(controls)) {
            const path=dsp.getParams().find(p=>p.endsWith('/'+key)); assert.ok(path,key); dsp.setParamValue(path,value);
        }
        return dsp.render(inputs,inputs[0].length)[0];
    } finally { dsp.destroy(); }
}
for(const rate of [44100,48000]) {
    test(`Physical Pluck exact core latches strike pitch and velocity, ${rate} Hz`, async()=>{
        const n=nframes(rate,2), start=rate/10|0;
        const a=[0,0,.8,1,0,1].map(v=>fill(n,v));
        a[1][start]=1;
        const y=await render(rate,factories.pluck,a);
        assert.equal(peak(y.subarray(0,start)),0);
        assert.ok(peak(y)>.01 && peak(y)<1);
        assert.ok(tone(y,rate,440,rate*.15|0,rate*.15|0)>tone(y,rate,450,rate*.15|0,rate*.15|0)*2);
        const changed=a.map(x=>x.slice()); changed[0].fill(12,start+1); changed[2].fill(.01,start+1);
        assert.deepEqual(await render(rate,factories.pluck,changed),y);
        const silent=a.map(x=>x.slice()); silent[2].fill(0);
        assert.equal(peak(await render(rate,factories.pluck,silent)),0);
        assert.ok(rms(y.subarray(rate,rate*2))<rms(y.subarray(start,rate/2))*.03);
    });
    test(`Drawbar Organ isolates footage, sustains gate and releases, ${rate} Hz`, async()=>{
        const n=nframes(rate,1.5), gate=fill(n,0); gate.fill(1,rate/10|0,rate);
        const a=[0,0,0,1,0,0,0,0,8,0,0,0,0,0,0].map(v=>fill(n,v)); a[1]=gate;
        const y=await render(rate,factories.organ,a);
        assert.equal(peak(y.subarray(0,rate/10|0)),0);
        assert.ok(Math.abs(tone(y,rate,440,rate/2|0,rate/4|0)-.1)<.005);
        assert.ok(tone(y,rate,880,rate/2|0,rate/4|0)<.001);
        assert.ok(peak(y.subarray(rate+rate/20))<1e-5);
        a[8].fill(0); a[6].fill(8);
        const sub=await render(rate,factories.organ,a);
        assert.ok(tone(sub,rate,220,rate/2|0,rate/4|0)>.095);
        a[6].fill(0); assert.equal(peak(await render(rate,factories.organ,a)),0);
    });
    test(`Rotary crossover, dry path, bounded dual-rotor modulation, ${rate} Hz`,async()=>{
        const n=nframes(rate,5), x=Float32Array.from({length:n},(_,i)=>.1*Math.sin(i*2*Math.PI*220/rate)+.1*Math.sin(i*2*Math.PI*1760/rate));
        const args=[x,...[2,1,1,0].map(v=>fill(n,v))];
        assert.deepEqual(await render(rate,factories.rotary,args),x);
        args[4].fill(1); args[2].fill(0); args[3].fill(0);
        const neutral=await render(rate,factories.rotary,args);
        assert.ok(Math.abs(rms(neutral.subarray(rate))-rms(x.subarray(rate)))<.001);
        args[2].fill(1); args[3].fill(1);
        const wet=await render(rate,factories.rotary,args);
        assert.ok(peak(wet)<.4 && rms(wet.subarray(rate))>.015);
        assert.ok(rms(wet.map((v,i)=>v-neutral[i]))>.015);
        args[0].fill(0); assert.equal(peak(await render(rate,factories.rotary,args)),0);
    });
    test(`Physical Marimba bar responds to pitch, velocity and mallet at ${rate} Hz`,async()=>{
        const n=nframes(rate,2), onset=rate/10|0;
        for(const p of [-21,-12,0,12,15]) {
            const a=[p,0,.8,.45,2,.55,1].map(v=>fill(n,v)); a[1][onset]=1;
            const y=await render(rate,factories.marimba,a);
            assert.equal(peak(y.subarray(0,onset)),0);
            assert.ok(peak(y)>.005 && peak(y)<1);
            const f=440*2**(p/12), start=onset+rate*.03|0, len=rate*.14|0;
            assert.ok(tone(y,rate,f,start,len)>tone(y,rate,f*1.04,start,len)*1.5);
            assert.ok(rms(y.subarray(rate))<rms(y.subarray(onset,rate/2))*.03);
            a[2].fill(0); assert.equal(peak(await render(rate,factories.marimba,a)),0);
        }
        const a=[0,0,.8,.05,2,.55,1].map(v=>fill(n,v)); a[1][onset]=1;
        const soft=await render(rate,factories.marimba,a); a[3].fill(1);
        const hard=await render(rate,factories.marimba,a);
        assert.ok(peak(hard)>peak(soft));
    });
    for(const name of ['generator.physical-pluck','generator.physical-marimba','generator.drawbar-organ']) {
        test(`Registered ${name} plays real Note Source events at ${rate} Hz`,async()=>{
            const fixture=await json(name); assert.equal(fixture.inputs,1);
            const factory=await compiler.createMonoDSPFactory('Graph',fixture.source,'-single');
            const n=nframes(rate,2.5), actions=fill(n,0), onset=nframes(rate,.1), second=nframes(rate,1.5);
            const queue=new RhythmicActionQueue([{isNote:true}]);
            for(const [frame,word] of [[onset,70+512*100],[nframes(rate,1),199],[second,77+512*80],[nframes(rate,2),206]]) {
                assert.ok(queue.push(0,word)); queue.render([actions.subarray(frame,frame+128)],0);
            }
            const y=await render(rate,factory,[actions],fixture.values);
            assert.equal(peak(y.subarray(0,onset)),0);
            assert.ok(peak(y)>.003 && peak(y)<1);
            assert.ok(rms(y.subarray(second,rate*2))>.0001);
            if(name.endsWith('organ')) assert.ok(peak(y.subarray(rate*2+rate/10))<1e-5);
        });
    }
}

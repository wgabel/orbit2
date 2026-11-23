import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { RhythmicActionQueue } from '../../src/Orbit.Infrastructure/wwwroot/audio/Components/RhythmicActionQueue.js';

const root = process.env.ORBIT_GRAPH_PROBE_DIR, faust = process.env.ORBIT_FAUST_DIR;
const { instantiateFaustModuleFromFile, LibFaust, FaustCompiler, FaustMonoDspGenerator } = await import(pathToFileURL(join(faust,'dist/esm/index.js')));
const compiler = new FaustCompiler(new LibFaust(await instantiateFaustModuleFromFile(join(faust,'libfaust-wasm/libfaust-wasm.js'))));
const block = (rate,t) => Math.ceil(rate*t/128)*128;
const fill = (n,v) => new Float32Array(n).fill(v);
const hz = st => 440*2**(st/12);
const peak = x => x.reduce((m,v)=>{assert.ok(Number.isFinite(v),'non-finite output');return Math.max(m,Math.abs(v));},0);
const rms = x => Math.sqrt(x.reduce((s,v)=>s+v*v,0)/x.length);
// Evaluate shortest strong autocorrelation period, not merely a harmonic close to the target.
function estimatePitch(x, rate, expected, start, end) {
    const min=Math.floor(rate/(expected*2.2)), max=Math.ceil(rate/(expected*.45)), corr=[];
    for(let lag=min-1;lag<=max+1;lag++){
        let xy=0,xx=0,yy=0;
        for(let i=start;i<end-lag;i+=3){const u=x[i],v=x[i+lag];xy+=u*v;xx+=u*u;yy+=v*v;}
        corr[lag]=xy/Math.sqrt(xx*yy||1);
    }
    let best=min; for(let i=min;i<=max;i++)if(corr[i]>corr[best])best=i;
    const lag=Array.from({length:max-min+1},(_,i)=>i+min).find(i=>corr[i]>.75&&corr[i]>=.98*corr[best]&&corr[i]>corr[i-1]&&corr[i]>=corr[i+1]);
    if(lag===undefined)return null;
    const a=corr[lag-1],b=corr[lag],c=corr[lag+1],delta=.5*(a-c)/(a-2*b+c||1);
    return rate/(lag+Math.max(-.5,Math.min(.5,delta)));
}
async function render(rate,factory,inputs,controls={}){
    const dsp=await new FaustMonoDspGenerator().createOfflineProcessor(rate,128,{...factory,soundfiles:{}});
    try{for(const [key,value] of Object.entries(controls)){const p=dsp.getParams().find(p=>p.endsWith('/'+key));assert.ok(p,key);dsp.setParamValue(p,value);}
        return dsp.render(inputs,inputs[0].length)[0];
    }finally{dsp.destroy();}
}
const cents=(actual,expected)=>1200*Math.log2(actual/expected);
for(const kind of ['clarinet','flute']){
    const fixture=JSON.parse(await readFile(join(root,`generator.physical-${kind}.json`),'utf8'));
    const direct=await compiler.createMonoDSPFactory(`Direct_${kind.replace('-','_')}`,fixture.direct,'-single');
    const graph=await compiler.createMonoDSPFactory(`Graph_${kind.replace('-','_')}`,fixture.source,'-single');
    for(const rate of [44100,48000]){
        test(`${kind}: chromatic tuning, expression, idle and release at ${rate}`,async()=>{
            let worst=0,minRms=1,maxPeak=0;
            const n=block(rate,1.5),on=block(rate,.08),off=block(rate,1.05);
            // Every semitone, independent cross-product of dynamics and character.
            for(let st=-21;st<=15;st++)for(const exp of [.25,.65,1])for(const color of [0,.5,1]){
                const args=[st,0,exp,color,.055,.10,0,5,1].map(v=>fill(n,v)); args[1].fill(1,on,off);
                const y=await render(rate,direct,args),f=hz(st),active=y.subarray(block(rate,.65),block(rate,1));
                const actual=estimatePitch(y,rate,f,block(rate,.65),block(rate,1));
                const error=actual===null?Infinity:Math.abs(cents(actual,f));
                worst=Math.max(worst,error);minRms=Math.min(minRms,rms(active));maxPeak=Math.max(maxPeak,peak(y));
                assert.equal(peak(y.subarray(0,on)),0);
                assert.ok(peak(y.subarray(off+block(rate,.12)))<1e-7);
                assert.ok(rms(active)>.004,`${kind} ${rate} ${st} ${exp} ${color}: weak signal ${rms(active)}`);
                assert.ok(error<12,`${kind} ${rate} ${st} ${exp} ${color}: ${actual} Hz vs ${f} (${error} cents)`);
                assert.ok(peak(y)<.66);
            }
            console.log(`${kind} ${rate}: 333 trials; max pitch error ${worst.toFixed(2)} cents, minimum active RMS ${minRms.toFixed(4)}, max peak ${maxPeak.toFixed(4)}`);
        });
        test(`${kind}: moving expression, vibrato and short articulation at ${rate}`,async()=>{
            const n=block(rate,2.8), args=[0,0,.25,.5,.025,.10,0,5,1].map(v=>fill(n,v));
            args[1].fill(1,block(rate,.08),block(rate,.33));
            args[1].fill(1,block(rate,.60),block(rate,2.4));
            args[2].fill(.9,block(rate,1.15));
            args[6].fill(18,block(rate,1.65));
            const y=await render(rate,direct,args);
            assert.ok(peak(y)<.66);
            assert.ok(rms(y.subarray(block(rate,.20),block(rate,.33)))>.003);
            assert.ok(peak(y.subarray(block(rate,.46),block(rate,.60)))<1e-7);
            const quiet=rms(y.subarray(block(rate,.9),block(rate,1.1)));
            const loud=rms(y.subarray(block(rate,1.4),block(rate,1.6)));
            assert.ok(loud>quiet*1.5,`expression: ${quiet} -> ${loud}`);
            const pitches=[];
            for(let start=1.86;start<2.25;start+=.025){
                const f=estimatePitch(y,rate,440,block(rate,start),block(rate,start+.025));
                assert.ok(f!==null);
                pitches.push(cents(f,440));
            }
            assert.ok(Math.max(...pitches)-Math.min(...pitches)>12,'vibrato did not move pitch');
            assert.ok(pitches.every(p=>Math.abs(p)<35),'unexpected register during vibrato');
            assert.ok(peak(y.subarray(block(rate,2.53)))<1e-7);
        });
        test(`${kind}: Note Source legato, repeated notes, release, and velocity at ${rate}`,async()=>{
            assert.equal(fixture.inputs,1);
            const n=block(rate,3.7),actions=fill(n,0),queue=new RhythmicActionQueue([{isNote:true}]);
            const on=(note,v)=>note+1+512*v,off=note=>130+note;
            const sequence=[[.1,on(69,60)],[.8,on(76,110)],[1.5,off(76)],[2.2,off(69)],[2.65,on(69,100)],[3.25,off(69)]];
            for(const [time,word] of sequence){const frame=block(rate,time);assert.ok(queue.push(0,word));queue.render([actions.subarray(frame,frame+128)],0);}
            const y=await render(rate,graph,[actions],fixture.values);
            assert.equal(peak(y.subarray(0,block(rate,.1))),0);
            assert.ok(peak(y)<.66);
            for(const [start,end,note] of [[.55,.75,69],[1.25,1.45,76],[1.95,2.15,69],[3.02,3.2,69]]){
                const f=hz(note-69),actual=estimatePitch(y,rate,f,block(rate,start),block(rate,end));
                assert.ok(actual!==null&&Math.abs(cents(actual,f))<15,`${kind}: note ${note}, ${start}s, ${actual}`);
                assert.ok(rms(y.subarray(block(rate,start),block(rate,end)))>.003);
            }
            assert.ok(peak(y.subarray(block(rate,2.34),block(rate,2.65)))<1e-7);
            assert.ok(peak(y.subarray(block(rate,3.4)))<1e-7);
            assert.ok(rms(y.subarray(block(rate,3.02),block(rate,3.2)))>rms(y.subarray(block(rate,.55),block(rate,.75))));
        });
    }
}

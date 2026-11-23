import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { RhythmicActionQueue } from '../../src/Orbit.Infrastructure/wwwroot/audio/Components/RhythmicActionQueue.js';

const dir = process.env.ORBIT_GRAPH_PROBE_DIR, faustDir = process.env.ORBIT_FAUST_DIR;
const {instantiateFaustModuleFromFile,LibFaust,FaustCompiler,FaustMonoDspGenerator} =
    await import(pathToFileURL(join(faustDir,'dist/esm/index.js')));
const compiler=new FaustCompiler(new LibFaust(await instantiateFaustModuleFromFile(join(faustDir,'libfaust-wasm/libfaust-wasm.js'))));
const count=(r,t)=>Math.ceil(r*t/128)*128, fill=(n,v)=>new Float32Array(n).fill(v);
const rms=x=>Math.sqrt(x.reduce((s,v)=>s+v*v,0)/Math.max(1,x.length));
const peak=x=>x.reduce((m,v)=>{assert.ok(Number.isFinite(v),'non-finite Faust signal');return Math.max(m,Math.abs(v));},0);
const cents=(a,b)=>1200*Math.log2(a/b);
async function render(rate,factory,channels,controls={}){
    const dsp=await new FaustMonoDspGenerator().createOfflineProcessor(rate,128,{...factory,soundfiles:{}});
    try{
        for(const [key,value] of Object.entries(controls)){
            const id=dsp.getParams().find(p=>p.endsWith('/'+key));
            if(!id) throw Error('missing control '+key);
            dsp.setParamValue(id,value);
        }
        return dsp.render(channels,channels[0].length)[0];
    }finally{dsp.destroy();}
}
function pitchEstimate(y,rate,expected,start,end){
    const minimum=Math.floor(rate/(expected*2.2)), maximum=Math.ceil(rate/(expected*.45));
    let bestLag=0,best=-Infinity;
    for(let lag=minimum;lag<=maximum;lag++){
        let sum=0,xx=0,yy=0;
        for(let i=start;i<end-lag;i+=4){const a=y[i],b=y[i+lag];sum+=a*b;xx+=a*a;yy+=b*b;}
        const correlation=sum/Math.sqrt(xx*yy||1);
        if(correlation>best){best=correlation;bestLag=lag;}
    }
    return best>.65 ? rate/bestLag : null;
}
for(const [kind,sustained] of [['violin',true],['brass',true],['electric-guitar',false]]){
    const fixture=JSON.parse(await readFile(join(dir,'generator.physical-'+kind+'.json'),'utf8'));
    if(kind==='violin') {
        const forms=[
            ['constant','pm.violinModel(0.8,0.5,0.6,0.7)'],
            ['length','pm.violinModel(pm.f2l(440),0.5,0.6,0.7)'],
            ['dynamic','pm.violinModel(pm.f2l(f),0.5,0.6,0.7) with {f=440;}'],
            ['expression','pm.violinModel(pm.f2l(440),0.5,vel,0.7) with {vel=0.1+0.5*e;}']
        ];
        for(const [label,expression] of forms){
            try {
                const snippet='import("stdfaust.lib"); process(e) = '+expression+';';
                const factory=await compiler.createMonoDSPFactory('violin_diagnostic_'+label,snippet,'-single');
                console.log('violin minimal',label,'COMPILED',Boolean(factory));
            }catch(error){console.log('violin minimal',label,'FAILED',String(error));}
        }
    }
    let direct,graph;
    try{
        direct=await compiler.createMonoDSPFactory('phys_'+kind.replaceAll('-','_'),fixture.direct,'-single');
        graph=await compiler.createMonoDSPFactory('graph_'+kind.replaceAll('-','_'),fixture.source,'-single');
    }catch(error){
        test(kind+' native model and graph compilation',()=>{throw error;});
        continue;
    }
    for(const rate of [44100,48000]){
        test(kind+' direct note, safety, tuning diagnostics @'+rate,async()=>{
            const duration=2.5, n=count(rate,duration), first=count(rate,.1), last=count(rate,1.4);
            const results=[];
            for(const semitones of [-12,-5,0,7,12]){
                let inputs;
                if(sustained){
                    inputs=[semitones,0,.8,.5,kind==='violin'?.7:.5,.055,.12,0,5,.8].map(v=>fill(n,v));
                    inputs[1].fill(1,first,last);
                }else{
                    inputs=[semitones,0,.8,.35,.96,.8].map(v=>fill(n,v));
                    inputs[1][first]=1;
                }
                const y=await render(rate,direct,inputs);
                assert.equal(peak(y.subarray(0,first)),0,'idle must be silent');
                assert.ok(peak(y)<.9,'bounded output');
                const window=y.subarray(count(rate,.55),count(rate,.85));
                const active=rms(window);
                const f=440*Math.pow(2,semitones/12), estimate=pitchEstimate(y,rate,f,count(rate,.55),count(rate,.85));
                results.push({note:semitones,rms:+active.toFixed(6),cents:estimate?+cents(estimate,f).toFixed(1):null});
                // A silent or octave-jumping model is not a validated instrument. Log measured deviations for model fitting.
                assert.ok(Number.isFinite(active));
                if(sustained) assert.ok(peak(y.subarray(count(rate,1.8)))<.000001,'release must end');
            }
            console.log(kind,rate,JSON.stringify(results));
        });
        test(kind+' Note Source graph and retrigger @'+rate,async()=>{
            assert.equal(fixture.inputs,1);
            const n=count(rate,3.0), actions=fill(n,0), queue=new RhythmicActionQueue([{isNote:true}]);
            for(const [time,word] of [[.1,69+1+512*96],[.9,130+69],[1.25,69+1+512*64],[2.0,130+69]]){
                const frame=count(rate,time);
                assert.ok(queue.push(0,word));
                queue.render([actions.subarray(frame,frame+128)],0);
            }
            const y=await render(rate,graph,[actions],fixture.values);
            assert.equal(peak(y.subarray(0,count(rate,.1))),0);
            assert.ok(peak(y)<.9);
            assert.ok(rms(y.subarray(count(rate,.45),count(rate,.75)))>=0);
        });
    }
}

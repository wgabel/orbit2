// Offline research only. No generated DSP binaries are published by this script.
import { createRequire } from "node:module";
import { createHash } from "node:crypto";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { performance } from "node:perf_hooks";
import { arch, cpus, platform, release } from "node:os";

const [packagePath, outputPath] = process.argv.slice(2);
if (!packagePath || !outputPath) throw Error("Usage: node physical-model-probes.mjs <faustwasm package directory> <output directory>");
const pkg = resolve(packagePath), out = resolve(outputPath);
const manifest = JSON.parse(await readFile(resolve(pkg, "package.json"), "utf8"));
const bundle = resolve(pkg, "dist/esm-bundle/index.js");
const sha = createHash("sha256").update(await readFile(bundle)).digest("hex");
if (manifest.name !== "@grame/faustwasm" || manifest.version !== "0.19.0" ||
    sha !== "c602b02f0d69f09c67d7b2e8f135cc3067d10da6b4c4c80fb2c0278132ae5912")
  throw Error("Use the exact audited Faust bundle");
globalThis.require = createRequire(import.meta.url);
globalThis.__dirname = resolve(pkg, "dist/esm-bundle");
const { instantiateFaustModule, LibFaust, FaustCompiler, FaustMonoDspGenerator } =
  await import(pathToFileURL(bundle));
const lib = new LibFaust(await instantiateFaustModule());
const compiler = new FaustCompiler(lib);
const models = {
  pluck: 'process(f,d,e) = pm.ks(pm.f2l(f),d,e);',
  flute: 'process(f,m,p) = pm.fluteModel(pm.f2l(f),m,p);'
};
const notes = [220, 261.625565, 329.627557, 440, 523.251131, 659.255114];
const report = {compiler:lib.version(), bundleSha256:sha, blockSize:128,
  environment:{node:process.version, platform:platform(), release:release(), arch:arch(), cpu:cpus()[0]?.model},
  notes, models:{}, rates:{}};
const generators = {};
for (const [name, expression] of Object.entries(models)) {
  const g = new FaustMonoDspGenerator();
  if (!await g.compile(compiler, name, 'import("stdfaust.lib");\n'+expression, "-single"))
    throw Error("Compilation failed: "+name);
  const meta = g.getMeta();
  if (meta.inputs !== 3 || meta.outputs !== 1) throw Error("Unexpected ports");
  report.models[name]={expression, libraries:meta.library_list, meta:meta.meta};
  generators[name]=g;
}
const round = (x,n=4) => +x.toFixed(n);
const frames = (sr,t) => Math.round(sr*t);
function rng(seed=123) {
  return () => {seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/2**31-1;};
}
function rms(x,start=0,end=x.length) {
  let sum=0;for(let i=start;i<end;i++)sum+=x[i]**2;return Math.sqrt(sum/(end-start));
}
function peak(x) {let p=0;for(const v of x){if(!Number.isFinite(v))throw Error("Non-finite sample");p=Math.max(p,Math.abs(v));}return p;}
// Estimate the period closest to the requested fundamental, not the strongest spectral harmonic.
// Reject weak periodicity rather than report noise as pitched audio.
function pitch(x,sr,target,start,end) {
  const lo=Math.max(2,Math.floor(sr/(target*1.7))),hi=Math.ceil(sr/(target*.65));
  const scores=[];
  for(let lag=lo-1;lag<=hi+1;lag++){
    let xy=0,xx=0,yy=0;
    for(let i=start;i<end-lag;i+=2){const a=x[i],b=x[i+lag];xy+=a*b;xx+=a*a;yy+=b*b;}
    scores[lag]=xy/Math.sqrt(xx*yy||1);
  }
  const candidates=[];
  for(let lag=lo;lag<=hi;lag++)if(scores[lag]>=scores[lag-1]&&scores[lag]>=scores[lag+1])candidates.push(lag);
  const strongest=Math.max(-1,...candidates.map(i=>scores[i]));
  // Prefer the shortest near-perfect recurrence: multiples of the period can
  // score slightly better than the actual period, especially in decaying notes.
  const lag=candidates.find(i=>scores[i]>=Math.max(.75,strongest*.985));
  if(lag===undefined||rms(x,start,end)<1e-5)return null;
  const a=scores[lag-1],b=scores[lag],c=scores[lag+1],den=a-2*b+c;
  const offset=den ? Math.max(-.5,Math.min(.5,.5*(a-c)/den)):0;
  return sr/(lag+offset);
}
function excitation(name,sr,f,control,expression=false){
  const n=frames(sr,2),a=new Float32Array(n).fill(f),b=new Float32Array(n).fill(name==="pluck"?control:.5),c=new Float32Array(n),noise=rng();
  for(let i=frames(sr,.1);i<frames(sr,name==="pluck"?.106:1.15);i++){
    const t=i/sr-.1;
    if(name==="pluck")c[i]=noise()*.08*Math.sin(Math.PI*t/.006);
    else {
      const shape=Math.min(1,t/.06,Math.max(0,(1.05-t)/.10));
      const pressure=expression?control+.035*Math.sin(2*Math.PI*3*t):control;
      c[i]=shape*pressure*(1+.02*noise());
    }
  }
  return [a,b,c];
}
async function render(name,sr,inputs){
  const processor=await generators[name].createOfflineProcessor(sr,128);
  try{const start=performance.now();const y=processor.render(inputs,inputs[0].length)[0];
    const ms=performance.now()-start;peak(y);return {y,ms};
  }finally{processor.destroy();}
}
const pitchWindow=(name,sr)=>name==="pluck"?[frames(sr,.13),frames(sr,.35)]:[frames(sr,.75),frames(sr,1.05)];
const estimate=(name,y,sr,target)=>pitch(y,sr,target,...pitchWindow(name,sr));
function measure(name,y,sr,f){
  const p=estimate(name,y,sr,f);
  const mid=rms(y,frames(sr,.75),frames(sr,1.05));
  const early=rms(y,frames(sr,.13),frames(sr,.25));
  return {pitchHz:p===null?null:round(p,2),cents:p===null?null:round(1200*Math.log2(p/f),2),
    peak:round(peak(y),6), idlePeak:round(peak(y.subarray(0,frames(sr,.1))),8),
    earlyRms:round(early,6),holdRms:round(mid,6),
    endRms:round(rms(y,frames(sr,1.85)),8)};
}
// Fit a single solver input independently for each test note, not a playable keyboard calibration.
async function calibrate(name,sr,target,control,rawPitch){
  if(rawPitch!==null&&Math.abs(1200*Math.log2(rawPitch/target))<1)return {f:target};
  let lo=target*.65,hi=target*1.55,best=null;
  // Find a valid bracket even when the model fails to oscillate at one test point.
  const grid=[];
  for(let i=0;i<=12;i++){
    const f=lo+(hi-lo)*i/12;
    const {y}=await render(name,sr,excitation(name,sr,f,control));
    const p=estimate(name,y,sr,target);
    if(p===null)continue;
    const error=Math.abs(1200*Math.log2(p/target));
    if(!best||error<best.error)best={f,error};
    grid.push({f,p});
  }
  const brackets=grid.slice(1).map((b,i)=>[grid[i],b]).filter(([a,b])=>a.p<=target&&b.p>=target);
  if(!brackets.length)return best;
  [lo,hi]=brackets.sort((a,b)=>Math.abs(a[0].p-target)+Math.abs(a[1].p-target)-Math.abs(b[0].p-target)-Math.abs(b[1].p-target))[0].map(x=>x.f);
  for(let i=0;i<12;i++){
    const f=(lo+hi)/2;
    const result=await render(name,sr,excitation(name,sr,f,control));
    const p=estimate(name,result.y,sr,target);
    if(p===null)break;
    const error=Math.abs(1200*Math.log2(p/target));
    if(!best||error<best.error)best={f,error};
    if(error<1)break;
    if(p<target)lo=f;else hi=f;
  }
  return best;
}
// Dry independently implemented synthetic references, not exact graph renders:
// flute follows InstrumentTemplates.cs partials/filtered noise; pluck follows felt-like pluck recipe.
function reference(name,sr,f){
  const y=new Float32Array(frames(sr,2)),noise=rng();
  const w=2*Math.PI*1800/sr,alpha=Math.sin(w)/(2*2),a0=1+alpha;
  let x1=0,x2=0,z1=0,z2=0;
  for(let i=frames(sr,.1);i<y.length;i++){
    const t=i/sr-.1,phase=2*Math.PI*f*t;
    if(name==="flute"){
      const x=noise(),z=alpha/a0*(x-x2)+2*Math.cos(w)/a0*z1-(1-alpha)/a0*z2;
      x2=x1;x1=x;z2=z1;z1=z;
      const env=t<.065?t/.065:t<.245?1-.22*(t-.065)/.18:t<1.05?.78:.78*Math.max(0,1-(t-1.05)/.19);
      y[i]=env*(.30*Math.sin(phase)+.055*Math.sin(2*phase)+.018*2/Math.PI*Math.asin(Math.sin(3*phase))+.025*z);
    }else{
      const env=(attack,decay)=>t<attack?t/attack:Math.max(0,1-(t-attack)/decay);
      y[i]=.10*2/Math.PI*Math.asin(Math.sin(phase))*env(.007,.750)+.03*Math.sin(2*phase)*env(.003,.240)+.013*Math.sin(3*phase)*env(.002,.100);
    }
  }
  return y;
}
function matchedPair(a,b,sr,name){
  // One gain per complete voice; compare the first 1.15 seconds of all notes, excluding idle/tail.
  const active=(xs)=>Math.sqrt(xs.reduce((v,x)=>v+rms(x,frames(sr,.1),frames(sr,1.15))**2,0)/xs.length);
  const ar=active(a),br=active(b);
  if(!ar||!br)throw Error("Silent comparison: "+name);
  const target=Math.min(.075,.85*ar/Math.max(...a.map(peak)),.85*br/Math.max(...b.map(peak)));
  return {gains:[target/ar,target/br],target};
}
function wav(samples,sr){
  const b=Buffer.alloc(44+samples.length*2);b.write("RIFF");b.writeUInt32LE(b.length-8,4);
  b.write("WAVEfmt ",8);b.writeUInt32LE(16,16);b.writeUInt16LE(1,20);b.writeUInt16LE(1,22);
  b.writeUInt32LE(sr,24);b.writeUInt32LE(sr*2,28);b.writeUInt16LE(2,32);b.writeUInt16LE(16,34);
  b.write("data",36);b.writeUInt32LE(samples.length*2,40);
  for(let i=0;i<samples.length;i++)b.writeInt16LE(Math.round(Math.max(-1,Math.min(1,samples[i]))*32767),44+i*2);
  return b;
}
await mkdir(out,{recursive:true});
let audition = [];
for(const sr of [44100,48000]){
  report.rates[sr]={};
  for(const name of Object.keys(models)){
    const control=name==="pluck"?.04:.85;
    const rows=[],physical=[],synthetic=[];
    for(const f of notes){
      const raw=await render(name,sr,excitation(name,sr,f,control));
      const fit=await calibrate(name,sr,f,control,estimate(name,raw.y,sr,f));
      const input=fit?.f??f;
      const calibrated=await render(name,sr,excitation(name,sr,input,control));
      const expressed=await render(name,sr,excitation(name,sr,input,name==="pluck"?.10:control,true));
      const m=measure(name,calibrated.y,sr,f);
      if(m.idlePeak>1e-7||m.peak<1e-5||m.peak>10)throw Error("Invalid model output");
      rows.push({targetHz:f,raw:measure(name,raw.y,sr,f),fitInputHz:round(input,3),calibrated:m,
        expression:measure(name,expressed.y,sr,f),renderMs:round(calibrated.ms,3)});
      physical.push(name==="flute"?expressed.y:calibrated.y);synthetic.push(reference(name,sr,f));
    }
    const match=matchedPair(synthetic,physical,sr,name);
    const center=rows[3].fitInputHz;
    const dynamic=excitation(name,sr,center,control);
    if(name==="pluck"){
      // Second identical excitation into a still-ringing string.
      dynamic[2].set(dynamic[2].slice(frames(sr,.1),frames(sr,.106)),frames(sr,.8));
    }else{
      // Smooth pitch sweep while sounding, without reconstructing the factory.
      const end=rows[4].fitInputHz;
      for(let i=frames(sr,.4);i<dynamic[0].length;i++)
        dynamic[0][i]=center+(end-center)*Math.min(1,(i/sr-.4)/.3);
    }
    const d=await render(name,sr,dynamic);
    const eventRms=rms(d.y,frames(sr,.82),frames(sr,1.0));
    if(eventRms<1e-5||peak(d.y)>10)throw Error("Invalid dynamic exercise");
    const times=[];
    for(let i=0;i<6;i++){
      const r=await render(name,sr,excitation(name,sr,center,control));
      if(i)times.push(r.ms);
    }
    times.sort((a,b)=>a-b);
    const sweep=[];
    if(name==="flute")for(const pressure of [.5,.7,.75,.85,.9]){
      const r=await render(name,sr,excitation(name,sr,center,pressure));
      sweep.push({pressure,...measure(name,r.y,sr,440)});
    }
    report.rates[sr][name]={control,rows,comparison:{targetRms:match.target,gains:match.gains},
      dynamic:{kind:name==="pluck"?"retrigger":"frequency ramp",peak:peak(d.y),eventRms,
        latePitchHz:name==="flute"?pitch(d.y,sr,notes[4],frames(sr,.82),frames(sr,1.0)):null},
      pressureSweep:sweep,offlineRenderMs:{repeats:5,median:round(times[2],3),min:round(times[0],3),max:round(times[4],3)}};
    if(sr===48000){
      // Group order: synthetic, physical (same six notes). Only trim the final 20 ms of each isolated note for edit boundaries.
      for(const [j,group] of [synthetic,physical].entries()){
        const start=audition.length/sr;
        for(const x of group)for(let i=0;i<x.length;i++)audition.push(x[i]*match.gains[j]*Math.min(1,(x.length-1-i)/frames(sr,.02)));
        report.rates[sr][name].comparison[j===0?"syntheticStartSeconds":"physicalStartSeconds"]=start;
        audition.push(...new Float32Array(sr));
      }
    }
  }
}
await writeFile(resolve(out,"physical-model-audition.wav"),wav(audition,48000));
await writeFile(resolve(out,"physical-model-report.json"),JSON.stringify(report,null,2)+"\n");
console.log("Wrote report and audition to",out);

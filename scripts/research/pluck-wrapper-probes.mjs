// Pinned physical pluck research, not a product integration test.
import { createRequire } from "node:module";
import { createHash } from "node:crypto";
import { readFile, writeFile, mkdir } from "node:fs/promises";
import { resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { performance } from "node:perf_hooks";
import { arch, cpus, platform } from "node:os";

const [packageDir, outputDir] = process.argv.slice(2);
if (!packageDir || !outputDir) throw Error("Usage: node pluck-wrapper-probes.mjs <faustwasm package directory> <output directory>");
const pkg=resolve(packageDir),out=resolve(outputDir);
const bundle=resolve(pkg,"dist/esm-bundle/index.js");
const fingerprint=createHash("sha256").update(await readFile(bundle)).digest("hex");
if(fingerprint!=="c602b02f0d69f09c67d7b2e8f135cc3067d10da6b4c4c80fb2c0278132ae5912")throw Error("Use the audited 0.19.0 bundle");
globalThis.require=createRequire(import.meta.url);
globalThis.__dirname=resolve(pkg,"dist/esm-bundle");
const {instantiateFaustModule,LibFaust,FaustCompiler,FaustMonoDspGenerator}=await import(pathToFileURL(bundle));
const lib=new LibFaust(await instantiateFaustModule());
const compiler=new FaustCompiler(lib),generator=new FaustMonoDspGenerator();
const source=await readFile(new URL("./pluck-wrapper.dsp",import.meta.url),"utf8");
if(!await generator.compile(compiler,"physical_pluck_research",source,"-single"))throw Error("Compile failed");
const meta=generator.getMeta();
if(meta.inputs!==5||meta.outputs!==1)throw Error("Unexpected ports");
const sha256=x=>createHash("sha256").update(x).digest("hex");
const report={compiler:lib.version(),bundleSha256:fingerprint,sourceSha256:sha256(source),
  node:process.version,platform:platform(),arch:arch(),cpu:cpus()[0]?.model,
  libraryHashes:Object.fromEntries(meta.library_list.map(p=>[p,sha256(lib.fs().readFile(p))])),
  metadata:meta.meta,rates:{}};
const frames=(sr,t)=>Math.round(sr*t);
const hz=p=>440*2**(p/12);
const peak=x=>{let m=0;for(const v of x){if(!Number.isFinite(v))throw Error("non-finite sample");m=Math.max(m,Math.abs(v));}return m;};
const rms=(x,a,b)=>Math.sqrt(x.subarray(a,b).reduce((s,v)=>s+v*v,0)/(b-a));
const round=x=>+x.toFixed(6);
const assert=(c,msg)=>{if(!c)throw Error(msg);};
function inputs(sr,seconds=3,pitch=0,vel=1,t60=1){
  const n=frames(sr,seconds),a=Array.from({length:5},()=>new Float32Array(n));
  a[0].fill(pitch);a[2].fill(vel);a[3].fill(t60);return a;
}
async function render(sr,a){
  const p=await generator.createOfflineProcessor(sr,128);
  try{const start=performance.now(),y=p.render(a,a[0].length)[0],ms=performance.now()-start;peak(y);return {y,ms};}
  finally{p.destroy();}
}
// Correlation near expected pitch; no per-note calibration or tuning table.
function frequency(x,sr,f,a,b){
  const low=Math.floor(sr/(1.035*f)),high=Math.ceil(sr/(.965*f)),r=[];
  for(let l=low-1;l<=high+1;l++){
    let xy=0,xx=0,yy=0;
    for(let i=a;i<b-l;i++){const u=x[i],v=x[i+l];xy+=u*v;xx+=u*u;yy+=v*v;}
    r[l]=xy/Math.sqrt(xx*yy||1);
  }
  let l=low;
  for(let i=low+1;i<=high;i++)if(r[i]>r[l])l=i;
  if(r[l]<.75||r[l]<r[l-1]||r[l]<r[l+1])return null;
  const d=r[l-1]-2*r[l]+r[l+1],delta=d?.5*(r[l-1]-r[l+1])/d:0;
  return sr/(l+Math.max(-.5,Math.min(.5,delta)));
}
// Fit the fundamental amplitude (Hann-windowed projection at measured f);
// this is a decay slope extrapolation, not broadband RT60.
function fundamental(x,sr,f,center,seconds){
  const n=frames(sr,seconds),start=frames(sr,center)-Math.floor(n/2);
  let re=0,im=0,weight=0;
  for(let i=0;i<n;i++){
    const w=.5-.5*Math.cos(2*Math.PI*i/(n-1)),ang=2*Math.PI*f*(start+i)/sr;
    re+=w*x[start+i]*Math.cos(ang);im+=w*x[start+i]*Math.sin(ang);weight+=w;
  }
  return 2*Math.hypot(re,im)/weight;
}
function estimateDecay(x,sr,f,t60){
  const window=Math.max(.045,8/f),start=.1+window*.5+.03,end=start+.6*t60;
  let sx=0,sy=0,sxx=0,sxy=0,n=0;
  for(let k=0;k<7;k++){
    const t=start+(end-start)*k/6,amp=fundamental(x,sr,f,t,window);
    if(amp<1e-8)return null;
    const y=Math.log(amp);sx+=t;sy+=y;sxx+=t*t;sxy+=t*y;n++;
  }
  const slope=(n*sxy-sx*sy)/(n*sxx-sx*sx);
  return slope<0?-Math.log(1000)/slope:null;
}
function equal(a,b,start=0,end=a.length){for(let i=start;i<end;i++)if(a[i]!==b[i])return false;return true;}
for(const sr of [44100,48000]){
  const rows=[];
  for(let p=-21;p<=15;p++)for(const t60 of [.3,1,2]){
    const a=inputs(sr,3,p,.8,t60),strike=frames(sr,.1);
    a[1][strike]=1;
    const {y}=await render(sr,a),f0=hz(p);
    const f=frequency(y,sr,f0,frames(sr,.14),frames(sr,.23));
    const t=f?estimateDecay(y,sr,f,t60):null;
    const row={semitones:p,targetHz:round(f0),t60,
      cents:f===null?null:round(1200*Math.log2(f/f0)),estimatedT60:t===null?null:round(t),
      peak:round(peak(y)),idle:peak(y.subarray(0,strike)),tailRms:round(rms(y,frames(sr,2.8),y.length))};
    assert(row.idle===0&&row.peak>0&&row.peak<1,"invalid isolated output "+JSON.stringify(row));
    assert(row.cents!==null&&Math.abs(row.cents)<=1,"pitch failure "+JSON.stringify(row));
    assert(row.estimatedT60!==null&&Math.abs(row.estimatedT60/t60-1)<=.03,"decay failure "+JSON.stringify(row));
    rows.push(row);
  }
  // Sample-aligned velocity and pitch latch: compare unchanged outputs after
  // changing live inputs during the burst and tail; second trigger uses new values.
  const a=inputs(sr,2,0,.8,1),first=frames(sr,.1),second=frames(sr,.8);
  a[1][first]=a[1][second]=1;
  const b=a.map(x=>x.slice());
  b[2].fill(.1,first+1,second);b[0].fill(7,first+1,second);
  const y=(await render(sr,a)).y,z=(await render(sr,b)).y;
  assert(equal(y,z),"untriggered pitch or velocity changed tail");
  const c=a.map(x=>x.slice());c[0].fill(7,second);c[2].fill(.4,second);
  const zz=(await render(sr,c)).y;
  assert(equal(y,zz,0,second)&&!equal(y,zz,second),"trigger capture failed");
  const newPitch=frequency(zz,sr,hz(7),frames(sr,1),frames(sr,1.2));
  assert(newPitch!==null&&Math.abs(1200*Math.log2(newPitch/hz(7)))<=1,"second trigger pitch");
  // No trigger + no external drive must stay exactly silent, even if pitch changes.
  const idle=inputs(sr,1,-21,1,2);idle[0].fill(15,frames(sr,.5));
  assert(peak((await render(sr,idle)).y)===0,"phantom strike");
  const zero=inputs(sr,1,0,0,1);zero[1][first]=1;
  assert(peak((await render(sr,zero)).y)===0,"zero velocity strike");
  const one=inputs(sr,1,0,1,1);one[1][first]=1;
  const half=one.map(x=>x.slice());half[2].fill(.5);
  const yy=(await render(sr,one)).y,hh=(await render(sr,half)).y;
  assert(equal(yy.map(x=>x*.5),hh),"nonlinear isolated velocity");
  // Continuous resonant external excitation without a trigger; initial pitch captured.
  const external=inputs(sr,3,-21,0,2);
  for(let i=0;i<external[4].length;i++)external[4][i]=Math.sin(2*Math.PI*hz(-21)*i/sr)*4;
  const ext=(await render(sr,external)).y;
  assert(peak(ext)>0&&peak(ext)<1,"external input not bounded");
  assert(Math.abs(1200*Math.log2(frequency(ext,sr,hz(-21),frames(sr,1),frames(sr,1.5))/hz(-21)))<=1,
    "external initial pitch");
  // Oversized modulation clamps exactly to the auditable supported bounds.
  for(const [p,t,edgeP,edgeT] of [[-80,-5,-21,.3],[80,8,15,2]]){
    const wide=inputs(sr,1,p,5,t),edge=inputs(sr,1,edgeP,1,edgeT);
    wide[1][first]=edge[1][first]=1;
    assert(equal((await render(sr,wide)).y,(await render(sr,edge)).y),"clamp mismatch");
  }
  // Adversarial but finite input: rapid trigger/retrigger, clamped ranges,
  // loud external audio and changing decay. Not a proof for arbitrary streams.
  const stress=inputs(sr,5,0,1,1);
  for(let i=0;i<stress[0].length;i++){
    stress[0][i]=Math.sin(2*Math.PI*.7*i/sr)*80;
    stress[2][i]=1.5;
    stress[3][i]=1.2+2*Math.sin(2*Math.PI*3*i/sr);
    stress[4][i]=(i%2?3:-3);
    if(i%frames(sr,.013)===0)stress[1][i]=1;
  }
  const sy=(await render(sr,stress)).y;
  const stressPeak=peak(sy);
  assert(stressPeak<1,"stress overloaded output");
  const measurements=rows.filter(x=>x.cents!==null&&x.estimatedT60!==null);
  const summary={count:rows.length,pitchMeasured:rows.filter(x=>x.cents!==null).length,
    maxPitchErrorCents:Math.max(...rows.filter(x=>x.cents!==null).map(x=>Math.abs(x.cents))),
    decayMeasured:measurements.length,maxRelativeDecayError:Math.max(...measurements.map(x=>Math.abs(x.estimatedT60/x.t60-1))),
    maxPeak:Math.max(...rows.map(x=>x.peak))};
  const timing=[];
  for(let i=0;i<6;i++){const r=await render(sr,a);if(i)timing.push(r.ms);}
  timing.sort((a,b)=>a-b);
  report.rates[sr]={summary,rows,tests:{pitchVelocityLatch:true,secondTriggerPitchHz:newPitch,silentIdle:true,zeroVelocity:true,
    halfVelocityExact:true,rangeClamps:true,externalPeak:peak(ext),stressPeak,median2SecMs:timing[2]}};
}
await mkdir(out,{recursive:true});
await writeFile(resolve(out,"pluck-wrapper-report.json"),JSON.stringify(report,null,2)+"\n");
console.log(JSON.stringify(Object.fromEntries(Object.entries(report.rates).map(([rate,x])=>[rate,{...x.summary,...x.tests}])),null,2));

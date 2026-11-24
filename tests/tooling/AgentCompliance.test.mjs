import test from 'node:test';
import assert from 'node:assert/strict';
import { validateRouting, extractReport, validateReport } from '../../scripts/agent-compliance.mjs';

const config = {
  "schemaVersion": 1,
  "alwaysRequired": [
    "AGENTS.md",
    "docs/agents/contributor-instructions.md",
    "docs/README.md",
    "docs/agents/task-routing.json",
    "docs/agents/compliance-reports.md"
  ],
  "routes": [
    {
      "id": "implementation",
      "when": "implementation",
      "required": [
        "docs/code.md"
      ],
      "recommended": [],
      "reference": [],
      "pathPrefixes": [
        "src/",
        "scripts/"
      ]
    },
    {
      "id": "documentation",
      "when": "documentation",
      "required": [
        "docs/conventions.md"
      ],
      "recommended": [],
      "reference": [],
      "pathPrefixes": [
        "docs/"
      ],
      "pathFiles": [
        "AGENTS.md"
      ]
    },
    {
      "id": "roadmap",
      "when": "roadmap",
      "required": [
        "docs/plan.md"
      ],
      "recommended": [],
      "reference": []
    },
    {
      "id": "dsp",
      "when": "dsp",
      "required": [
        "docs/dsp.md"
      ],
      "recommended": [],
      "reference": [],
      "pathPrefixes": [
        "src/audio/"
      ]
    },
    {
      "id": "gesture",
      "when": "gesture",
      "required": [
        "docs/gestures.md"
      ],
      "recommended": [],
      "reference": []
    },
    {
      "id": "novel-topic",
      "when": "novel-topic",
      "required": [
        "docs/novel.md"
      ],
      "recommended": [],
      "reference": []
    }
  ]
};
const alwaysRequired = config.alwaysRequired;
const exists = p=>alwaysRequired.includes(p)||config.routes.some(r=>r.required.includes(p));
const report = ()=>({schemaVersion:1,routes:["implementation","dsp","documentation"],consulted:[...alwaysRequired,"docs/code.md","docs/dsp.md","docs/conventions.md"],revision:"a".repeat(40),constraints:["Preserve contracts"],verification:{status:"not-run",checks:[],testedRevision:null},limitations:["CI pending"],discoverability:"No issues found.",exceptions:[]});

test("valid routing and additive report", ()=>{assert.deepEqual(validateRouting(config,exists),[]);assert.deepEqual(validateReport(config,report(),["src/audio/voice.js"]),[])});
test("invalid routing paths and duplicate IDs", ()=>{const c=JSON.parse(JSON.stringify(config));c.routes[0].required.push("../outside.md","docs/missing.md");c.routes.push(c.routes[0]);const e=validateRouting(c,exists);assert.ok(e.some(x=>x.includes("Duplicate route")));assert.ok(e.some(x=>x.includes("../outside.md")));assert.ok(e.some(x=>x.includes("docs/missing.md")))});
test("path-derived routes and consultation", ()=>{const r=report();r.routes=["documentation"];r.consulted=["docs/conventions.md"];const e=validateReport(config,r,["src/audio/voice.js"]);for(const s of ["require route: implementation","require route: dsp","AGENTS.md","docs/dsp.md"])assert.ok(e.some(x=>x.includes(s)))});
test("semantic routes require documents independently", ()=>{const r=report();r.routes.push("gesture");assert.ok(validateReport(config,r,[]).some(e=>e.includes("docs/gestures.md")))});
test("invalid route and report fields", ()=>{const r=report();r.routes.push("unknown");r.consulted.push(r.consulted[0]);r.revision="main";r.constraints=[];assert.ok(validateReport(config,r,[]).length>=4)});
test("documentation waiver cannot cover code", ()=>{const r=report();r.verification={status:"documentation-only-waived",checks:[],testedRevision:null};assert.deepEqual(validateReport(config,r,["docs/README.md"]),[]);assert.ok(validateReport(config,r,["scripts/build.mjs"]).some(e=>e.includes("waiver")))});
test("exceptions do not bypass required documents", ()=>{const r=report();r.consulted=r.consulted.filter(p=>p!=="docs/dsp.md");r.exceptions=[{path:"docs/dsp.md",reason:"missing",authorization:"owner note"}];assert.ok(validateReport(config,r,[]).some(e=>e.includes("docs/dsp.md")))});
test("verification claims and limits", ()=>{const r=report();r.verification={status:"passed",checks:[],testedRevision:null};assert.ok(validateReport(config,r,[]).some(e=>e.includes("actual checks")));r.verification={status:"not-run",checks:[],testedRevision:null};r.limitations=[];assert.ok(validateReport(config,r,[]).some(e=>e.includes("limitation")))});
test("report parsing rejects missing, duplicate or malformed blocks", ()=>{const b='<!-- orbit2-agent-compliance:v1 -->\n```json\n'+JSON.stringify(report())+'\n```\n<!-- /orbit2-agent-compliance -->';assert.deepEqual(extractReport(b),report());for(const body of ["",b+"\n"+b,b.replace("json","text"),b.replace('"schemaVersion":1',"bad")])assert.throws(()=>extractReport(body))});
test("exact path and directory boundaries", ()=>{const r=report();r.routes=["novel-topic"];r.consulted=[...alwaysRequired,"docs/novel.md"];assert.deepEqual(validateReport(config,r,[]),[]);assert.ok(validateReport(config,r,["AGENTS.md"]).some(e=>e.includes("require route: documentation")));const e=validateReport(config,r,["src/audio-other/voice.js"]);assert.ok(e.some(x=>x.includes("require route: implementation")));assert.ok(!e.some(x=>x.includes("require route: dsp")))});

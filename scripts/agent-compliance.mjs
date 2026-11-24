function object(v){return v!==null&&typeof v==="object"&&!Array.isArray(v)}

function textValue(v){return typeof v==="string"&&v.trim().length>0}

function list(v){return Array.isArray(v)&&v.every(textValue)&&new Set(v).size===v.length}

function repoPath(v){return textValue(v)&&v===v.trim()&&!v.includes("\\")&&!/[\x00-\x1f\x7f:]/.test(v)&&v.split("/").every(p=>p!==""&&p!=="."&&p!=="..")}

function sha(v){return typeof v==="string"&&/^[0-9a-f]{40}$/.test(v)}

function docsOnly(p){return p.endsWith(".md")||p==="docs/agents/task-routing.json"}

export function validateRouting(config,exists){
  const errors=[];
  if(!object(config)||config.schemaVersion!==1)return ["Routing schemaVersion must be 1."];
  function docs(values,label,mandatory=false){
    if(!list(values)||(mandatory&&!values.length)){errors.push(label+" must be a unique string array"+(mandatory?" (nonempty).":"."));return;}
    for(const p of values)if(!repoPath(p)||!exists(p))errors.push(label+": invalid or missing repository file "+p);
  }
  docs(config.alwaysRequired,"alwaysRequired",true);
  for(const p of ["AGENTS.md","docs/agents/contributor-instructions.md","docs/README.md","docs/agents/task-routing.json","docs/agents/compliance-reports.md"])if(!Array.isArray(config.alwaysRequired)||!config.alwaysRequired.includes(p))errors.push("Missing bootstrap prerequisite: "+p);
  if(!Array.isArray(config.routes)||!config.routes.length)return [...errors,"routes must be a nonempty array."];
  const ids=new Set();
  for(const route of config.routes){
    if(!object(route)||typeof route.id!=="string"||!/^[a-z][a-z0-9-]*$/.test(route.id)||!textValue(route.when)){errors.push("Invalid route id or description.");continue;}
    const label="Route "+route.id;
    if(ids.has(route.id))errors.push("Duplicate route ID: "+route.id);ids.add(route.id);
    docs(route.required,label+" required",true);docs(route.recommended,label+" recommended");docs(route.reference,label+" reference");
    if([route.required,route.recommended,route.reference].every(Array.isArray)){const all=[...route.required,...route.recommended,...route.reference];if(new Set(all).size!==all.length)errors.push(label+" repeats a document across priorities.");}
    if(route.pathPrefixes!==undefined&&(!list(route.pathPrefixes)||route.pathPrefixes.some(p=>!p.endsWith("/")||!repoPath(p.slice(0,-1)))))errors.push(label+" has invalid pathPrefixes.");
    if(route.pathFiles!==undefined&&(!list(route.pathFiles)||route.pathFiles.some(p=>!repoPath(p))))errors.push(label+" has invalid pathFiles.");
  }
  for(const id of ["implementation","documentation","roadmap","novel-topic"])if(!ids.has(id))errors.push("Missing core route: "+id);
  return errors;
}

export function extractReport(body){
  const open="<!-- orbit2-agent-compliance:v1 -->",close="<!-- /orbit2-agent-compliance -->";
  if(typeof body!=="string"||body.split(open).length!==2||body.split(close).length!==2)throw Error("Expected exactly one agent compliance block.");
  const start=body.indexOf(open)+open.length,end=body.indexOf(close);
  const match=body.slice(start,end).trim().match(/^```json[ \t]*\r?\n([\s\S]*?)\r?\n```$/);
  if(end<start||!match)throw Error("Compliance block must contain exactly one JSON fence.");
  try{return JSON.parse(match[1]);}catch{throw Error("Compliance block contains invalid JSON.");}
}

export function validateReport(config,report,changedPaths){
  const errors=[];
  if(!object(report)||report.schemaVersion!==1)return ["Report schemaVersion must be 1."];
  const routes=new Map(config.routes.map(r=>[r.id,r]));
  if(!list(report.routes)||!report.routes.length)errors.push("routes must be a nonempty unique string array.");
  const chosen=new Set(Array.isArray(report.routes)?report.routes:[]);
  for(const id of chosen)if(!routes.has(id))errors.push("Unknown route: "+id);
  if(!list(report.consulted)||report.consulted.some(p=>!repoPath(p)))errors.push("consulted must contain unique repository-relative file paths.");
  const consulted=new Set(Array.isArray(report.consulted)?report.consulted:[]);
  const required=new Set(config.alwaysRequired);
  for(const id of chosen)for(const p of routes.get(id)?.required??[])required.add(p);
  for(const route of config.routes){
    if(changedPaths.some(p=>(route.pathPrefixes??[]).some(prefix=>p.startsWith(prefix))||(route.pathFiles??[]).includes(p))){
      if(!chosen.has(route.id))errors.push("Changed paths require route: "+route.id);
      for(const p of route.required)required.add(p);
    }
  }
  for(const p of required)if(!consulted.has(p))errors.push("Missing required consultation: "+p);
  if(!sha(report.revision))errors.push("revision must be a full Git SHA.");
  if(!list(report.constraints)||!report.constraints.length)errors.push("constraints must be a nonempty unique string array.");
  if(!list(report.limitations))errors.push("limitations must be a unique string array.");
  if(!textValue(report.discoverability))errors.push("discoverability must be nonempty.");
  if(!Array.isArray(report.exceptions)||report.exceptions.some(x=>!object(x)||!repoPath(x.path)||!textValue(x.reason)||!textValue(x.authorization)))errors.push("exceptions must contain path, reason and authorization; exceptions never waive required consultation.");
  const v=report.verification,states=["passed","failed","partial","not-run","documentation-only-waived"];
  if(!object(v)||!states.includes(v.status)||!list(v.checks)||(v.testedRevision!==null&&!sha(v.testedRevision)))errors.push("Invalid verification status, checks or testedRevision.");
  else{
    if(["passed","failed","partial"].includes(v.status)&&(!v.checks.length||!sha(v.testedRevision)))errors.push("Reported verification requires actual checks and a tested revision.");
    if(["not-run","documentation-only-waived"].includes(v.status)&&(v.checks.length||v.testedRevision!==null))errors.push("Unrun or waived verification cannot claim checks or a tested revision.");
    if(v.status==="documentation-only-waived"&&changedPaths.some(p=>!docsOnly(p)))errors.push("Documentation-only waiver cannot cover executable or non-documentation changes.");
    if(v.status!=="passed"&&(!Array.isArray(report.limitations)||!report.limitations.length))errors.push("Incomplete verification requires a limitation.");
  }
  return errors;
}

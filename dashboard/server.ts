import http from 'node:http';
import {createHash} from 'node:crypto';
import {readFile,readdir,access,writeFile} from 'node:fs/promises';
import {resolve,sep} from 'node:path';
import {spawn} from 'node:child_process';
import {handleVoice} from './voice.ts';
import {extractIssues} from '../scorer/issues.ts';
import {root} from '../runner/unity.ts';
import {fingerprint,currentRevision} from '../runner/revision.ts';
let job:any=null;const replayOnly=process.argv.includes('--replay');
const origins=['http://127.0.0.1:3210','http://localhost:3210'];
const load=async(path:string)=>JSON.parse(await readFile(resolve(root,path),'utf8'));
const catalog=async(includeDisabled=false)=>{const tasks=Object.entries(await load('tasks/tasks.json')).map(([id,task]:any)=>({id,...task}));return includeDisabled?tasks:tasks.filter(t=>t.available!==false)};
async function busy(){if(job?.running)return true;for(const f of ['.active','.cycle'])try{await access(resolve(root,'runs',f));return true}catch{}return false}
function launch(task:string,args:string[]){
 job={running:true,task,events:[]};const proc=spawn(process.execPath,['--env-file=.env.local',...args],{cwd:root,stdio:['ignore','pipe','pipe']});
 proc.stdout.on('data',b=>{job.events.push(b.toString());if(job.events.length>80)job.events.shift()});proc.stderr.on('data',b=>job.error=b.toString().slice(-1500));proc.on('close',code=>{job.running=false;job.exitCode=code});proc.on('error',()=>{job.running=false;job.error='Worker could not start'});
}
async function records(){
 const tasks=await catalog(true),map=new Map();
 for(const base of (replayOnly?['docs/build/evidence']:['docs/build/evidence','runs'])){let dirs=[];try{dirs=await readdir(resolve(root,base))}catch{}for(const id of dirs)try{
  const result=await load(base+'/'+id+'/result.json'),trace=await load(base+'/'+id+'/'+(base==='runs'?'trace.json':'trace.public.json'));let analysis={};try{analysis=await load(base+'/'+id+'/analysis.json')}catch{}
  const task=tasks.find(t=>t.id===result.taskId);const run={...result,...analysis,taskFamily:result.taskFamily||result.task?.category||task?.category||result.taskId,trace};run.taskAvailability=task?.available!==false;run.availabilityNote=task?.availabilityNote;run.hmiRevision=run.hmiRevision||fingerprint(run.uiHash,run.componentHashes);run.issues=extractIssues(run,trace);map.set(id,run);
 }catch{}}
 return map;
}
async function cycles(){const map=new Map();for(const base of ['docs/build/cycles',...(replayOnly?[]:['runs'])]){let dirs=[];try{dirs=await readdir(resolve(root,base))}catch{}for(const id of dirs.filter(id=>/^cycle-[0-9TZ-]+$/.test(id)))try{const c=await load(base+'/'+id+'/cycle.json');c.diffUrl='/api/cycle-diff?id='+id;map.set(id,c)}catch{}}return [...map.values()].sort((a,b)=>b.id.localeCompare(a.id))}
const server=http.createServer(async(req,res)=>{
 const url=new URL(req.url||'/','http://127.0.0.1');const json=(v:any,status=200)=>{res.writeHead(status,{'Content-Type':'application/json','Cache-Control':'no-store'});res.end(JSON.stringify(v))};
 const body=async(limit=8192)=>{let text='';for await(const chunk of req){text+=chunk;if(text.length>limit)throw Error('Request too large')}return JSON.parse(text||'{}')};
 try{
  if(await handleVoice(req,res,url,{enabled:!replayOnly}))return;
  if(['/voice-panel.js','/recorder.js','/reports.js','/instructions.js','/premium.css'].includes(url.pathname)){res.setHeader('Content-Type',url.pathname.endsWith('.css')?'text/css':'text/javascript');res.end(await readFile(resolve(root,'dashboard',url.pathname.slice(1))));return}
  if(url.pathname.startsWith('/assets/')){const base=resolve(root,'dashboard/assets'),file=resolve(base,decodeURIComponent(url.pathname.slice(8)));if(!file.startsWith(base+sep))return json({error:'Denied'},403);res.setHeader('Content-Type',file.endsWith('.ttf')?'font/ttf':'image/png');res.end(await readFile(file));return}
  if(url.pathname==='/api/catalog'){const tasks=await catalog();const unsupported=(await catalog(true)).filter(t=>t.available===false);return json({tasks,unsupported,categories:[...new Set(tasks.map(t=>t.category))]})}
  if(url.pathname==='/api/runs'){
   const map=await records(),changes=await cycles();
   for(const c of changes){if(c.status!=='succeeded'||!c.uiHash||c.uiHash===c.parentHash)continue;for(const before of c.runs||[]){if(!before.label?.startsWith('baseline-'))continue;const old=map.get(before.id);if(!old||old.uiHash!==c.parentHash)continue;const after=(c.runs||[]).map(r=>map.get(r.id)).find(r=>r?.taskId===old.taskId&&r.id!==old.id&&r.success&&!r.failure&&r.uiHash===c.uiHash);if(!after)continue;const rules=new Set(after.issues.map(i=>i.rule));for(const issue of old.issues)if(!rules.has(issue.rule)&&issue.category!=='infrastructure')issue.resolution={cycleId:c.id,retestRunId:after.id,diffUrl:c.diffUrl,label:'Not observed in linked fresh retest'};}}
   const runs=[...map.values()].sort((a,b)=>b.id.localeCompare(a.id)),versions=await load('tasks/versions.json');
   const currentUiHash=createHash('sha256').update(await readFile(resolve(root,'unity-hmi/Assets/Glance/UI/GlanceRadioUI.cs'))).digest('hex'),config=await load('tasks/benchmark.json');
   const cohort=(variant,hash)=>{const a=runs.filter(r=>r.taskId==='radio'&&r.variant===variant&&r.uiHash===hash&&r.policyVersion===config.policyVersion),n=a.map(r=>r.actions).sort((a,b)=>a-b);return {trials:a.length,successes:a.filter(r=>r.success).length,medianInputs:n.length?n[Math.floor(n.length/2)]:null,uiHash:hash}};
   const {hmiRevision:currentHmiRevision}=await currentRevision(currentUiHash);
   let cycle=null,live=null;try{cycle=await load('runs/cycle-latest.json')}catch{}if(!replayOnly)try{live=await load('runs/live.json')}catch{}
   let implementationFixes=[];try{implementationFixes=await load('docs/build/implementation-fixes.json')}catch{}
   return json({job:job?.running?job:await busy()?{running:true,events:['A CLI evaluation owns Unity.']}:job,cycle,cycles:changes,live,implementationFixes,versions,currentUiHash,currentHmiRevision,benchmark:{baseline:cohort('baseline',config.originalRadioUiHash),revised:cohort('revised',currentUiHash),originalUiHash:config.originalRadioUiHash,originalHmiRevision:config.originalRadioUiHash},liveAvailable:!replayOnly&&!!process.env.OPENAI_API_KEY,runs});
  }
  if(url.pathname==='/api/cycle-diff'){const id=url.searchParams.get('id')||'';if(!/^cycle-[0-9TZ-]+$/.test(id))return json({error:'Invalid cycle'},400);let text;for(const base of ['docs/build/cycles',...(replayOnly?[]:['runs'])])try{text=await readFile(resolve(root,base,id,'proposal-diff.stdout'),'utf8')}catch{}if(text===undefined)return json({error:'Diff unavailable'},404);res.setHeader('Content-Type','text/plain');res.end(text.replaceAll(root,'<GLANCE_ROOT>'));return}
  if(url.pathname==='/api/redesign')return json({summary:await load('docs/build/redesign/summary.json'),patch:await readFile(resolve(root,'docs/build/redesign/ui.patch'),'utf8')});
  if(req.method==='POST'&&url.pathname.startsWith('/api/')){
   if(!origins.includes(req.headers.origin||''))return json({error:'Origin denied'},403);
   if(url.pathname==='/api/recording'){const chunks=[];let size=0;for await(const chunk of req){size+=chunk.length;if(size>150000000)return json({error:'Recording too large'},413);chunks.push(chunk)}await writeFile(resolve(root,'docs/build/glance-live-demo.webm'),Buffer.concat(chunks));return json({saved:true})}
   if(replayOnly||!process.env.OPENAI_API_KEY)return json({error:'Live mode and a server-side API key are required'},400);
   if(await busy())return json({error:'Unity is busy with another evaluation or cycle'},409);
   if(url.pathname==='/api/cycle'){launch('Codex cycle',['runner/cycle.ts']);return json({started:true})}
   if(url.pathname==='/api/fresh'){launch('Fresh baseline HMI',['runner/fresh.ts']);return json({started:true})}
   if(url.pathname==='/api/suite'){launch('Full HMI suite',['runner/suite.ts']);return json({started:true})}
   const input=await body(),tasks=await catalog();let taskId=input.task;
   if(url.pathname==='/api/instruction'&&!taskId){
    if(typeof input.instruction!=='string'||!input.instruction.trim()||input.instruction.length>2000)return json({error:'Enter one HMI instruction, up to 2000 characters'},400);
    const response=await fetch('https://api.openai.com/v1/decisions',{method:'POST',headers:{Authorization:'Bearer '+process.env.OPENAI_API_KEY,'Content-Type':'application/json'},body:JSON.stringify({model:process.env.OPENAI_DECISIONS_MODEL||'gpt-6-luna',input:input.instruction,questions:[{type:'choice',name:'task',instructions:'Map this instruction to exactly one supported HMI evaluation with the exact requested target. Different numbers, temperatures, stations or levels than the listed contract are unsupported; never substitute a nearby target. Choose unsupported for multiple tasks, physical driving/vehicle physics, or requests outside the listed verifier contracts. Do not invent a task or alter a goal.',choices:[...tasks.map(t=>({value:t.id,description:t.instruction+(t.verificationNote?' '+t.verificationNote:'')})),{value:'unsupported',description:'Not exactly one supported verifiable HMI task'}]}]}),signal:AbortSignal.timeout(20000)});
    if(!response.ok)return json({error:'Instruction mapping API failed; choose a capability directly'},502);const d=await response.json();taskId=d.answers?.find(a=>a.name==='task')?.choice;if(taskId==='unsupported')return json({error:'Choose one supported HMI task, or use the full-suite button for all capabilities'},400);
   }
   if(!['/api/start','/api/instruction'].includes(url.pathname))return json({error:'Not found'},404);
   const task=tasks.find(t=>t.id===taskId);if(!task)return json({error:'Unknown registered task'},400);
   const versions=await load('tasks/versions.json');const uiHash=createHash('sha256').update(await readFile(resolve(root,'unity-hmi/Assets/Glance/UI/GlanceRadioUI.cs'))).digest('hex');const variant=url.pathname==='/api/instruction'?(taskId==='radio'?(uiHash===versions.baseline?'baseline':'revised'):'regression'):input.variant;
   if(!['baseline','revised','regression'].includes(variant))return json({error:'Invalid variant'},400);if(await busy())return json({error:'Unity became busy; retry after the current job'},409);
   launch(task.title,['runner/run.ts',taskId,variant]);return json({started:true,task:{id:taskId,title:task.title,category:task.category,instruction:task.instruction}});
  }
  if(url.pathname.startsWith('/runs/')){const base=resolve(root,'runs'),file=resolve(root,'.'+decodeURIComponent(url.pathname));if(!file.startsWith(base+sep)||!file.endsWith('.png'))return json({error:'Denied'},403);let bytes;try{if(replayOnly)throw Error();bytes=await readFile(file)}catch{bytes=await readFile(resolve(root,'docs/build/evidence',file.slice(base.length+1)))}res.setHeader('Content-Type','image/png');res.end(bytes);return}
  if(url.pathname!=='/')return json({error:'Not found'},404);res.setHeader('Content-Type','text/html; charset=utf-8');res.end(await readFile(resolve(root,'dashboard/index.html')));
 }catch(e:any){json({error:e.code==='ENOENT'?'Not found':'Request failed: '+String(e.message).slice(0,160)},400)}
});server.listen(3210,'127.0.0.1',()=>console.log('Glance: http://127.0.0.1:3210'));

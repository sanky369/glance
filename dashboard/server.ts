import http from 'node:http';
import {createHash} from 'node:crypto';
import {readFile,readdir} from 'node:fs/promises';
import {resolve,sep} from 'node:path';
import {spawn} from 'node:child_process';
import {root} from '../runner/unity.ts';
let job:any=null;const replayOnly=process.argv.includes('--replay');
const server=http.createServer(async(req,res)=>{
 const url=new URL(req.url||'/','http://127.0.0.1');
 const json=(v:any,status=200)=>{res.writeHead(status,{'Content-Type':'application/json','Cache-Control':'no-store'});res.end(JSON.stringify(v));};
 try{
  if(url.pathname==='/api/runs'){
   const records=new Map();for(const base of (replayOnly?['docs/build/evidence']:['docs/build/evidence','runs'])){let dirs:string[]=[];try{dirs=await readdir(resolve(root,base));}catch{}for(const d of dirs){try{const result=JSON.parse(await readFile(resolve(root,base,d,'result.json'),'utf8'));const trace=JSON.parse(await readFile(resolve(root,base,d,base==='runs'?'trace.json':'trace.public.json'),'utf8'));let analysis={};try{analysis=JSON.parse(await readFile(resolve(root,base,d,'analysis.json'),'utf8'));}catch{}records.set(d,{...result,...analysis,trace});}catch{}}}const runs=[...records.values()];
   const versions=JSON.parse(await readFile(resolve(root,'tasks/versions.json'),'utf8'));const currentUiHash=createHash('sha256').update(await readFile(resolve(root,'unity-hmi/Assets/Glance/UI/GlanceRadioUI.cs'))).digest('hex');
   return json({job,versions,currentUiHash,liveAvailable:!replayOnly&&!!process.env.OPENAI_API_KEY,runs:runs.sort((a,b)=>b.id.localeCompare(a.id))});
  }
  if(url.pathname==='/api/redesign')return json({summary:JSON.parse(await readFile(resolve(root,'docs/build/redesign/summary.json'),'utf8')),patch:await readFile(resolve(root,'docs/build/redesign/ui.patch'),'utf8')});
  if(url.pathname==='/api/start'&&req.method==='POST'){
   const origin=req.headers.origin;if(origin&&!['http://127.0.0.1:3210','http://localhost:3210'].includes(origin))return json({error:'Origin denied'},403);
   if(replayOnly||!process.env.OPENAI_API_KEY)return json({error:'Live runs need a server-side OPENAI_API_KEY; recorded evidence remains available.'},400);
   if(job?.running)return json({error:'A run is already active'},409);
   let data='';for await(const chunk of req){data+=chunk;if(data.length>4096)return json({error:'Request too large'},413);}
   const input=JSON.parse(data);if(!['radio','climate'].includes(input.task)||!['baseline','revised','regression'].includes(input.variant))return json({error:'Invalid task/variant'},400);
   job={running:true,task:input.task,variant:input.variant,events:[]};
   const proc=spawn(process.execPath,['--env-file=.env.local','runner/run.ts',input.task,input.variant],{cwd:root,stdio:['ignore','pipe','pipe']});
   proc.stdout.on('data',b=>{job.events.push(b.toString());if(job.events.length>60)job.events.shift();});
   proc.stderr.on('data',b=>{job.error=b.toString().slice(-2000);});proc.on('error',()=>{job.running=false;job.error='Could not launch runner';});proc.on('close',code=>{job.running=false;job.exitCode=code;});return json({started:true});
  }
  if(url.pathname.startsWith('/runs/')){
   const base=resolve(root,'runs'),path=resolve(root,'.'+decodeURIComponent(url.pathname));
   if(!path.startsWith(base+sep)||!path.endsWith('.png'))return json({error:'Denied'},403);
   let bytes;try{if(replayOnly)throw new Error('Archive only');bytes=await readFile(path);}catch{bytes=await readFile(resolve(root,'docs/build/evidence',path.slice(base.length+1)));}res.setHeader('Content-Type','image/png');res.end(bytes);return;
  }
  if(url.pathname!=='/')return json({error:'Not found'},404);
  res.setHeader('Content-Type','text/html; charset=utf-8');res.end(await readFile(resolve(root,'dashboard/index.html')));
 }catch(e:any){json({error:e.code==='ENOENT'?'Not found':'Request failed'},400);}
});server.listen(3210,'127.0.0.1',()=>console.log('Glance: http://127.0.0.1:3210'));

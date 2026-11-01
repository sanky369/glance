import {readFile,writeFile,mkdir,open,unlink,access} from 'node:fs/promises';
import {resolve} from 'node:path';
import {spawn} from 'node:child_process';
import {root,project} from './unity.ts';
import {hash} from './apply-revision.ts';
const tasks=JSON.parse(await readFile(resolve(root,'tasks/tasks.json'),'utf8')),versions=JSON.parse(await readFile(resolve(root,'tasks/versions.json'),'utf8'));
const id='suite-'+new Date().toISOString().replace(/[:.]/g,'-'),out=resolve(root,'runs',id),lock=resolve(root,'runs/.cycle');await mkdir(out,{recursive:true});let owns=false,activeChild:any=null,cancelled=false;for(const signal of ['SIGINT','SIGTERM'])process.on(signal,()=>{cancelled=true;activeChild?.kill('SIGTERM')});
const result:any={id,startedAt:new Date().toISOString(),status:'running',tasks:[],scope:'All registered task contracts; each task starts from its deterministic fixture. Real agent inputs, not scripted completion.'};
try{const h=await open(lock,'wx');owns=true;await h.writeFile(JSON.stringify({id,pid:process.pid}));await h.close();try{await access(resolve(root,'runs/.active'));throw Error('Another run owns Unity')}catch(e:any){if(e.code!=='ENOENT')throw e}
 const ui=hash(await readFile(resolve(project,'Assets/Glance/UI/GlanceRadioUI.cs'),'utf8'));
 for(const [task,definition]of Object.entries(tasks)){
  if((definition as any).available===false){result.tasks.push({task,status:'unavailable',success:false,reason:(definition as any).availabilityNote});continue;}
  if(cancelled)throw Error('Suite cancelled');console.log(JSON.stringify({stage:'Testing '+task}));const variant=task==='radio'?(ui===versions.baseline?'baseline':'revised'):'regression';
  const p=spawn(process.execPath,['runner/run.ts',task,variant],{cwd:root,env:{...process.env,GLANCE_CYCLE_ID:id,GLANCE_MAX_CALLS:'10',GLANCE_MAX_ACTIONS:'10',GLANCE_MAX_MS:'100000'},stdio:['ignore','pipe','pipe']});activeChild=p;let output='',error='';p.stdout.on('data',b=>{output+=b;process.stdout.write(b)});p.stderr.on('data',b=>error+=b);const code=await new Promise(r=>p.on('close',r));activeChild=null;let run;for(const line of output.split('\n'))try{const d=JSON.parse(line);if(d.taskId===task&&d.uiHash)run=d}catch{}
  result.tasks.push({task,code,runId:run?.id,success:!!run?.success&&!run?.failure,actions:run?.actions,error:run?.failure||error.slice(-500)||null});await writeFile(resolve(out,'suite.json'),JSON.stringify(result,null,2));await writeFile(resolve(root,'runs/suite-latest.json'),JSON.stringify(result,null,2));
 }
 result.status='complete';result.passed=result.tasks.filter(t=>t.success).length;result.total=result.tasks.length;result.finishedAt=new Date().toISOString();
}catch(e:any){result.status='failed';result.error=e.message;process.exitCode=1}finally{await writeFile(resolve(out,'suite.json'),JSON.stringify(result,null,2)+'\n');await writeFile(resolve(root,'runs/suite-latest.json'),JSON.stringify(result,null,2)+'\n');if(owns)await unlink(lock).catch(()=>{});console.log(JSON.stringify({stage:'Full HMI suite '+result.status,passed:result.passed,total:result.total,out}));}

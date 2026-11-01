import {readFile,writeFile,open,unlink,access} from 'node:fs/promises';
import {resolve} from 'node:path';
import {spawn} from 'node:child_process';
import {root,project,unity,settled} from './unity.ts';
import {hash,atomicWrite} from './apply-revision.ts';
const id='fresh-'+Date.now(),lock=resolve(root,'runs/.cycle'),target=resolve(project,'Assets/Glance/UI/GlanceRadioUI.cs');
let owns=false,before='',oldVersions='';
const event=(stage:string)=>console.log(JSON.stringify({stage}));
async function compile(){const p=spawn('unity',['recompile','--project-path',project,'--timeout','120','--format','json'],{stdio:['ignore','pipe','pipe'],env:{...process.env,OPENAI_API_KEY:''}});let text='';p.stdout.on('data',b=>text+=b);const code=await new Promise(r=>p.on('close',r));if(code!==0)throw Error('UI compile failed');await unity('editor.playmode start');await settled()}
try{
 const h=await open(lock,'wx');owns=true;await h.writeFile(JSON.stringify({id,pid:process.pid}));await h.close();try{await access(resolve(root,'runs/.active'));throw Error('An evaluation owns Unity')}catch(e:any){if(e.code!=='ENOENT')throw e}
 before=await readFile(target,'utf8');oldVersions=await readFile(resolve(root,'tasks/versions.json'),'utf8');const fixture=await readFile(resolve(root,'docs/build/demo-baseline.cs'),'utf8');const config=JSON.parse(await readFile(resolve(root,'tasks/demo.json'),'utf8'));if(hash(fixture)!==config.baselineHash)throw Error('Demo fixture digest changed');
 event('Loading the documented nested-radio baseline with full native HMI');await unity('editor.playmode stop');await atomicWrite(target,fixture);const versions=JSON.parse(oldVersions);versions.baseline=hash(fixture);await atomicWrite(resolve(root,'tasks/versions.json'),JSON.stringify(versions,null,2)+'\n');await compile();await unity('glance.test reset','--task','driver-door');await settled();event('Fresh HMI ready · all native controls available; radio uses the baseline menu');
}catch(e:any){if(before){try{await unity('editor.playmode stop');await atomicWrite(target,before);await atomicWrite(resolve(root,'tasks/versions.json'),oldVersions);await compile()}catch{}}console.error(e.message);process.exitCode=1}finally{if(owns)await unlink(lock).catch(()=>{})}

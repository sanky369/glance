import {mkdir,readFile,writeFile,open,unlink,rename,access,chmod,copyFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import {spawn} from 'node:child_process';
import {root,project,unity,settled} from './unity.ts';
import {proposeRedesign} from './redesign.ts';
import {applyRevision,hash,atomicWrite} from './apply-revision.ts';

const id='cycle-'+new Date().toISOString().replace(/[:.]/g,'-');
const out=resolve(root,'runs',id),target=resolve(project,'Assets/Glance/UI/GlanceRadioUI.cs');
const lock=resolve(root,'runs/.cycle'),versionsPath=resolve(root,'tasks/versions.json');
await mkdir(resolve(root,'runs'),{recursive:true});
let acquired=false,applied=false,original='',versionsBefore='',replacement='',activeChild:ReturnType<typeof spawn>|null=null;
let cancelled=false;const cancellation=new AbortController();
for(const signal of ['SIGINT','SIGTERM'] as const)process.on(signal,()=>{cancelled=true;cancellation.abort();activeChild?.kill('SIGTERM');});
const state:any={id,out,validationPolicy:'radio-path-climate-function-v1',status:'running',stage:'starting',startedAt:new Date().toISOString(),runs:[]};
async function atomic(path:string,value:any){const temp=path+'.tmp';await writeFile(temp,JSON.stringify(value,null,2)+'\n');await rename(temp,path);}
async function stage(name:string,extra:any={}){
 if(cancelled&&state.stage!=='rolling-back'&&name!=='rolling-back'&&name!=='failed')throw new Error('Cycle cancelled');
 Object.assign(state,extra,{stage:name,updatedAt:new Date().toISOString()});
 await atomic(resolve(out,'cycle.json'),state);await atomic(resolve(root,'runs/cycle-latest.json'),state);
 console.log(JSON.stringify({type:'cycle',...state}));
}
async function command(label:string,exe:string,args:string[],env:NodeJS.ProcessEnv=process.env){
 const child=spawn(exe,args,{cwd:root,env,stdio:['ignore','pipe','pipe']});activeChild=child;
 let stdout='',stderr='',pending='';
 child.stdout.on('data',b=>{const chunk=b.toString();stdout+=chunk;pending+=chunk;const lines=pending.split('\n');pending=lines.pop()||'';for(const line of lines){try{console.log(JSON.stringify({type:'cycle-child',cycleId:id,label,event:JSON.parse(line)}));}catch{}}});
 child.stderr.on('data',b=>stderr+=b.toString());
 let code;try{code=await new Promise((r,j)=>{child.on('error',j);child.on('close',r)});}finally{activeChild=null;await writeFile(resolve(out,label+'.stdout'),stdout);await writeFile(resolve(out,label+'.stderr'),stderr);}
 if(code!==0)throw new Error(label+' failed; see retained command output');return stdout;
}
const node=(label:string,args:string[])=>command(label,process.execPath,args,{...process.env,GLANCE_CYCLE_ID:id});
async function test(task:string,variant:string,label:string){
 let stdout='',commandFailure:Error|undefined;
 try{stdout=await node(label,['runner/run.ts',task,variant]);}catch(e:any){commandFailure=e;stdout=await readFile(resolve(out,label+'.stdout'),'utf8').catch(()=>'');}
 const lines=stdout.trim().split('\n');let result:any;
 for(const line of lines){try{const value=JSON.parse(line);if(value.taskId===task&&value.uiHash)result=value;}catch{}}
 if(!result)throw commandFailure??new Error('Test did not return a source-tagged result');
 state.runs.push({label,id:result.id,uiHash:result.uiHash,success:result.success,actions:result.actions,failure:result.failure});
 if(commandFailure)throw commandFailure;
 if(!result.success||result.failure)throw new Error(label+' did not pass');
 const expected=hash(await readFile(target,'utf8'));if(result.uiHash!==expected)throw new Error('Test source hash differs from current UI');
 return result;
}
async function restart(label:string){
 const env={...process.env};delete env.OPENAI_API_KEY;
 await command(label+'-compile','unity',['recompile','--project-path',project,'--timeout','120','--format','json'],env);
 await command(label+'-ready','unity',['status','--until-ready','--timeout','120','--project-path',project,'--format','json'],env);
 await unity('editor.playmode start');await settled();
}
try{
 const handle=await open(lock,'wx');acquired=true;await handle.writeFile(JSON.stringify({id,pid:process.pid,startedAt:state.startedAt}));await handle.close();
 try{await access(resolve(root,'runs/.active'));throw new Error('Another Glance run owns Unity');}catch(e:any){if(e.code!=='ENOENT')throw e;}
 await mkdir(out);await stage('preflight');
 if(!process.env.OPENAI_API_KEY)throw new Error('OPENAI_API_KEY is missing');
 original=await readFile(target,'utf8');versionsBefore=await readFile(versionsPath,'utf8');
 await writeFile(resolve(out,'parent.cs'),original,{flag:'wx'});await chmod(resolve(out,'parent.cs'),0o444);
 await writeFile(resolve(out,'versions-before.json'),versionsBefore,{flag:'wx'});await chmod(resolve(out,'versions-before.json'),0o444);
 state.parentHash=hash(original);
 try{
  const editor=await unity('editor status');
  if(editor.playMode!=='playing')throw new Error('Editor playMode is '+editor.playMode);
  if(editor.playerLoopTicking===false)throw new Error('Editor player loop is not advancing');
  await settled();
 }catch(e:any){throw new Error('Unity preflight failed: open Main in the isolated unity-hmi Editor, start Play, unpause it, and retry. Details: '+e.message);}
 const versions=JSON.parse(versionsBefore);versions.baseline=state.parentHash;await atomicWrite(versionsPath,JSON.stringify(versions,null,2)+'\n');
 await stage('baseline-radio');const radio=await test('radio','baseline','baseline-radio');
 await stage('baseline-climate');const climate=await test('climate','regression','baseline-climate');
 const trace=JSON.parse(await readFile(resolve(root,'runs',radio.id,'trace.json'),'utf8'));
 const findings={baselineRun:radio.id,climateBaselineRun:climate.id,sourceHash:state.parentHash,verifiedSuccess:radio.success,metrics:{actions:radio.actions,screensVisited:radio.screensVisited,backtracks:radio.backtracks,smallTargets:radio.smallTargets},observedPath:trace.filter((s:any)=>s.executed).map((s:any)=>s.action.label),observedTargets:trace.filter((s:any)=>s.executed).map((s:any)=>({label:s.action.label,width:s.action.width,height:s.action.height})),request:'Improve only evidenced navigation or target-size problems. Preserve existing styling, all stations, Media, Home and Climate and all functionality. An already optimal one-input path may require no change.',limits:'One genuine trial per task per phase; visible-control inventory assists the agent. No inference about human distraction or safety.'};
 const findingsPath=resolve(out,'findings.json');await writeFile(findingsPath,JSON.stringify(findings,null,2)+'\n',{flag:'wx'});await chmod(findingsPath,0o444);
 if(await readFile(target,'utf8')!==original)throw new Error('UI changed during baseline; refusing proposal');
 await stage('codex-proposal');const proposal=await proposeRedesign(findingsPath,original,{signal:cancellation.signal});state.proposal=proposal;
 replacement=await readFile(resolve(proposal.out,'after.cs'),'utf8');
 await command('proposal-diff','git',['diff','--no-index','--',resolve(proposal.out,'before.cs'),resolve(proposal.out,'after.cs')]).catch(async e=>{const diff=await readFile(resolve(out,'proposal-diff.stdout'),'utf8');if(!diff.trim()&&original!==replacement)throw e;});
 if(await readFile(target,'utf8')!==original)throw new Error('UI changed during proposal; refusing stale application');
 if(proposal.outcome==='changed'){
  await stage('applying');await unity('editor.playmode stop');
  applied=true;await applyRevision(proposal.id);
  await stage('compiling');await restart('revised');
 }else{
  const currentVersions=JSON.parse(await readFile(versionsPath,'utf8'));currentVersions.revised=hash(original);await atomicWrite(versionsPath,JSON.stringify(currentVersions,null,2)+'\n');
 }
 await stage('retest-radio');const afterRadio=await test('radio','revised','retest-radio');
 await stage('retest-climate');const afterClimate=await test('climate','regression','retest-climate');
 state.comparison={before:{radioActions:radio.actions,radioSmallTargets:radio.smallTargets,climateActions:climate.actions},after:{radioActions:afterRadio.actions,radioSmallTargets:afterRadio.smallTargets,climateActions:afterClimate.actions},climateInputDelta:afterClimate.actions-climate.actions,improved:afterRadio.actions<radio.actions||afterRadio.smallTargets<radio.smallTargets};
 state.warnings=afterClimate.actions>climate.actions?[{code:'climate-input-increase',before:climate.actions,after:afterClimate.actions,delta:afterClimate.actions-climate.actions,message:'Climate succeeded with its passenger invariant, but this fresh trial used more inputs. This does not fail the functional acceptance criterion; retain and inspect the trace.'}]:[];
 if(afterRadio.actions>radio.actions||afterRadio.smallTargets>radio.smallTargets)throw new Error('Fresh radio retest regressed measured task inputs or radio target size');
 await stage('exporting');await node('export',['scripts/export_runs.ts']);
 const evidence=resolve(root,'docs/build/cycles',id);await mkdir(evidence,{recursive:true});
 for(const file of ['parent.cs','findings.json'])await copyFile(resolve(out,file),resolve(evidence,file));
 await writeFile(resolve(evidence,'proposal-diff.stdout'),(await readFile(resolve(out,'proposal-diff.stdout'),'utf8')).replaceAll(root,'<GLANCE_ROOT>'));
 await copyFile(resolve(proposal.out,'after.cs'),resolve(evidence,'after.cs'));
 state.evidence='docs/build/cycles/'+id;
 await stage(proposal.outcome==='already_optimized'?'already-optimized':'complete',{status:'succeeded',finishedAt:new Date().toISOString(),uiHash:hash(replacement)});
 await writeFile(resolve(evidence,'cycle.json'),JSON.stringify(state,null,2).replaceAll(root,'<GLANCE_ROOT>')+'\n');
}catch(e:any){
 state.error=e.message;
 if(acquired){
  await mkdir(out,{recursive:true});
  if(applied){
   try{
    await stage('rolling-back');
    const current=await readFile(target,'utf8');if(current!==replacement&&current!==original)throw new Error('UI changed outside cycle; refusing to overwrite concurrent edits');
    await unity('editor.playmode stop');await atomicWrite(target,original);await atomicWrite(versionsPath,versionsBefore);await restart('rollback');state.rollback='restored-and-restarted';
   }catch(rollback:any){state.rollback='failed';state.rollbackError=rollback.message;}
  }else if(versionsBefore){await atomicWrite(versionsPath,versionsBefore);}
  await stage('failed',{status:'failed',finishedAt:new Date().toISOString()});
 }else console.log(JSON.stringify({type:'cycle',id,status:'failed',stage:'locked',error:'Another Glance cycle owns Unity'}));
 process.exitCode=1;
}finally{
 if(activeChild)activeChild.kill('SIGTERM');
 if(acquired)await unlink(lock).catch(()=>{});
}

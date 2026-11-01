import {readdir,readFile,mkdir,writeFile,copyFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import {createHash} from 'node:crypto';
import {root} from '../runner/unity.ts';
const dest=resolve(root,'docs/build/evidence');await mkdir(dest,{recursive:true});const results=[];
for(const id of await readdir(resolve(root,'runs'))){
 const dir=resolve(root,'runs',id);let result:any;try{result=JSON.parse(await readFile(resolve(dir,'result.json'),'utf8'));}catch{continue;}
 const recorded=result;try{result={...result,...JSON.parse(await readFile(resolve(dir,'analysis.json'),'utf8'))};}catch{}
 const trace=JSON.parse(await readFile(resolve(dir,'trace.json'),'utf8'));const out=resolve(dest,id);await mkdir(out,{recursive:true});
 const safe=(v:any)=>JSON.stringify(v,null,2).replaceAll(root,'<GLANCE_ROOT>')+'\n';
 await writeFile(resolve(out,'recorded-result.json'),safe(recorded));await writeFile(resolve(out,'result.json'),safe(result));await writeFile(resolve(out,'trace.public.json'),safe(trace));
 if(result.finalScreenshot)await copyFile(resolve(dir,result.finalScreenshot),resolve(out,result.finalScreenshot));
 for(const s of trace){await copyFile(resolve(dir,s.screenshot),resolve(out,s.screenshot));if(s.afterScreenshot)await copyFile(resolve(dir,s.afterScreenshot),resolve(out,s.afterScreenshot));}
 try{await copyFile(resolve(dir,'ui-source.cs'),resolve(out,'ui-source.cs'));}catch{}
 const hash=createHash('sha256').update(await readFile(resolve(out,'trace.public.json'))).digest('hex');results.push({...result,traceSha256:hash});
}
await writeFile(resolve(dest,'index.json'),JSON.stringify(results,null,2)+'\n');console.log('Exported',results.length,'genuine runs to docs/build/evidence');

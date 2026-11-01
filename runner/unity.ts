import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {resolve} from 'node:path';
const exec=promisify(execFile);
export const root=resolve(import.meta.dirname,'..');
export const project=resolve(root,'unity-hmi');
const env={...process.env};delete env.OPENAI_API_KEY;
export async function unity(command:string,...args:string[]) {
 const {stdout}=await exec('unity',['command','--caller','plugin','--skill','unity-cli','--project-path',project,'--format','json',...command.split(' '),...(args.length?['--',...args]:[])],{cwd:root,env,timeout:60000,maxBuffer:4*1024*1024});
 let r;try {r=JSON.parse(stdout);}catch {throw new Error('Unity returned invalid JSON');}
 if(!r.success)throw new Error(JSON.stringify(r.errors));
 const result=r.data.result;if(result?.success===false)throw new Error(JSON.stringify(result));return result;
}
export const sleep=(ms:number)=>new Promise(r=>setTimeout(r,ms));
export async function settled() {const until=Date.now()+10000;while(Date.now()<until){const s=await unity('glance.test state');if(s.ready)return s;await sleep(100);}throw new Error('HMI did not settle');}
export async function gesture(c:any) {
 await unity('glance.pointer gesture','--x',String(c.x),'--y',String(c.y),'--end_x',String(c.endX),'--end_y',String(c.endY),'--width','1280','--height','720','--duration',String(c.duration));
 const until=Date.now()+6000;while(Date.now()<until){if(!(await unity('glance.pointer status')).busy)return;await sleep(50);}throw new Error('Pointer delivery timed out');
}

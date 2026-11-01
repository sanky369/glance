import {readFile,writeFile,access,rename,unlink,open} from 'node:fs/promises';
import {resolve} from 'node:path';
import {createHash} from 'node:crypto';
import {pathToFileURL} from 'node:url';
import {root,project} from './unity.ts';
export const hash=(source:string)=>createHash('sha256').update(source).digest('hex');
export function protectedSource(source:string){
 let text=source;
 for(const signature of ['public void Show(string page)','private void Menu(string label,float x,UnityAction action)']){
  const index=text.indexOf(signature);if(index<0||text.indexOf(signature,index+1)>=0)throw new Error('Required UI signature missing or duplicated');
  const start=index+signature.length;
  const mask=text.replace(/@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|\/\/[^\n]*|\/\*[\s\S]*?\*\//g,s=>' '.repeat(s.length));
  let at=start;while(at<mask.length&&/\s/.test(mask[at]))at++;
  let end=at;
  if(mask.slice(at,at+2)==='=>'){end=mask.indexOf(';',at)+1;if(end===0)throw new Error('Unclosed expression');}
  else{if(mask[at]!=='{')throw new Error('Invalid method body');let depth=0;for(;end<mask.length;end++){if(mask[end]==='{')depth++;if(mask[end]==='}'&&--depth===0){end++;break;}}if(depth!==0)throw new Error('Unclosed method');}
  text=text.slice(0,start)+' { ALLOWED_UI_BODY } '+text.slice(end);
 }
 return text;
}
export function reconstructPresentation(parent:string,proposal:string){
 const signatures=['public void Show(string page)','private void Menu(string label,float x,UnityAction action)'];
 function body(source:string,signature:string){
  const index=source.indexOf(signature);
  if(index<0||source.indexOf(signature,index+1)>=0)throw new Error('Required UI signature missing or duplicated');
  const mask=source.replace(/@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|\/\/[^\n]*|\/\*[\s\S]*?\*\//g,s=>' '.repeat(s.length));
  let start=index+signature.length;while(start<mask.length&&/\s/.test(mask[start]))start++;
  let end=start;
  if(mask.slice(start,start+2)==='=>'){end=mask.indexOf(';',start)+1;if(end===0)throw new Error('Unclosed UI expression');}
  else{
   if(mask[start]!=='{')throw new Error('Invalid UI method body');
   let depth=0;for(;end<mask.length;end++){if(mask[end]==='{')depth++;if(mask[end]==='}'&&--depth===0){end++;break;}}
   if(depth!==0)throw new Error('Unclosed UI method body');
  }
  return {start,end,text:source.slice(start,end)};
 }
 const changes=signatures.map(signature=>({...body(parent,signature),replacement:body(proposal,signature).text}));
 let candidate=parent;
 for(const change of changes.sort((a,b)=>b.start-a.start))candidate=candidate.slice(0,change.start)+change.replacement+candidate.slice(change.end);
 validateProposal(parent,candidate);return candidate;
}
// This parser accepts a deliberately small presentation language, never arbitrary C#.
const pages=new Set(['Home','Climate','Media','Sources','FM','Presets','Library','Audio','Bluetooth','USB','Scan','Vehicle','Settings','Playing']);
function presentation(source:string,strict:boolean){
 const showStart=source.indexOf('public void Show(string page)')+'public void Show(string page)'.length;
 const home=source.indexOf('if(page=="Home")',showStart);
 const tune=source.indexOf('private void Tune(string id)',home);
 const menuStart=source.indexOf('private void Menu(string label,float x,UnityAction action)')+'private void Menu(string label,float x,UnityAction action)'.length;
 const menuEnd=source.indexOf('private Transform Box(',menuStart);
 if(home<showStart||tune<home||menuEnd<menuStart)throw new Error('Unsupported presentation structure');
 const prefix=source.slice(showStart,home);
 const tokens:string[]=[];
 const input=source.slice(home,tune).trim();
 let at=0;
 const token=/\s+|"[^"\\\r\n]*"|-?(?:\d+(?:\.\d+)?|\.\d+)[fF]?|=>|==|!=|[A-Za-z_][A-Za-z_0-9]*|[{}(),;]/y;
 while(at<input.length){token.lastIndex=at;const match=token.exec(input);if(!match)throw new Error('Unsupported presentation token at '+at);at=token.lastIndex;if(!/^\s+$/.test(match[0]))tokens.push(match[0]);}
 let i=0,controls=0;const inventory:string[]=[],conditions:string[]=[];
 const peek=()=>tokens[i];
 function take(value?:string){const actual=tokens[i++];if(actual===undefined||(value!==undefined&&actual!==value))throw new Error('Unsupported presentation syntax: expected '+value+', got '+actual);return actual;}
 function literal(){const value=take();if(!/^"[^"\\\r\n]{1,100}"$/.test(value))throw new Error('Expected a plain label literal');return value.slice(1,-1);}
 function number(){const value=take();if(!/^-?(?:\d+(?:\.\d+)?|\.\d+)[fF]?$/.test(value))throw new Error('Expected a numeric literal');const n=Number(value.replace(/[fF]$/,''));if(!Number.isFinite(n)||Math.abs(n)>1000)throw new Error('Geometry outside allowed range');return n;}
 function vector(){take('new');take('Vector2');take('(');const x=number();take(',');const y=number();take(')');return [x,y];}
 function bounds(at:number[],size:number[],interactive:boolean){
  const minimum=interactive&&strict?64:1;
  if(size[0]<minimum||size[1]<minimum||size[0]>740||size[1]>200||Math.abs(at[0])+size[0]/2>370||Math.abs(at[1])+size[1]/2>100)throw new Error('Presentation geometry exceeds content bounds or minimum target size');
 }
 function callback(){take('(');take(')');take('=>');const call=take();if(call!=='Show'&&call!=='Tune')throw new Error('Only known UI navigation callbacks are allowed');take('(');let target:string;
  if(call==='Show'&&peek()==='Parent'){take('Parent');take('(');take('page');take(')');target='Parent(page)';}
  else{target=literal();if(call==='Tune'?!['radio_1','radio_2','radio_3'].includes(target):!pages.has(target))throw new Error('Unknown UI callback target');}
  take(')');return call+':'+target;
 }
 const menuCalls:{x:number}[]=[];
 function statement(context:string):void{
  if(++controls>200)throw new Error('Too many presentation statements');
  if(peek()==='{'){take('{');while(peek()!=='}')statement(context);take('}');return;}
  if(peek()==='if'){
   take('if');take('(');take('page');const op=take();if(op!=='=='&&op!=='!=')throw new Error('Unsupported UI condition');const page=literal();if(!pages.has(page))throw new Error('Unknown UI page');take(')');
   const condition=op+page;conditions.push(context+'|'+condition);statement(context+'|'+condition);
   if(peek()==='else'){take('else');statement(context+'|not('+condition+')');}return;
  }
  const call=take();if(!['Button','Menu','Label'].includes(call))throw new Error('Only Button, Menu and Label presentation calls are allowed');take('(');
  if(call==='Menu'){const label=literal();take(',');const x=number();take(',');const action=callback();take(')');take(';');menuCalls.push({x});inventory.push(context+'|'+label+'|'+action);return;}
  take('content');take(',');const label=peek()==='page'?take():literal();take(',');const at=vector();take(',');const size=vector();take(',');
  if(call==='Label'){const points=number();if(points<10||points>48)throw new Error('Label font size outside allowed range');}
  else inventory.push(context+'|'+label+'|'+callback());
  take(')');take(';');bounds(at,size,call==='Button');
 }
 while(i<tokens.length-1)statement('');take('}');if(i!==tokens.length)throw new Error('Unexpected trailing presentation code');
 const menu=source.slice(menuStart,menuEnd).trim();
 const match=/^=>\s*Button\(content,label,new Vector2\(x,(-?(?:\d+(?:\.\d+)?|\.\d+)[fF]?)\),new Vector2\((\d+(?:\.\d+)?[fF]?),(\d+(?:\.\d+)?[fF]?)\),action\);$/.exec(menu.replace(/\s+/g,' ').replace(/\s*([(),;])\s*/g,'$1'));
 if(!match)throw new Error('Menu must only forward its existing action to a bounded Button');
 const [y,w,h]=match.slice(1).map(n=>Number(n.replace(/[fF]$/,'')));bounds([0,y],[w,h],true);for(const call of menuCalls)bounds([call.x,y],[w,h],true);
 return {prefix,inventory,conditions};
}
export function validateProposal(before:string,after:string){
 if(protectedSource(before)!==protectedSource(after))throw new Error('Proposal changes protected code outside Show/Menu');
 if(/^[ \t]*#/m.test(after)&&before!==after)throw new Error('Preprocessor directives are not permitted in a UI proposal');
 const parent=presentation(before,false),proposal=presentation(after,before!==after);
 if(parent.prefix!==proposal.prefix)throw new Error('Show prologue and Climate branch must remain byte-identical');
 if(JSON.stringify(parent.conditions)!==JSON.stringify(proposal.conditions))throw new Error('Existing UI page control flow must be preserved');
 for(const control of parent.inventory)if(!proposal.inventory.includes(control))throw new Error('Existing UI control or callback was removed');
}
export async function atomicWrite(path:string,text:string){
 const temporary=path+'.glance-'+process.pid+'.tmp';
 try{await writeFile(temporary,text,{flag:'wx'});await rename(temporary,path);}finally{await unlink(temporary).catch(()=>{});}
}
export async function applyRevision(id:string){
 if(!/^redesign-[0-9TZ-]+$/.test(id))throw new Error('Supply a redesign run ID');
 const dir=resolve(root,'runs',id),target=resolve(project,'Assets/Glance/UI/GlanceRadioUI.cs');
 const before=await readFile(resolve(dir,'before.cs'),'utf8'),after=await readFile(resolve(dir,'after.cs'),'utf8');
 validateProposal(before,after);
 if(await readFile(target,'utf8')!==before)throw new Error('Current UI differs from proposal parent; refusing stale patch');
 const path=resolve(root,'tasks/versions.json'),versions=JSON.parse(await readFile(path,'utf8'));versions.revised=hash(after);
 await atomicWrite(target,after);await atomicWrite(path,JSON.stringify(versions,null,2)+'\n');
 return {id,uiHash:hash(after),changed:before!==after};
}
if(process.argv[1]&&import.meta.url===pathToFileURL(resolve(process.argv[1])).href){
 const lock=resolve(root,'runs/.cycle');let acquired=false;
 try{
  const handle=await open(lock,'wx');acquired=true;await handle.writeFile(JSON.stringify({id:'apply-'+process.pid,pid:process.pid,startedAt:new Date().toISOString()}));await handle.close();
  try{await access(resolve(root,'runs/.active'));throw new Error('A Glance run owns the UI; standalone application is locked');}catch(e:any){if(e.code!=='ENOENT')throw e;}
  console.log(JSON.stringify(await applyRevision(process.argv[2]||'')));
 }finally{if(acquired)await unlink(lock);}
}

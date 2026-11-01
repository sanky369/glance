import {readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import {createHash} from 'node:crypto';
import {root,project} from './unity.ts';
const id=process.argv[2];if(!id||!/^redesign-[0-9TZ-]+$/.test(id))throw new Error('Supply the reviewed redesign run ID');
const dir=resolve(root,'runs',id),target=resolve(project,'Assets/Glance/UI/GlanceRadioUI.cs');
const before=await readFile(resolve(dir,'before.cs'),'utf8'),after=await readFile(resolve(dir,'after.cs'),'utf8');
if(await readFile(target,'utf8')!==before)throw new Error('Current UI differs from proposal parent; refusing stale patch');
function protectedSource(source:string){
 let text=source;
 for(const signature of ['public void Show(string page)','private void Menu(string label,float x,UnityAction action)']){
  const index=text.indexOf(signature);if(index<0)throw new Error('Required UI signature changed');
  const start=index+signature.length;
  const mask=text.replace(/@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|\/\/[^\n]*|\/\*[\s\S]*?\*\//g,s=>' '.repeat(s.length));
  let at=start;while(/\s/.test(mask[at]||'')&&at<mask.length)at++;
  let end=at;
  if(mask.slice(at,at+2)==='=>'){end=mask.indexOf(';',at)+1;if(end===0)throw new Error('Unclosed expression');}
  else{if(mask[at]!=='{')throw new Error('Invalid method body');let depth=0;for(;end<mask.length;end++){if(mask[end]==='{')depth++;if(mask[end]==='}'&&--depth===0){end++;break;}}if(depth!==0)throw new Error('Unclosed method');}
  text=text.slice(0,start)+' { ALLOWED_UI_BODY } '+text.slice(end);
 }
 return text.replace(/\s+/g,'');
}
if(protectedSource(before)!==protectedSource(after))throw new Error('Proposal changes protected code outside Show/Menu');
await writeFile(target,after);
const path=resolve(root,'tasks/versions.json'),versions=JSON.parse(await readFile(path,'utf8'));versions.revised=createHash('sha256').update(after).digest('hex');await writeFile(path,JSON.stringify(versions,null,2)+'\n');
console.log('Applied reviewed UI-only proposal. Refresh/recompile Unity, then run fresh retests.');

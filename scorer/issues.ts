import {createHash} from 'node:crypto';

export type Issue={id:string,runId:string,sourceHash:string|null,category:'functionality'|'usability'|'infrastructure',severity:'high'|'medium'|'low',status:'observed',title:string,detail:string,rule:string,evidence:{steps:number[],screenshots:string[],observed:unknown}};
export function extractIssues(result:any,trace:any[]=result.trace||[]):Issue[]{
 const rows=Array.isArray(trace)?trace:[],issues:Issue[]=[];
 const runId=String(result.id||'unknown'),sourceHash=typeof result.uiHash==='string'?result.uiHash:null;
 const add=(rule:string,category:Issue['category'],severity:Issue['severity'],title:string,detail:string,steps:number[],observed:unknown)=>{
  const screenshots=[...new Set(steps.map(index=>rows[index]?.screenshot).filter((v:any)=>typeof v==='string'))] as string[];
  issues.push({id:'issue-'+createHash('sha256').update(JSON.stringify([runId,sourceHash,rule,steps])).digest('hex').slice(0,20),runId,sourceHash,category,severity,status:'observed',title,detail,rule,evidence:{steps,screenshots,observed}});
 };
 if(result.taskAvailability===false){add('capability-unavailable','infrastructure','medium','This control is not instantiated in the sample',result.availabilityNote||'The requested control is unavailable; this does not establish a defect in the available HMI.',[],{taskId:result.taskId});return issues;}
 const failure=typeof result.failure==='string'?result.failure:'';
 const infrastructure=!!failure;
 if(failure){
  const timed=/timeout|timed out|budget exceeded|did not settle/i.test(failure),api=/api|decisions|http|fetch|network|econn|enotfound|429|rate limit/i.test(failure);
  add(timed?'execution-timeout':api?'api-failure':'execution-failure','infrastructure','high',timed?'Evaluation timed out':api?'API or network blocked evaluation':'Evaluation execution failed','The recorded execution error interrupted this evaluation; it does not establish an HMI defect.',[],{failure});
 }
 const task=result.task||{},final=result.final&&typeof result.final==='object'?result.final:{};
 if(typeof task.field==='string'&&Object.hasOwn(final,task.field)&&final[task.field]!==task.expected){
  add('task-target-unmet','functionality',infrastructure?'low':'high','Requested final state was not reached',infrastructure?'The recorded final state differs from the target during an interrupted evaluation; investigate the execution error before attributing this to the HMI.':'The final verifier state differs from the requested target. This identifies the observed outcome, not its cause.',[],{field:task.field,expected:task.expected,actual:final[task.field]});
 }
 const invariants=task.invariants&&typeof task.invariants==='object'?task.invariants:{};
 const violated=Object.entries(invariants).filter(([key,value])=>Object.hasOwn(final,key)&&final[key]!==value).map(([field,expected])=>({field,expected,actual:final[field]}));
 if(violated.length)add('invariant-violated','functionality','high','A protected final-state value changed','Recorded verifier state violates a task invariant. Inspect the trace before attributing the change to a particular input.',[],violated);
 if(result.success===false&&!infrastructure&&!issues.some(i=>i.category==='functionality'))add('task-not-verified','functionality','medium','Task success was not verified','The recorded verifier reported failure, but the available fields do not identify a specific state mismatch.',[],{success:false,final});
 const stale=rows.flatMap((step,index)=>step?.stale?[index]:[]);
 if(stale.length)add('stale-observation','infrastructure','low','Stale observations were rejected','The visible control inventory changed before input delivery. These stale choices were not executed.',stale,{count:stale.length});
 const executed=rows.flatMap((step,index)=>step?.executed?[{step,index}]:[]);
 const small=executed.filter(({step})=>Number.isFinite(step.action?.width)&&Number.isFinite(step.action?.height)&&Math.min(step.action.width,step.action.height)<44);
 if(small.length)add('small-target','usability','medium','Executed controls were smaller than the screenshot threshold','At least one executed control has a dimension below 44 screenshot pixels. This is a model-inspection signal, not a physical-size, human-usability, or safety finding.',small.map(v=>v.index),small.map(({step})=>({label:step.action?.label,width:step.action?.width,height:step.action?.height,threshold:44,unit:'screenshot pixels'})));
 const comparable=(value:any)=>value&&typeof value==='object'?Object.fromEntries(Object.entries(value).filter(([key])=>!['ready','timestamp','observedAt'].includes(key)).sort(([a],[b])=>a.localeCompare(b))):null;
 const noEffect=executed.filter(({step})=>{const before=comparable(step.before),after=comparable(step.after);return before&&after&&Object.keys(before).length&&JSON.stringify(before)===JSON.stringify(after);});
 if(noEffect.length)add('no-observed-effect','functionality','low','Some inputs produced no observed state change','Before/after verifier state was identical. The input may be intentionally idempotent, already selected, or at a boundary; this alone does not prove a defect.',noEffect.map(v=>v.index),noEffect.map(({step})=>({label:step.action?.label,before:step.before,after:step.after})));
 const screens:{screen:string,step:number}[]=[];
 for(let index=0;index<rows.length;index++)for(const key of ['before','after']){const screen=rows[index]?.[key]?.screenId;if(typeof screen==='string'&&screens.at(-1)?.screen!==screen)screens.push({screen,step:index});}
 if(typeof final.screenId==='string'&&screens.at(-1)?.screen!==final.screenId)screens.push({screen:final.screenId,step:Math.max(0,rows.length-1)});
 const backtracks=screens.filter((entry,index)=>screens.slice(0,Math.max(0,index-1)).some(previous=>previous.screen===entry.screen));
 if(backtracks.length)add('backtracking','usability','medium','The evaluation revisited earlier screens','The recorded path returned to a previously visited screen. This may reflect task structure or model navigation and requires inspection before calling it an HMI issue.',[...new Set(backtracks.map(v=>v.step))],{path:screens.map(v=>v.screen),revisits:backtracks.length});
 if(screens.length>=4)add('deep-navigation','usability','medium','The task crossed multiple screen transitions','The recorded task involved at least three screen transitions. This flags path depth for investigation; no shortest path or human performance is assumed.',[...new Set(screens.map(v=>v.step))],{path:screens.map(v=>v.screen),transitions:screens.length-1});
 return issues;
}

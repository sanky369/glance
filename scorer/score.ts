export function score(trace:any[],task:any,final:any) {
 const inputs=trace.filter(s=>s.executed);const screens=trace.flatMap(s=>[s.before?.screenId,s.after?.screenId]).concat(final.screenId).filter(Boolean).filter((s,i,a)=>i===0||s!==a[i-1]);const unique=[...new Set(screens)];
 let backtracks=0;for(let i=1;i<screens.length;i++)if(screens[i]!==screens[i-1]&&screens.slice(0,i-1).includes(screens[i]))backtracks++;
 return {scorerVersion:'2-final-screen-inclusive',success:final[task.field]===task.expected&&Object.entries(task.invariants||{}).every(([k,v])=>final[k]===v),actions:inputs.length,screensVisited:unique.length,backtracks,
 smallTargets:inputs.filter(s=>Math.min(s.action.width,s.action.height)<44).length,decisionsCalls:trace.filter(s=>s.requestStarted||s.response).length,
 apiLatencyMs:trace.reduce((n,s)=>n+(s.apiLatencyMs||0),0),inputTokens:trace.reduce((n,s)=>n+(s.response?.usage?.input_tokens||0),0),
 outputTokens:trace.reduce((n,s)=>n+(s.response?.usage?.output_tokens||0),0),threshold:'44 screenshot pixels; not a physical-size or safety threshold'};
}

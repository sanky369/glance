const element=(tag,className,text)=>{const node=document.createElement(tag);if(className)node.className=className;if(text!==undefined)node.textContent=String(text);return node;};
const human=value=>String(value||'Unknown').replaceAll(/[_-]/g,' ');
const short=value=>value?String(value).slice(0,10):'Unknown source';
const revision=run=>run.hmiRevision||run.uiHash;
const family=run=>String(run.taskFamily||run.task?.family||run.taskId?.split(/[-/:.]/)[0]||'unknown');
const outcome=run=>run.taskAvailability===false?'unavailable':run.failure?'interrupted':run.success===true?'pass':run.success===false?'fail':'unknown';
const safeUrl=value=>{if(typeof value!=='string'||!value)return null;try{const url=new URL(value,location.origin);return url.origin===location.origin&&['http:','https:'].includes(url.protocol)?url.pathname+url.search+url.hash:null;}catch{return null;}};

export function initializeReports(container){
 if(!container)return;
 container.innerHTML=`<div class="reports-heading"><h2>Evaluation reports</h2><button type="button" class="reports-refresh">Refresh reports</button></div><p class="reports-disclaimer">Recorded AI evaluations and verifier evidence. These are investigation signals, not human usability research or safety findings.</p><div class="reports-filters"></div><p class="reports-status" role="status">Loading evaluation reports…</p><div class="reports-summary"></div><div class="reports-results"></div><div class="reports-pagination"></div><section class="reports-cycles"><h3>Codex changes and retests</h3><div class="reports-cycle-list"></div></section><section class="reports-implementation"><h3>Codex desktop implementation changes</h3><p>Source fixes made during development are separate from autonomous UI redesign cycles.</p><div class="reports-implementation-list"></div></section>`;
 const find=selector=>container.querySelector(selector),filters=find('.reports-filters'),status=find('.reports-status'),results=find('.reports-results'),summary=find('.reports-summary'),pagination=find('.reports-pagination'),cycleList=find('.reports-cycle-list'),refreshButton=find('.reports-refresh');
 const fields=[['family','Task family'],['variant','Variant'],['outcome','Result'],['category','Issue category'],['cohort','Source cohort'],['revision','Source revision'],['fix','Fix evidence']];
 const selects={};for(const [key,label]of fields){const wrapper=element('label','reports-filter',label);const select=element('select');select.setAttribute('aria-label',label);wrapper.append(select);filters.append(wrapper);selects[key]=select;select.addEventListener('change',()=>{page=0;render();});}
 let data={runs:[],cycles:[]},page=0,disposed=false,request=0,controller=null;const pageSize=12;
 const cohort=run=>!revision(run)?'unknown':revision(run)===(data.currentHmiRevision||data.currentUiHash)?'current':data.benchmark?.originalHmiRevision?revision(run)===data.benchmark.originalHmiRevision?'original':'historical':run.uiHash===(data.benchmark?.originalUiHash||data.originalUiHash)?'original':'historical';
 function setOptions(key,values){const select=selects[key],previous=select.value;select.replaceChildren(new Option('All','all'));for(const [value,label]of values)select.add(new Option(label,value));if([...select.options].some(option=>option.value===previous))select.value=previous;}
 function configure(){
  const unique=values=>[...new Set(values)].sort().map(value=>[value,human(value)]);
  setOptions('family',unique(data.runs.map(family)));setOptions('variant',unique(data.runs.map(run=>String(run.variant||'unknown'))));
  setOptions('outcome',[['pass','Passed'],['fail','Failed verifier'],['interrupted','Interrupted execution'],['unavailable','Unavailable in sample'],['unknown','Unknown']]);
  setOptions('fix',[['linked','Linked passing retest'],['unverified','Not yet verified']]);
  setOptions('category',[['functionality','Functionality'],['usability','Usability'],['infrastructure','Infrastructure'],['none','No extracted issues']]);
  setOptions('cohort',[['current','Current UI'],['original','Original benchmark UI'],['historical','Other historical UI'],['unknown','Unknown source']]);
  setOptions('revision',[...new Set(data.runs.map(run=>revision(run)||'unknown'))].sort().map(hash=>[hash,hash==='unknown'?'Unknown source':`${short(hash)}${hash===(data.currentHmiRevision||data.currentUiHash)?' · Current UI':''}`]));
 }
 function resolutionFor(issue){const resolution=issue.resolution;if(!resolution||resolution.label!=='Not observed in linked fresh retest'||!resolution.cycleId||!safeUrl(resolution.diffUrl))return null;const run=data.runs.find(candidate=>candidate.id===resolution.retestRunId);return run?.success===true&&!run.failure?resolution:null;}
 function issueMatches(issue){return(selects.category.value==='all'||issue.category===selects.category.value)&&(selects.fix.value==='all'||(selects.fix.value==='linked'?!!resolutionFor(issue):!resolutionFor(issue)));}
 function matched(run){const issues=Array.isArray(run.issues)?run.issues:[];return(selects.family.value==='all'||family(run)===selects.family.value)&&(selects.variant.value==='all'||(run.variant||'unknown')===selects.variant.value)&&(selects.outcome.value==='all'||outcome(run)===selects.outcome.value)&&(selects.cohort.value==='all'||cohort(run)===selects.cohort.value)&&(selects.revision.value==='all'||(revision(run)||'unknown')===selects.revision.value)&&(selects.category.value==='none'?issues.length===0&&selects.fix.value==='all':selects.category.value==='all'&&selects.fix.value==='all'||issues.some(issueMatches));}
 function runLink(runId,label){const link=element('a','reports-retest-link',label||`Inspect retest ${runId}`);link.href=`#reports-run-${encodeURIComponent(runId)}`;link.onclick=event=>{const index=data.runs.findIndex(run=>run.id===runId);if(index<0)return;event.preventDefault();for(const select of Object.values(selects))select.value='all';page=Math.floor(index/pageSize);render();const target=document.getElementById('reports-run-'+runId);const details=target?.querySelector('details');if(details)details.open=true;target?.scrollIntoView({behavior:'smooth',block:'start'});};return link;}

 function issueCard(issue,run){
  const card=element('article',`reports-issue reports-severity-${issue.severity||'low'}`);card.append(element('h5',null,issue.title),element('p','reports-issue-meta',`${human(issue.category)} · ${human(issue.severity)} · ${human(issue.status||'observed')}`),element('p',null,issue.detail));
  const steps=Array.isArray(issue.evidence?.steps)?issue.evidence.steps:[];
  if(steps.length)card.append(element('p','reports-evidence',`Trace steps: ${steps.map(index=>Number(index)+1).join(', ')}`));
  const observations=issue.evidence?.observed;
  if(Array.isArray(observations)&&issue.rule==='small-target')card.append(element('p','reports-evidence',observations.map(item=>`${item.label||'Control'}: ${Number(item.width).toFixed(1)} × ${Number(item.height).toFixed(1)} screenshot px`).join(' · ')));
  if(observations?.field)card.append(element('p','reports-evidence',`${human(observations.field)} · Expected ${observations.expected} · Observed ${observations.actual}`));
  if(Array.isArray(observations)&&issue.rule==='invariant-violated')card.append(element('p','reports-evidence',observations.map(item=>`${human(item.field)}: expected ${item.expected}, observed ${item.actual}`).join(' · ')));
  for(const screenshot of (issue.evidence?.screenshots||[]).slice(0,3)){if(!/^[a-zA-Z0-9_.-]+\.png$/.test(screenshot))continue;const link=element('a','reports-evidence-link',`View ${screenshot}`);link.href=`/runs/${encodeURIComponent(run.id)}/${encodeURIComponent(screenshot)}`;link.target='_blank';link.rel='noopener';card.append(link);}
  const resolution=resolutionFor(issue);
  if(resolution){const evidence=element('div','reports-resolution');evidence.append(element('p',null,'Not observed in linked fresh retest'),element('p',null,`Cycle ${resolution.cycleId}. This is evidence from one linked retest, not a permanent fix guarantee.`));const diff=element('a','reports-diff-link','Inspect source change');diff.href=safeUrl(resolution.diffUrl);diff.target='_blank';diff.rel='noopener';evidence.append(diff,runLink(resolution.retestRunId));card.append(evidence);}else card.append(element('p','reports-resolution-pending','No linked passing retest verifies this observation yet.'));
  card.append(element('p','reports-source',`Evidence ${issue.id} · HMI ${short(revision(run))} · UI source ${short(issue.sourceHash)}`));return card;
 }
 function runCard(run){
  const card=element('article','reports-run');card.id='reports-run-'+run.id;const head=element('div','reports-run-heading');head.append(element('h3',null,human(run.taskId)),element('span',`reports-outcome reports-outcome-${outcome(run)}`,human(outcome(run))));card.append(head);
  card.append(element('p','reports-run-meta',`${human(family(run))} · ${human(run.variant)} · ${human(cohort(run))} HMI · ${short(revision(run))}`));
  if(run.task?.instruction)card.append(element('p','reports-instruction',run.task.instruction));
  const metrics=element('dl','reports-metrics');for(const [label,value]of [['Inputs',run.actions],['Screens',run.screensVisited],['Backtracks',run.backtracks],['Small targets',run.smallTargets],['Duration',Number.isFinite(run.elapsedMs)?`${(run.elapsedMs/1000).toFixed(1)} s`:undefined]]){metrics.append(element('dt',null,label),element('dd',null,value??'Not recorded'));}card.append(metrics);
  if(run.failure)card.append(element('p','reports-run-error',`Execution error: ${run.failure}`));
  const details=element('details','reports-run-details'),allIssues=Array.isArray(run.issues)?run.issues:[],visibleIssues=allIssues.filter(issueMatches);
  details.append(element('summary',null,`${visibleIssues.length} observed ${visibleIssues.length===1?'issue':'issues'} · Run ${run.id}`));
  if(visibleIssues.length){for(const issue of visibleIssues)details.append(issueCard(issue,run));}else details.append(element('p',null,'No issues extracted for this selection. This does not establish complete usability or functionality coverage.'));
  const final=run.final&&typeof run.final==='object'?Object.entries(run.final).filter(([,value])=>value===null||['string','number','boolean'].includes(typeof value)):[];
  if(final.length){details.append(element('h4',null,'Recorded final state'));const values=element('dl','reports-final-state');for(const [key,value]of final)values.append(element('dt',null,human(key)),element('dd',null,value));details.append(values);}
  if(/^[a-zA-Z0-9_.-]+\.png$/.test(run.finalScreenshot||'')){const link=element('a','reports-evidence-link','View final screenshot');link.href=`/runs/${encodeURIComponent(run.id)}/${encodeURIComponent(run.finalScreenshot)}`;link.target='_blank';link.rel='noopener';details.append(link);}card.append(details);return card;
 }
 function renderCycles(){
  cycleList.replaceChildren();const runMap=new Map(data.runs.map(run=>[run.id,run]));
  for(const cycle of (Array.isArray(data.cycles)?data.cycles:[]).slice().reverse().slice(0,12)){
   const card=element('article','reports-cycle'),diff=safeUrl(cycle.diffUrl),applied=cycle.uiHash||cycle.proposal?.uiHash;
   const retests=(Array.isArray(cycle.runs)?cycle.runs:[]).filter(ref=>/^retest|^after|^revised/.test(ref.label||'')).map(ref=>runMap.get(ref.id)).filter(Boolean);
   const passing=retests.filter(run=>run.success===true&&!run.failure&&run.uiHash===applied),taskCount=new Set(passing.map(run=>run.taskId)).size;
   const verified=cycle.status==='succeeded'&&!cycle.rollback&&!!diff&&!!applied&&!!cycle.parentHash&&applied!==cycle.parentHash&&retests.length>=2&&passing.length===retests.length&&taskCount>=2;
   const label=cycle.rollback?'Change rolled back':verified?`Source change verified on ${taskCount} tasks`:cycle.status==='failed'?'Cycle failed':cycle.proposal?.outcome==='already_optimized'?'No source change proposed':cycle.status==='running'?'Cycle in progress':'Change evidence incomplete';
   card.append(element('h4',null,label),element('p','reports-cycle-meta',`${cycle.id||'Unnamed cycle'} · ${human(cycle.stage||cycle.status)}`));
   if(cycle.proposal?.summary)card.append(element('p',null,cycle.proposal.summary));
   card.append(element('p',null,`Parent ${short(cycle.parentHash)} → Applied ${short(applied)}`));
   if(diff){const link=element('a','reports-diff-link','Inspect actual source diff');link.href=diff;link.target='_blank';link.rel='noopener';card.append(link);}else card.append(element('p',null,'No linked source diff is available.'));
   if(retests.length)card.append(element('p','reports-retests',`Linked passing retests on this source: ${passing.length}/${retests.length}. ${retests.map(run=>`${human(run.taskId)} · ${run.id}`).join('; ')}`));
   if(cycle.error)card.append(element('p','reports-run-error',cycle.error));
   card.append(element('p','reports-cycle-limit',verified?'These retests verify the linked task outcomes on the changed source. They do not prove that every observed issue or HMI task is fixed.':'A proposal or model claim alone is not a verified fix. Observed issues retain their original evidence and status.'));cycleList.append(card);
  }
  if(!cycleList.children.length)cycleList.append(element('p',null,'No archived or local Codex cycle evidence is available.'));
 }
 function renderImplementationFixes(){
  const list=find('.reports-implementation-list');list.replaceChildren();
  for(const fix of Array.isArray(data.implementationFixes)?data.implementationFixes:[]){
   const card=element('article','reports-implementation-fix');card.append(element('h4',null,fix.title||fix.id||'Implementation change'));
   if(fix.summary||fix.description)card.append(element('p',null,fix.summary||fix.description));
   if(Array.isArray(fix.sourceFiles))card.append(element('p','reports-source',`Source: ${fix.sourceFiles.join(', ')}`));
   const ids=fix.retestRunIds||fix.finalTestIds||fix.runIds||[],runs=(Array.isArray(ids)?ids:[]).map(id=>data.runs.find(run=>run.id===id)).filter(Boolean),passing=runs.filter(run=>run.success===true&&!run.failure);
   card.append(element('p',null,`Linked passing final evaluations: ${passing.length}/${runs.length}. These results verify their recorded task outcomes, not causality or every issue.`));
   for(const run of runs)card.append(runLink(run.id,`${human(run.taskId)} · ${human(outcome(run))} · ${run.id}`));
   const url=safeUrl(fix.diffUrl);if(url){const link=element('a','reports-diff-link','Inspect implementation source diff');link.href=url;link.target='_blank';link.rel='noopener';card.append(link);}else card.append(element('p',null,'No linked implementation diff is available.'));
   list.append(card);
  }
  if(!list.children.length)list.append(element('p',null,'No separate desktop implementation fixes are recorded.'));
 }
 function render(){
  if(disposed)return;const filtered=data.runs.filter(matched);page=Math.min(page,Math.max(0,Math.ceil(filtered.length/pageSize)-1));results.replaceChildren();summary.replaceChildren();pagination.replaceChildren();
  const issues=filtered.flatMap(run=>Array.isArray(run.issues)?run.issues:[]).filter(issueMatches);
  summary.append(element('p',null,`${filtered.length} of ${data.runs.length} evaluations · ${filtered.filter(run=>outcome(run)==='pass').length} passed · ${issues.length} matching observed issues`));
  for(const category of ['functionality','usability','infrastructure'])summary.append(element('span',`reports-category reports-category-${category}`,`${human(category)}: ${issues.filter(issue=>issue.category===category).length}`));
  for(const run of filtered.slice(page*pageSize,(page+1)*pageSize))results.append(runCard(run));
  if(!filtered.length)results.append(element('p','reports-empty','No evaluations match these filters. Change a filter or refresh after a run completes.'));
  if(filtered.length>pageSize){const previous=element('button',null,'Previous'),next=element('button',null,'Next');previous.type=next.type='button';previous.disabled=page===0;next.disabled=(page+1)*pageSize>=filtered.length;previous.onclick=()=>{page--;render();};next.onclick=()=>{page++;render();};pagination.append(previous,element('span',null,`Page ${page+1} of ${Math.ceil(filtered.length/pageSize)}`),next);}
  status.textContent=`Reports loaded · ${new Date().toLocaleTimeString()}`;renderCycles();renderImplementationFixes();
 }
 async function refresh(){const current=++request;controller?.abort();controller=new AbortController();refreshButton.disabled=true;status.textContent='Loading evaluation reports…';try{const response=await fetch('/api/runs',{signal:controller.signal});if(!response.ok)throw new Error(`Reports request failed (${response.status})`);const value=await response.json();if(disposed||current!==request)return;data={...value,runs:Array.isArray(value.runs)?value.runs:[],cycles:Array.isArray(value.cycles)?value.cycles:[]};configure();render();}catch(error){if(disposed||current!==request)return;status.textContent=error.name==='AbortError'?'Report request cancelled':`${error.message}. Existing reports remain visible; refresh to retry.`;}finally{if(!disposed&&current===request)refreshButton.disabled=false;}}
 const runsUpdated=()=>{if(!disposed)void refresh();};document.addEventListener('glance:runs-updated',runsUpdated);refreshButton.addEventListener('click',refresh);void refresh();return{refresh,destroy(){disposed=true;++request;controller?.abort();document.removeEventListener('glance:runs-updated',runsUpdated);container.replaceChildren();}};
}

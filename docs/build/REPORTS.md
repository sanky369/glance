# Evaluation reports and observed issues

Reports describe recorded model-driven evaluations. They are not human usability research, driver-distraction measurements, or safety findings.

## Failure modes considered before implementation

- Failed API/Unity execution must not become a claim of a defective HMI.
- Missing final state must not count as a proven invariant violation.
- Idempotent or boundary inputs may produce no state change; this observation alone must not be called a functional defect.
- Screenshot pixel sizes must not imply physical target size or safety compliance.
- A longer task requiring repeated adjustments must not be mislabeled as deep navigation solely from input count.
- Old results with variant `baseline` or `revised` must not be treated as current evidence when source hashes differ.
- A successful Codex proposal, model claim, or compile must not mark an issue fixed without an applied source diff and real passing retests.
- Missing results, malformed optional fields, unavailable diff routes, and fetch failures must leave a usable, truthful interface.
- Fix evidence must not cross-link unrelated baseline runs or tasks.
- Filter changes must preserve the dataset and must not start API jobs, Unity actions, or source edits.

## Integration contract

`scorer/issues.ts` exports `extractIssues(result, trace = result.trace || [])`. Import it in the server and attach the resulting array as `run.issues` while loading each result. No state is written. IDs are deterministic for the same run, source, rule, and evidence steps. Each issue includes `id`, `runId`, `sourceHash`, `category`, `severity`, `status`, `title`, `detail`, `rule`, and `evidence`.

Serve `dashboard/reports.js` at `/reports.js`, add a container, and call `initializeReports(container)` from a module script. It fetches `/api/runs`, exposes a Refresh button, and returns `{refresh, destroy}`. No polling or execution actions occur. A document-level `glance:runs-updated` event triggers a read-only refresh, preserving available filter selections; the host dispatches this only when the newest completed run ID changes. Destroy removes the event listener. Styles use `reports-*` classes.

The API supplies `runs`, `currentUiHash`, `benchmark.originalUiHash`, and optional `cycles`. Each run retains its original task, final state, trace, verifier success/failure, source hash, and attached issues. Optional `taskFamily` or `task.family` sets task grouping; otherwise the prefix of taskId is used. Cycle entries use existing manifest fields `id`, `status`, `stage`, `parentHash`, `uiHash`, `proposal`, `comparison`, `runs`, `rollback`, `error`, and an optional server-issued `diffUrl`. Each cycle run reference must contain its real run ID and phase label.

## Rules and limitations

Functionality observations include an unmet recorded task target, a final invariant violation, and an executed input with no observed state change. The last is explicitly ambiguous, since idempotent and boundary inputs can be intentional. Infrastructure observations include API/network failures, timeouts, runner failures, and stale rejected observations. Usability observations include multiple screen transitions, repeated screens/backtracking, and executed controls below 44 screenshot pixels. These are investigation signals rather than causal diagnoses. Issue status stays `observed`. The server may attach `resolution={cycleId,retestRunId,diffUrl,label:"Not observed in linked fresh retest"}` only when an actual succeeded source-change cycle links this exact baseline run to a passing same-task retest on the applied source and that issue rule is absent there. The report displays the cautious label, links the real source diff/retest, and never presents a permanent fix. Fix evidence filters distinguish linked passing retests from observations not yet verified. Category and fix-evidence filters must match the same issue.

Original/current cohorts are determined from source hashes. A cycle card reports verified source-change retests only when a succeeded cycle includes a differing parent/applied hash, an actual server-issued diff URL, and at least two linked real passing retests for distinct tasks on the applied source. It does not claim that every issue or full-HMI task is fixed. Proposed, rolled-back, failed, missing-diff, and incomplete evidence remain clearly labeled.

## Acceptance checks

Load real archived and local results, then filter by task family, variant, outcome, issue category, and source cohort/revision. Confirm counts, visible cards, and empty states reflect the combined filters. Check recorded small-target and no-effect evidence against the linked trace steps/screenshots. Confirm missing source hashes form an unknown cohort. Confirm cycle proposal-only and failed/rollback cards never say verified; verify every displayed diff URL and retest ID against server evidence.

## Verification artifact — 2026-11-01T14:54:12.873Z

Both source files pass `node --check`. A read-only pass over 33 real archived result/trace pairs verified deterministic extraction, run/source ownership, `observed` status only, and valid trace indices. Examples:

| Real archived run | Extracted observations |
| --- | --- |
| `2026-11-01T13-07-02-767Z-radio-baseline` | Small target; multiple screen transitions |
| `2026-11-01T13-16-05-156Z-climate-baseline` | Execution timeout; target unmet during interruption; small target |
| `2026-11-01T13-30-36-950Z-radio-revised` | No extracted issues |
| `2026-11-01T14-05-49-485Z-climate-baseline` | Small target; input with no observed effect |

Repeat against the current archive without API calls or Unity actions:

```sh
node --input-type=module <<'JS'
import {readdir,readFile} from 'node:fs/promises';
import {extractIssues} from './scorer/issues.ts';
let checked=0;
for(const entry of await readdir('docs/build/evidence',{withFileTypes:true})){
  if(!entry.isDirectory())continue;
  const base=`docs/build/evidence/${entry.name}`;
  const result=JSON.parse(await readFile(`${base}/result.json`,'utf8'));
  const trace=JSON.parse(await readFile(`${base}/trace.public.json`,'utf8'));
  const issues=extractIssues(result,trace);
  if(JSON.stringify(issues)!==JSON.stringify(extractIssues(result,trace)))throw Error('Unstable extraction');
  if(issues.some(issue=>issue.runId!==result.id||issue.sourceHash!==result.uiHash||issue.status!=='observed'))throw Error('Evidence ownership');
  if(issues.some(issue=>issue.evidence.steps.some(index=>index<0||index>=trace.length)))throw Error('Trace reference');
  checked++;
}
console.log(JSON.stringify({checked,passed:true}));
JS
```

This check covers extraction on real recorded evaluations. Browser filtering, cycle-link presentation, and final server integration remain to be checked in the real dashboard by the lead.


## Separate implementation changes

Optional `data.implementationFixes[]` entries represent Codex desktop development work, separate from autonomous UI redesign cycles. Supported fields are `{id,title,summary,sourceFiles?,diffUrl?,retestRunIds?}` (`finalTestIds`/`runIds` are accepted aliases). Cards show only linked real run outcomes and a safe server-issued diff URL when supplied. They do not infer that the change caused a passing result. Retest links navigate to the exact result card and clear conflicting filters.

The final resolution/filter/implementation-card edits are awaiting the lead’s completed server integration before browser testing, as requested.


## Manual recording walkthrough

1. Open **Evaluation reports**, click **Refresh reports**, and leave every filter on **All** to show the overall evaluation and issue counts.
2. Set **Issue category** to **Usability** and **Source cohort** to **Original benchmark UI**. Expand a run’s **observed issues** row and use **View 000.png** or the available screenshot links to inspect its recorded target/path evidence.
3. Change **Fix evidence** to **Linked passing retest**. Expand an issue labeled **Not observed in linked fresh retest**. Open **Inspect source change**, then **Inspect retest …** to navigate to its actual result. Retest navigation intentionally resets conflicting filters.
4. Return **Fix evidence** to **All**. Set **Source cohort** to **Current UI**, then select a **Task family** and **Result → Passed** to inspect the current successful task evaluations. Use **Variant** and **Source revision** when comparing exact source cohorts.
5. Set **Result → Interrupted execution** and **Issue category → Infrastructure** to distinguish execution/API failures from functionality observations. Clear incompatible filters to **All** if the view is empty.
6. Set **Fix evidence → Not yet verified** to show observations without linked passing retests. These remain investigation items; they are not automatically confirmed defects.
7. Show **Codex changes and retests** and **Codex desktop implementation changes** separately. Explain that autonomous UI changes and development fixes have different evidence; no card promises every issue is permanently fixed.
8. During a separately authorized live task, keep one filter selected. When the task finishes, confirm the reports refresh automatically and retain that selection. No report control itself starts a live evaluation.

The final event-listener integration has not been exercised while the live suite is busy.

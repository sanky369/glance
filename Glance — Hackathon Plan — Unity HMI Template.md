# Glance — Hackathon Plan (Unity HMI + OpenAI Decisions API)

Updated 1 November 2026 · Decisions API revision of the original 31 October plan

**Status:** this document specifies the next build. The Unity template is running in `unity-hmi/`; Pointer navigation and driver-temperature adjustment to 21°C are now verified (see `docs/unity-hmi/POINTER-AUTOMATION.md`). Decisions API integration, radio task and the full evaluation pipeline remain pending. See `docs/unity-hmi/SETUP.md` for verified setup evidence. The existing architecture PNG predates Decisions; the text architecture below supersedes its generic agent block.

Build Glance at the DevDay Community Hackathon in London on Sunday 1 November: an agent powered by **OpenAI Decisions API** tests a **synthetic automotive HMI**, Glance records interaction friction, Codex revises the HMI, and a fresh agent checks that the improvement worked without breaking climate control. **Preferred visual and HMI foundation:** Unity Industry’s new Automotive HMI Template (Unity 6.3+). **Must-work fallback:** the original HTML HMI, with the Unity demo displayed separately if feasible. The six-hour hackathon schedule and scientific claims remain unchanged.

## The idea in one minute

**Pitch:** "Glance gives a real-time 3D automotive HMI an AI usability walkthrough: Decisions selects the agent’s next interaction, Glance measures friction, Codex improves the UI, and a fresh agent proves the fix before human trials."

Glance is an **automated, instrumented walkthrough inspired by cognitive walkthroughs**. The Decisions agent receives a screenshot and a list of currently visible controls. That list assists discovery: results describe this agent and observation setup, not human discoverability or a purely visual inspection.

Why it can win:

- **Domain credibility.** An automotive human-factors problem with an actual interactive HMI and live driving context, rather than another generic agent.
- **The architecture judges rewarded.** Winners like Echo Canvas, Pulse and Sentinel kept the model inside constraints and let deterministic code do the measuring. Glance does the same.
- **Decisions is in the core journey.** Every test action is selected by the Decisions API from the current visible-action set; it is not an optional classifier beside a scripted demo.
- **Codex does real work.** It edits an isolated HMI variation in the Unity project from measured evidence, not merely a chatbot. If Unity integration fails the early gate, the same loop targets the original HTML HMI.
- **Most demos skip the regression check.** Proving a second task still works makes this read like real engineering.
- **Timely.** Interface usability matters in 2026 automotive safety assessments. Unity’s new Industry HMI Template provides ready-made 3D cockpit/HMI content for a credible demonstration; this does not confer compliance or distraction-validation claims.

## The day

You have six hours of build time, 11:00–17:00, and doors close at 09:30 sharp.

| Time | What happens | Your move |
| --- | --- | --- |
| 09:00 | Arrivals, coffee, team finding | Pitch Glance in one sentence; recruit (see Team) |
| 09:30 | Doors close, no entry after | Be inside by 09:15 |
| 10:00 | Opening brief | Note the judging criteria, judges and any tool rules |
| 11:00 | Build starts | Follow the build plan |
| 13:00 | Lunch | Eat at the laptop; agree the 15:00 checkpoint |
| 17:00 | Submissions close, dinner, first-round judging | Submit by 16:45 |
| 18:00 | Final demos | Run the demo script |
| 19:00 | Awards, close by 19:30 |  |

**Rules we know:** max 4 people per team; come with a team or form one on the day.

**Prizes:** tickets to the official DevDay Exchange London (3 Nov), OpenAI API credits, ChatGPT Pro subscriptions.

**Verified Builders Guide:** existing ideas, libraries, APIs, templates and boilerplate are allowed with credit; build the core during the event. Judging equally weights usefulness, Codex use, execution and demo clarity. Submission video: at most 90 seconds. Finalists: two-minute demo plus up to one minute of questions. Follow updated opening-brief instructions; see `BUILD-BRIEF.md` for the submission checklist.

## Scope and the Unity template decision

**Core promise stays the same:** one HMI, two tasks, one authentic code redesign, independent scorer, and before/after test evidence. **Unity is a test subject and simulation context, not a proxy for human glance behaviour.**

**Official sample selected:** [New for Unity Industry: Automotive HMI sample for embedded-style hardware](https://unity.com/blog/unity-industry-hmi-template-for-embedded-systems), published 6 October 2026. Unity says the **new** template is available as a new project in **Unity Hub with Unity 6.3 or later**. It is an update to the older 2021 Automotive HMI Template; do **not** assume the older Asset Store package is the same project.

### What the Unity article actually confirms

| Confirmed in the new sample | Why it helps Glance | What must not be assumed |
| --- | --- | --- |
| Digital instrument cluster with vehicle and warning data | Credible dashboard in the demo | No standardised Glance state API is promised |
| Interactive 3D vehicle and city driving simulation | Moving vehicle, traffic, lanes, signals, pedestrians, crosswalks | Not necessarily a manually drivable first-person car game |
| HVAC, body controls, ADAS visualization | Realistic regression task: cabin temperature | No pre-existing radio-station task is confirmed |
| Separated **data / presentation UI / 3D simulation** layers | Good place to add a small independent HMI adapter | Exact prefab, class, event and UI toolkit names must be inspected |
| Centralised themes | Before/after visual polish | Theme changes alone do not demonstrate usability improvements |
| Runs up to 60 fps on Raspberry Pi 5 in Unity’s example | Shows embedded hardware focus | No guarantee for this laptop, WebGL, browser automation or the Glance demo |

**Practical conclusion:** reuse the template’s 3D scene and climate/vehicle state; build a tiny, synthetic **Radio** task UI in a designated Glance-owned layer. Do not spend hackathon time making a realistic car physics simulator. Avoid using any proprietary vehicle models or internal company data.

### Primary track: Unity-native HMI actually under test

- **Unity project:** new Unity Industry Automotive HMI Template (6.3+) with existing vehicle/city simulation and HVAC screen.
- **Glance-owned UI variant:** deliberately bury a radio preset behind `Media > Sources > FM > Presets` and make key targets small. Add a selectable preset as an explicit synthetic task. This is *our implementation*, not a feature promised by Unity’s article.
- **Task A (primary):** change the radio to a predefined preset; a C# state adapter confirms the station ID.
- **Task B (regression):** adjust cabin temperature to 21°C; a C# state adapter confirms the actual climate value. Wire to the sample’s actual climate system *after inspecting its code*, or use a labelled synthetic climate adapter if integration is too slow.
- **Test surface:** try a **Web build** and Playwright driving the Unity canvas *only after a 30–45 minute smoke test*. Web target compatibility is **unverified** by the blog. A native desktop build can instead be automated with real computer-use/screenshot-and-mouse controls when available.
- **Codex change scope:** `unity-hmi/Assets/Glance/UI/` only (or equivalent user-created UI/prefab overrides). Template baseline, state bridge, evaluator and task definitions remain fixed. Restrict write access using a staging copy or permissions; a `codex -C` working directory alone is not an enforcement boundary.
- **Simulation:** use the template’s existing city/traffic motion as a visual backdrop. Fixed test state and reproducible UI inputs are more important than physics fidelity.

### Must-work fallback: original HTML HMI

Keep the same Decisions selector when changing test surface. If the Unity template is unavailable, cannot be licensed/loaded, fails Web build or automated clicking, or consumes the core build window, **immediately revert to the original 1280×720 React/HTML HMI**. Use Playwright to control DOM elements. Display Unity in a separate pane *only if it already runs*. In that fallback, say accurately: **"Glance tested the HTML synthetic HMI; Unity provides the automotive scene"**. Do not imply Glance exercised native Unity controls.

### Claims we do not make

- No predictions of driver glance duration, eyes-off-road time or metres driven blind.
- No NHTSA/Euro NCAP compliance verdicts, driver-safety ratings or proof of reduced cognitive workload.
- No simulated agent equals a human driver; the Unity city simulator is **visual context**, not validated distraction measurement.
- No heatmaps resembling eye tracking: the overlay is a **tap/interaction map**.
- Not a replacement for human trials; an early, reproducible inspection layer before them.

**Keep out:** JLR/private HMI, names, data and internal methods. Use the public Unity template subject to its applicable licence, synthetic radio data and public guidelines. The previously dropped **blind-drive replay** remains dropped.

## How it works — new implementation

The **Decisions API selects one pre-enumerated visible action**; a browser/desktop runtime performs its already-specified input; **locked deterministic code** verifies success and scores friction. Unity provides the HMI and moving city scene where feasible.

1. **Unity project and simulation:** create in Unity Hub with Unity 6.3+; inspect its data, UI and vehicle layers. Reuse digital cluster, city simulation and climate UI. Keep the traffic/vehicle simulation autonomous; no manual driving or true driver-attention simulation required.
2. **Glance state adapter (C#):** expose only task-relevant test state such as `radio.stationId`, `climate.tempC`, `screenId`, and `sessionId`. Implement the adapter against the sample’s *actual* state/events after inspection. Do not assume `window.hmiState` exists in Unity.
3. **Locked task specs:** outside the editable UI folder, store Task A desired preset ID and Task B target `21`, with objective checks against exported Unity state; do not accept the agent’s self-report.
4. **Test runtime (decision gate):** if Web build works, Playwright captures a screenshot of the Unity canvas and clicks **canvas-relative x/y coordinates**. Unity renders mostly to a canvas, so DOM locators are not reliable for inside-canvas buttons. Decisions sees the screenshot, visible-action descriptions and within-run action history, not hidden `state`, task predicates or the test checker. The runner supplies complete input arguments for each candidate; the API chooses a value rather than inventing coordinates. If using desktop Unity, use real screenshot/mouse input and a read-only state export.
5. **Browser/Unity read-only bridge:** for Web build, use a small Unity C# → browser JavaScript `.jslib` callback/event to publish state snapshots for **test verification**; keep that read-only channel inaccessible to action-selection prompts. Host `window.glanceState` is *our new bridge*, not a Unity template feature. For desktop, export state to a local JSON file or a simple local HTTP receiver. Synchronise timestamps and reset state for each run.
6. **Trace logger:** also retain the observation revision, candidate list, Decisions response/request identifier when available, selected choice, model, confidence/probabilities, API latency/usage, executor outcome and stale/error status. Store action step, `screenId`, element label (if instrumented), click coordinate, canvas/screen bounds, timestamp and screenshot at `runs/<id>/trace.json`. On-screen numbered dots constitute an **interaction map**, not eye tracking.
7. **Deterministic scorer:** compare exported state against locked tasks and count actions, distinct screens, backtracks and small targets. Use target dimensions in **Unity logical pixels or explicitly calibrated px/mm**; do not assert millimetres from WebGL pixels without calibration.
8. **Findings → Codex:** give Codex `findings.json`, failure screenshots and rules; modify **Glance-owned Unity UI only** (the actual UXML/USS, uGUI prefab-backed C# or other UI technology found during inspection). Show a diff. Do not depend on knowing the template's internal UI framework beforehand.
9. **Fresh retest:** reset task, simulator state, initial view, viewport, action cap and model prompt; a fresh agent reruns Task A, then separately Task B. Repeat each three times if time allows; keep all failed runs.

### Decisions API integration — core design

**Official API facts, checked 1 November:** use `POST /v1/decisions` with `model: "gpt-6-luna"`, `input`, and a named `choice` question in `questions`. Read its matching entry from `answers`. Choices contain unique `value` fields and optional descriptions; supply 2–255 options. Text and image inputs are supported. Confirm project access before depending on a live run. [Guide](https://developers.openai.com/api/docs/guides/decisions), [request reference](https://developers.openai.com/api/reference/resources/decisions/methods/create).

The following is **Glance's proposed integration contract**, not additional API fields or an implemented feature:

1. **Capture a settled observation.** Save the tested HMI screenshot and a control/layout revision. The revision tracks actionable UI state, not every traffic-simulation frame or clock tick. The model receives only the HMI crop, never the results dashboard or verifier state.
2. **Enumerate the visible actions.** A read-only Unity UI adapter discovers enabled, on-screen, non-occluded uGUI controls; the HTML fallback uses the equivalent rendered controls. Supply neutral per-observation IDs and only visible labels/appearance. Do not expose internal object names, hidden menu destinations, hidden station state or an optimal route. Icon-only controls remain icon-only observations. Keep a complete binding to click coordinates or a bounded drag path in the executor, with render-space calibration. A visible temperature selector may expose discrete visible choices with fully specified gestures; no direct `CarData` setter is an action.
3. **Ask one choice question: `next_action`.** Input consists of task wording, current screenshot, visible candidates, and a bounded history from this run. Choices map to executable inputs, plus `report_done` and `stop_unable` (which also ensure a valid choice set when no controls exist). Allow no more than 253 executable candidates plus the two stop choices. If that limit is exceeded, fail the observation and simplify the synthetic UI; do not remove task-irrelevant distractors to fit. Descriptions must not recommend the correct option. There is no scripted radio route and no unrestricted generated code or coordinates.
4. **Validate before execution.** Require the named answer to select a current allowed value. Match its observation revision, refresh hit testing and reject an action if a modal, transition or layout change invalidated its target. Never reuse a choice after navigation. Execute exactly the selected input, then recapture and read state on the separate verification channel. `report_done` only requests the deterministic success check; a false claim is a recorded unsuccessful stop.
5. **Bound the run and preserve failure.** Freeze policy before baseline: at most 25 executed HMI actions, 40 Decisions calls including retries/recaptures, and five minutes per run, with cancellation and a configured API spend limit. Invalid/missing answers, API failures and repeated no-effect actions produce bounded recovery or a retained failure; never substitute the successful action manually. Wait for UI transitions in the runtime rather than adding unbounded model-selected waits.
6. **Record the causal evidence.** Each trace links screenshot → candidate set → actual Decisions choice → actual input → resulting state check. The dashboard shows the chosen control, API call count/latency and verified outcome. Confidence is diagnostic model output, not proof of success, a human-confusion measure or a friction score. Do not expose private keys or authorization headers in artifacts.

**Measurement control:** freeze candidate-discovery and naming rules outside Codex's editable folder. Regenerate candidates from the redesigned rendered UI rather than reusing baseline coordinates. Keep ordering neutral and consistent; do not prune distractors based on the task. Compare before/after with the same Decisions model, prompt, history policy, viewport and budgets. Retain both the chosen candidates and their bounds so small-target measurements describe actual activated controls. Report API calls separately from actual HMI inputs.

**Why this fits:** Glance already controls its synthetic UI, so it can enumerate available inputs. Decisions chooses among those inputs; Codex writes UI code; deterministic code evaluates the result. The inventory makes this an assisted, instrumented evaluation. The executor must still click or drag the tested interface—calling the sample navigation API or writing state directly is setup, not a valid agent trial. This follows OpenAI's [bounded-choice computer-use guidance](https://developers.openai.com/api/docs/guides/tools-computer-use).

### Architecture

```text
              GLANCE DASHBOARD / TEST COORDINATOR
                              |
        UNITY HMI (or labelled HTML fallback surface)
                 |                         |
       screenshot + visible controls    verifier-only state
                 |                         |
      observation + bound candidate set   locked task checker
                 |                         |
       OPENAI DECISIONS API                |
       choice: next_action                 |
                 |                         |
      validate revision + allowed choice  |
                 |                         |
      calibrated input executor           |
                 |                         |
      real UI input -> observe again      |
                 |                         |
       decisions + input trace ----------> deterministic scorer
                                                |
                                           findings.json
                                                |
                                      CODEX UI-ONLY PATCH
                                                |
                              reset -> fresh Decisions runs
                                                |
                              radio + climate + comparison
```

### Integration gate: resolve first, without guessing

1. Does Unity Hub show the **new 6.3+ Industry HMI template**, and can this laptop open it with an appropriate Unity Industry entitlement/trial? Verify availability and licence before relying on it.
2. Can the example scene run in Play Mode with its existing city simulation and HVAC panel? Observe actual scene/object/state names rather than inventing APIs.
3. Can the selected native runtime capture the HMI and deliver calibrated inputs reliably? Prefer the already-running native sample. A Web build is optional and unverified; lack of Web support alone does not disqualify a working native route.
4. Can the executor perform a verified UI click/drag and the test harness independently read state? Sample API navigation does not satisfy this check.
5. Can the adapter enumerate only visible controls with calibrated input bindings, and can a real Decisions `next_action` call select one that the executor successfully activates?
6. Can the server authenticate to Decisions with the supported model and retain an actual request/response trace?
7. Can Codex edit only a small user-owned UI slice and see the revised UI run without lengthy reimports/rebuilds?

**Original cutoff: 11:45.** If resuming after this checkpoint, do not restart a 45-minute setup window; resolve the remaining input/API gates immediately and protect the 15:00 full-loop checkpoint. If a required capability remains unavailable, switch the *agent-tested* surface to the original HTML HMI. Unity may still be shown as an independent reference scene; do not let it compromise the 15:00 full-loop checkpoint.

**Repository layout:**

```text
glance/
  unity-hmi/                  Unity 6.3+ project created from new Industry HMI Template
    Assets/Glance/
      UI/                    Codex-editable custom radio/HMI UI variation ONLY
      StateBridge/           Verifier-only state exporter, LOCKED
      VisibleActions/        Visible control enumeration, LOCKED
      Tests/                 Optional Unity smoke tests, LOCKED
  hmi-fallback/              HTML HMI; only UI/ is editable by Codex
  tasks/                     Task A and B goals + success predicates, LOCKED
  runner/                    Decisions loop, input executor, trace logger (LOCKED)
    decisions/               API client, prompt, choice validation, budgets
    observations/            Screenshot + candidate snapshots/revisions
  scorer/                    Deterministic metrics + interaction-map renderer, LOCKED
  runs/                      Per-run screenshots, state, traces and results
  dashboard/                 Comparison and demo playback
  findings.json              Scorer output used as Codex input
```

**Codex edit rule:** in primary mode, expose only a *copy* of `unity-hmi/Assets/Glance/UI/` to Codex, then review/apply changed files back; never allow uncontrolled changes to Unity template internals, `tasks/`, `runner/`, `scorer/`, or the state bridge. In fallback mode Codex edits only `hmi-fallback/UI/`; keep the state adapter and visible-action discovery outside that writable slice.

## What we measure

Keep task predicates and metric definitions fixed before the baseline run; do not let Codex alter them.

| Metric | How it's computed | Limit / interpretation |
| --- | --- | --- |
| Task success | Exported `stationId == target` or `tempC == 21` | Verified program state, **not** model saying "done" |
| Actions | Number of actual taps/clicks/swipes in trace | A rough interaction-effort proxy |
| Screens visited | Distinct recorded `screenId` values | Requires adding stable IDs to the Glance UI integration |
| Backtracks | Back actions or returns to earlier IDs | May indicate friction; not proof of confusion |
| Small targets | Activated UI elements below a fixed rendered-size threshold | Calibrate Unity/canvas pixel scale; don't pretend px equals mm |
| Regression | Task B still succeeds on redesigned UI | Preserves climate capability |
| Optional simulation state | Lane/vehicle/traffic snapshot held fixed or shown as contextual metadata | **Not** distraction or safety scoring |

**Optional static task-time estimate:** keep the original proposal's illustrative KLM-style coefficients only for before/after comparison, not real driving glance claims. Cut it first if short of time.

**Anti-gaming rules**

- `tasks/`, `scorer/`, bridge and test datasets remain outside Codex's write boundary.
- Same starting station/temp, UI scene, simulator conditions where applicable, 1280×720 test capture, Decisions model/prompt, visible-action policy and run budgets for each version.
- Fresh agent context on each run; target **three runs per task per version**, report success fraction and median actions, including failures.
- Show actual measured numbers, never the plan's illustrative 9→3 taps as though measured.
- Build runs and screenshots are immutable once stored; no post-hoc hiding or manual alteration of traces.

## Build plan (six-hour hackathon, 11:00–17:00)

The Unity template creates an additional setup risk. **Do not sacrifice the central test → score → Codex patch → retest loop to impress with 3D.**

| Time | Goal | Done when / exit decision |
| --- | --- | --- |
| 10:00–11:00 | After briefing: confirm rules, Unity 6.3+ access and template entitlement, check installed editor and Codex | Scope and roles confirmed; no project code created before permitted start |
| 11:00–11:45 | **Integration gate**: verify native input, visible-action inventory, state export and one live Decisions choice; check project access immediately | By **11:45** choose Unity only if an API-selected UI action has a verified effect; otherwise HTML fallback with the same Decisions loop |
| 11:45–12:30 | Build smallest broken Radio UI, state adapter and locked Task A/B success checks | Two manual task completions and state checks pass |
| 12:30–13:30 | Decisions runner + candidate adapter + input executor + trace capture | One complete Task A trace linking genuine Decisions responses to actual UI actions |
| 13:30–14:15 | Deterministic scorer and interaction map | Display success and trace-derived metrics |
| 14:15–15:00 | Codex edits permitted UI; rebuild/reload; fresh Task A and B runs | **15:00 checkpoint: entire loop works** |
| 15:00–16:00 | Three runs/version if feasible; before/after dashboard; improve Unity visual scene only if all tests pass | Demo starts in one click; no untested feature additions |
| 16:00–16:30 | Freeze, record offline backup, prepare local trace replay | Backup demo saved in two locations |
| 16:30–16:45 | Rehearse and submit | Submit by 16:45 |

**If the 11:45 Unity gate fails:** switch to HTML HMI immediately, still use the Unity template as a visual showcase if it independently runs, and state precisely which interface the agent tested.

**If the 15:00 checkpoint fails, cut in order:** optional time estimate → third/further trials (say how many completed) → live Codex patch (show recorded diff) → live agent operation (replay previously recorded genuine trace and score live). Preserve real Task B regression and honest scientific limits.

## Team

You own **measurement validity**, locked tasks, anti-gaming boundary and judge explanation. The Unity-specific work is a separate role.

| Team size | You | Teammate 2 | Teammate 3 | Teammate 4 |
| --- | --- | --- | --- | --- |
| 4 | Scorer + task validity + pitch | Agent runner, screenshots, input + bridge | Unity 6.3 template, radio UI, Codex-editable UI | Dashboard, results, demo video, submission |
| 2 | Scorer, bridge/Unity adapter, Codex, pitch | Agent runner + dashboard + fallback HTML | — | — |
| Solo | **Prefer HTML fallback** unless Unity template is already installed, open and testable; keep Task A and Task B, use minimal dashboard | — | — | — |

**Recruitment:** seek a TypeScript/browser-automation engineer **and** a Unity 6.3 developer with actual HMI/template experience. Shared contract by 11:15: task state schema (`stationId`, `tempC`, `screenId`), run reset, trace schema and editable UI folder. One repository, one owner per integration boundary.

**Presentation rule:** no one should describe the city simulation as evidence of actual driver visual workload.

## Demo script — submission video (maximum 90 seconds)

The story is **Decisions chooses → measured friction → Codex fixes → fresh proof**. Record only actual working behavior and measured results. Finalists get a separate two-minute demo plus up to one minute of questions.

| Time | On screen | Say |
| --- | --- | --- |
| 0:00–0:10 | Unity HMI and task | "Glance helps HMI teams inspect prototypes before human trials." |
| 0:10–0:30 | Screenshot, visible candidates, real Decisions choice and executed interaction | "OpenAI Decisions chooses each next action from the controls currently visible." |
| 0:30–0:42 | Retained trace and measured friction | "Our independent code checks success and counts the actual interactions." |
| 0:42–0:58 | Findings and actual Codex UI diff | "Codex changes the UI, with the task and scoring rules fixed." |
| 0:58–1:18 | Fresh Decisions radio run, climate regression and actual comparison | "Same model and evaluation policy; here is the measured result, and climate still works." |
| 1:18–1:30 | Result summary and limits | "Instrumented agent inspection—not human distraction measurement. Unity assets are credited; this is what we built today." |

Keep a recorded backup demo open. If the live run stalls for over 10 seconds, switch to an honestly labelled previous genuine run. **If using the HTML fallback, say so explicitly.**

## Judge questions

| Question | Answer |
| --- | --- |
| Does Unity's template already include driving? | It contains a live city/vehicle/traffic simulation and 3D vehicle, as described by Unity. It does **not** establish a first-person driving game or human-distraction measurement. |
| Why not just have AI edit HTML? | Unity makes the HMI and driving context closer to an automotive engineering workflow; the testing approach can also support HTML. We only claim Unity-native testing if it really happened. |
| Is WebGL supported? | **Not verified** in the official article. We try a short Web build smoke test, then choose native computer use if practical, otherwise the proven HTML fallback. |
| Does the template already contain the radio task? | The article does **not** promise it. We implement our own synthetic radio task UI and state adapter. |
| How does Decisions control the HMI? | A visible-action adapter binds rendered controls to complete input gestures. Decisions selects one value; the executor validates and performs that input. It does not generate arbitrary coordinates or call hidden state setters. |
| Is this pure vision-based discoverability testing? | No. The agent receives a visible-control inventory as well as pixels; we report that assistance and keep its rules fixed across versions. |
| How do you know Task A is complete? | A read-only C# state exporter reports station ID; a separate deterministic checker compares it with the locked task goal. Agent self-report is ignored. |
| Can't Codex game the score? | Codex can edit only a copied/isolated Glance UI variant; task rules, state bridge, evaluator and original template remain unchanged. A separate climate task detects regression. |
| An AI is not a human driver. Why trust it? | It is not a proxy for humans. It performs a repeatable pre-trial interface inspection to raise hypotheses for real human testing. |
| Does it predict distraction or compliance? | No. Only UI interaction friction; visual/manual/cognitive workload and regulatory compliance require other methods and human evidence. |
| Why use Unity Industry? | It provides existing embedded-style HMI presentation, ADAS, HVAC, vehicle and city-simulation context without rebuilding a car scene from scratch. |
| What is next after the hackathon? | Test against human-study findings; extend to real HMI deployments, multimodal controls and validated accessibility/usability outcomes. |

## Risks and fallbacks

| Risk | Mitigation |
| --- | --- |
| New Unity Industry template not accessible; entitlement or trial issues | Verify Unity Hub / Unity 6.3+ immediately; HTML HMI is independent and must still ship. Do not substitute the *older* 2021 Asset Store sample without stating the difference. |
| Template import/build too large for the event | 11:45 gate. Use existing simulator only if it already runs; switch testing to HTML. |
| Web build fails / template packages not supported in browser | Do not assume WebGL works; use native desktop automation only if already reliable, otherwise HTML. |
| UI technology/state APIs unknown | Inspect actual Unity scene hierarchy and scripts before adding small Glance-owned `UI/` and `StateBridge/` modules; no invented sample class names. |
| No built-in radio function | Add minimal synthetic radio preset state/UI without changing Unity’s underlying vehicle simulation. |
| Decisions access/model unavailable | Check a real call early. Preserve the failure; an explicitly labelled Responses alternative may demonstrate the product but does not establish Decisions integration. Do not silently substitute it. |
| Decisions selected action is stale or has no effect | Refresh the observation, reject stale bindings, and retain bounded failures. Fix native focus/input; use HTML if necessary while keeping Decisions as the selector. |
| Visible-action metadata leaks privileged hints | Enumerate only visible controls with neutral IDs and user-visible descriptions. Keep state predicates, internal destinations and task-aware filtering out of the model input. |
| State accidentally visible to acting agent | Split action prompt/screenshot from separate verification-only state channel; no hidden state in model prompts. |
| Model can't finish Task A | Show genuine failure and trace, do not cherry-pick; limit to 25 actions and fix accessible navigation. |
| Unity rebuild takes too long after Codex patch | Patch smallest UI component possible, rebuild only if feasible; record a genuine earlier patch/diff for demo or move to HTML. |
| Codex modifies forbidden files | Use writable staging copy of isolated UI, file allowlist and diff review. Working directory is not a security sandbox. |
| Codex breaks climate | Keep failed regression result, patch again within time if possible. |
| Venue Wi-Fi / API limits / demo failure | Cache traces/screenshots, keep screen-recorded fallback locally and separately backed up. |
| Existing work lacks credit | Templates and boilerplate are allowed by the Builders Guide; record their licences and distinguish reused assets from today’s core work. |
| New project scope grows | No new navigation, ADAS testing, manual driving, physics or production deployment until core loop passes. |

## Checklist — today, 1 November

**Before the build starts / after the rules briefing**

- [ ] Registration and venue confirmed; obey entry cutoff (09:30).
- [ ] Credit existing code/templates/assets and preserve their licences; follow any updated opening-brief rules.
- [ ] Confirm Unity 6.3+ editor installed, new Industry HMI template visible in Unity Hub, and entitlement/trial appropriate; check separate Unity/asset redistribution licensing where applicable.
- [ ] Test Codex CLI + Node + executor; verify Decisions access with one real choice call and retain its response.
- [ ] Freeze visible-action inventory policy, stale-observation checks and run budgets before baseline; prove selected input has a visible effect.
- [ ] Agree with team: template integration *optional*, complete Glance loop *mandatory*.
- [ ] Freeze Task A preset ID, Task B 21°C, fixed capture dimensions, action cap and trace schema.
- [ ] At 11:45 choose Unity-native HMI or HTML tested surface — no ambiguity.
- [ ] At 15:00 confirm full loop; by 16:00 freeze; by 16:45 submit.

**Unity template smoke test (at start of permitted coding window)**

- [ ] Create Unity Hub project using the **new Unity Industry Automotive HMI template for Unity 6.3+**.
- [ ] Open sample, run city/vehicle scene; inspect data/UI/3D separation and climate controls.
- [ ] Inspect actual UI implementation technology (uGUI/UI Toolkit/other), scripts, scene references and input system; do not guess.
- [ ] Try Web build and coordinate click; if failing, assess native desktop screenshot+mouse executor once.
- [ ] Confirm a valid state export for `radio.stationId`, `climate.tempC`, `screenId`.
- [ ] Commit to single track; prevent any agent from editing tasks, scorer, bridge, visible-action adapter, Decisions policy or base template.

## Source-specific notes and unknowns

- Unity's **6 October 2026** announcement confirms the new **6.3+** template and its 3D vehicle/city simulation, cluster, ADAS, HVAC, theme system and data/UI/3D layer separation. It does **not** document sample C# classes, HMI scripting framework, build platform compatibility or an API for Glance; these are integration tasks, not promises.
- A separate **2021 Automotive HMI Template** exists in Unity's Asset Store, with different packaging and historical content. Do not confuse it with the **new Unity Hub project template** linked by the user.
- The article cites embedded Linux/Raspberry Pi 5 example performance. That does not validate a WebGL deployment or the laptop's performance. Native desktop may be quicker if computer automation already works.
- Unity Web builds *can* communicate with host JavaScript using `.jslib` and `unityInstance.SendMessage`, but that is a general Unity capability, **not proof that this particular HMI sample exports successfully to Web**.
- The document's measured-tap improvements and success rates are aspirations for the live demo until actual traces confirm them.

## Sources

- [OpenAI Decisions guide](https://developers.openai.com/api/docs/guides/decisions): supported model, typed choices and input evidence
- [Create decision reference](https://developers.openai.com/api/reference/resources/decisions/methods/create): request/answer contract
- [OpenAI computer use](https://developers.openai.com/api/docs/guides/tools-computer-use): choosing Decisions when complete actions are already enumerated
- [London Builders Guide](https://warp-peach-ef6.notion.site/): current submission and demo requirements

- [DevDay Community Hackathon: London (Luma event page)](https://luma.com/dde-ldn-hack?locale=en-GB): schedule, team size, prizes
- [Meet the winners of OpenAI Build Week](https://learn.chatgpt.com/blog/build-week-winners): winners and judging criteria
- [DevDay Community Hackathon Tokyo (Luma)](https://luma.com/OpenAIDevDay2026CommunityHackathonTokyo): Codex-only tool rule at another DevDay hackathon
- [OpenAI DevDay 2026 Recap for Developers (InfoQ)](https://www.infoq.com/news/2026/10/openai-devday-2026/): Agents API computer use, GPT-6.1 Sol
- [Euro NCAP results reward physical buttons in 2026 tests (Automotive World)](https://www.automotiveworld.com/news/euro-ncap-results-reward-physical-buttons-in-2026-tests/)
- [Euro NCAP 2026 safety rules (Carwow)](https://www.carwow.co.uk/news/9964/euro-ncap-2026-safety-rules)
- [NHTSA visual-manual distraction guidelines test procedure](https://rosap.ntl.bts.gov/view/dot/41935/dot_41935_DS1.pdf): why glance claims need human testing

- [Unity — New for Unity Industry: Automotive HMI sample for embedded-style hardware (6 Oct 2026)](https://unity.com/blog/unity-industry-hmi-template-for-embedded-systems): official selected template, built-in features, requirements and download steps
- [Unity — HMI and embedded systems](https://unity.com/solutions/human-machine-interface-hmi): broader Unity Industry context and entitlement links
- [Unity — Call JavaScript from Unity C# in Web builds](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/webgl/develop/interactingwithbrowserscripting/web-interacting-browser-js-to-unity): optional JS bridge integration approach
- [Unity Asset Store — older Automotive HMI Industry Sample Project](https://assetstore.unity.com/packages/templates/packs/automotive-hmi-industry-sample-project-201095): **different older sample; reference only, not the selected 2026 template**

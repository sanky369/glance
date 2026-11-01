# Glance — agreed build brief

1 November 2026 · Product scope. Unity template setup is now available in `unity-hmi/`; see `docs/unity-hmi/SETUP.md` for verified results and remaining integration work. The core Glance pipeline has not been implemented yet.

## Product

Glance helps automotive HMI designers, UX engineers and human-factors teams inspect touchscreen interaction friction before human testing. An agent using OpenAI Decisions API selects actions on a synthetic car interface; deterministic code measures the interaction trace; Codex changes the UI using that evidence; a fresh agent repeats the task and checks an unrelated function still works.

The complete journey is **test → evidence → Codex UI patch → retest → comparison**, with a climate-control regression check. Unity supplies the preferred HMI and automotive context. The core value is the verified improvement loop.

## Filled-in starter prompt

I’m building Glance at the DevDay Exchange Community Hackathon in London on Sunday 1 November 2026. Glance is an AI-assisted automotive touchscreen inspection and improvement tool for HMI designers, UX engineers and human-factors teams. It runs a Decisions-powered agent through a synthetic radio task using screenshots and an inventory of visible controls, records interaction friction, uses Codex to revise the UI, and runs a fresh test plus a climate-control regression check.

I am building solo. My preferred foundation is Unity Industry’s Automotive HMI template, with C# for the UI and a verification state adapter. Use a small TypeScript/Node test coordinator, Playwright for a verified browser test surface, an OpenAI Decisions API choice loop over pre-enumerated visible actions, a deterministic scorer, and a React/Vite results dashboard. Use Codex CLI for evidence-driven UI changes in an isolated editable copy. If Unity cannot run and support automation, state verification and a UI edit/reload within the first 45 minutes, switch the tested interface to React/HTML. Show Unity separately only if it already works, and label which surface was actually tested.

Help me ship one complete journey: select a fixed radio task, run a real agent, inspect the trace and metrics, apply a Codex-generated UI patch, reset and rerun with a fresh agent, verify cabin temperature can still be changed to 21°C, and compare genuine before/after results. Check current official documentation before implementing APIs. Keep scope small, preserve failures, and produce repeatable end-to-end evidence. Do not claim to measure human distraction, eye movements, safety or regulatory compliance.

The build window is 11:00–17:00 London time. Build the core during the event and credit all existing ideas, code, templates and assets. Freeze by 16:00 and target submission by 16:45. Prepare a public GitHub repository with setup instructions and what was built today, a working demo video no longer than 90 seconds, and one concrete example of Codex’s contribution. Keep secrets and personal data out of published material and verify public links without authentication. Prepare a separate two-minute finalist demo. Flag dependencies that threaten the deadline.

## Smallest useful scope

- One synthetic HMI with two real capabilities: radio selection and cabin temperature.
- Task A: select a fixed, visibly labelled station/preset, frozen before the baseline.
- Task B: set temperature to 21°C from a different fixed starting value; run independently from a reset state.
- The acting agent receives the HMI screenshot, its task, visible control candidates and bounded within-run history. Decisions selects a candidate; the runner executes its pre-bound click/drag. Hidden verification state and results controls stay outside that observation and action surface.
- This is an instrumented, assisted walkthrough: the visible-control inventory helps discovery and must be disclosed in results. Keep discovery rules and neutral descriptions fixed across baseline and redesign. Never expose hidden destinations, state predicates or task-aware hints.
- See the original plan’s **Decisions API integration** section for the full observation/choice/execution contract and error policy.
- Record screenshots, actual inputs, stable screen IDs, timestamps, target bounds when instrumented, and verified final state.
- Report task success, input count, screens visited, backtracks and activated small targets using predefined definitions. Timing is agent execution time, not human task or glance time.
- Codex edits only the isolated UI copy. Keep the task predicates, runner, state bridge, scorer and baseline fixed outside its write boundary; review the patch before applying it.
- Reset before every run; use the same viewport, Decisions model/prompt, candidate policy and initial state. Freeze limits at 25 executed HMI actions, 40 Decisions calls and five minutes per run, plus a configured API spend limit. Use fresh context. Aim for three runs per task/version; report actual counts and retain failures.
- Persist trace JSON linking screenshots and candidate sets to Decisions responses, selected values, actual input and state checks; retain API latency/usage, scores, version identifiers and the actual Codex diff. A replay must be labelled as a recorded run.

Do not add arbitrary website testing, user accounts, CAN integration, realistic vehicle physics, manual driving, ADAS evaluation, eye tracking or a general UI editor.

## Solo implementation sequence

| London time | Deliverable |
| --- | --- |
| 11:00–11:45 | Finish the native input gate, visible-action bindings, verifier state and one real Decisions choice with a verified UI effect. Check API access now; choose Unity or HTML without replacing Decisions. |
| 11:45–12:30 | Working radio and climate tasks; fixed reset and success contracts. |
| 12:30–13:30 | One authentic Decisions-driven task trace with verified outcome; accommodate lunch. |
| 13:30–14:15 | Deterministic metrics and a minimal evidence/results view. |
| 14:15–15:00 | Codex UI patch, fresh radio retest, climate regression; complete loop must work. |
| 15:00–16:00 | Repeat runs, fix defects, capture repeatable E2E evidence and polish the comparison. |
| 16:00–16:45 | Freeze; README, credits, secret review, public-link checks, ≤90-second recording, submission and receipt check. |

If starting late, shorten optional polish and repeat runs before consuming the submission buffer. Prefer genuine recorded evidence over a fragile live-only demo.

## Verified event requirements and corrections

The public Builders Guide allows existing ideas, libraries, APIs, templates and boilerplate, but requires the core project to be made during the hackathon; previously completed projects are not eligible. Identify reused material and today's contribution. There is no Codex-only restriction stated in the guide.

Judging: idea/usefulness, meaningful Codex use, execution, and demo/clarity; each scores 1–5 with equal weight, total /20. Six finalists. Finalist format is a **two-minute demo plus up to one minute of questions**, replacing the original plan's three-minute demo assumption.

Submit once per team by 17:00 using the route provided at the opening brief. Include project description/users/problem, team details/contact, public GitHub repo with README/setup/today's work, ≤90-second working video, a concrete Codex example, and an app link with instructions if deployed. Check submission receipt and unauthenticated link access. Hosted deployment is conditional, not a stated requirement.

Suggested 90-second video: 0–10s problem/users; 10–30s actual Decisions choice, candidates and executed UI action; 30–42s measured evidence; 42–58s Codex input and actual diff; 58–78s fresh Decisions retest and climate check; 78–90s measured comparison, limits and contribution.

## Current local state and unresolved dependencies

- Unity template 1.0.4 is running in the isolated `unity-hmi/` folder with Unity 6000.6.3f1 and CLI beta.15; Body Control, HVAC and ADAS render with advancing frames and zero errors at the recorded check. See `docs/unity-hmi/SETUP.md`.
- User is building solo. Initial native pointer trials failed. A dedicated virtual-mouse driver now verifies Climate navigation and driver temperature at 21°C through actual UI input; see `docs/unity-hmi/POINTER-AUTOMATION.md`. This is a deterministic E2E check, not a live Decisions trial.
- Template licences and third-party notices are preserved. Browser compatibility, player builds, a Glance UI edit/reload, resettable verifier bridge and real agent trials remain unverified.
- Decisions API is now a core planned dependency, not an implemented integration. Current official docs name `gpt-6-luna` and `POST /v1/decisions`; use a named `choice` question and map its selected value to a current executable candidate. The API does not invent action arguments or operate Unity itself.
- OpenAI project/model access and credentials remain unverified. Validate one real request early. Keep credentials server-side and out of traces. If access fails, retain the failure and clearly label any alternative; do not claim a Responses call demonstrates Decisions integration.
- Prefer the locally controlled screenshot/candidate/executor loop. Decisions can consume the screenshot as inline image evidence; it does not need access to a hosted local app.
- Submission destination is supplied during the opening brief and has not been obtained here.
- Numbers in the original diagram (including 9→3 taps) are illustrative, not measured results.

## Sources

- [Referenced planning chat](chatgpt-conversation://6ae6245c-1b74-83ed-8d84-1fa00b9766bd): accessible five-turn preview and local plan/diagram.
- [Builders Guide](https://warp-peach-ef6.notion.site/): read in browser on 1 November 2026; rules, judging, submission and finalist format.
- [London event](https://luma.com/dde-ldn-hack): build window, deadline and maximum team size.
- [Unity template announcement](https://unity.com/blog/unity-industry-hmi-template-for-embedded-systems): version, acquisition route and supplied features; does not establish Web compatibility.
- [OpenAI Decisions guide](https://developers.openai.com/api/docs/guides/decisions): typed choice selection and currently supported model.
- [Create decision reference](https://developers.openai.com/api/reference/resources/decisions/methods/create): request/answer schema.
- [OpenAI computer use](https://developers.openai.com/api/docs/guides/tools-computer-use): runtime-controlled screenshots/actions and outcome verification.
- [Codex non-interactive mode](https://developers.openai.com/codex/noninteractive): automation integration reference.
- [Codex quickstart](https://developers.openai.com/codex/quickstart): setup reference.

Use current official SDK/API documentation during implementation; no untested API snippets are prescribed by this brief.

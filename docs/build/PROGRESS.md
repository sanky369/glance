# Glance build record

1 November, 13:02 London. Target: complete loop by 15:00, freeze 16:00, submit 16:45.

- Unity HMI and pointer driver verified; repository pushed.
- Project key securely created in ignored .env.local; live Decisions choice succeeded.
- Building synthetic radio UI, locked state/reset, visible-action adapter, runner and scoring next.
- Core runtime will use TypeScript with Node's native fetch/HTTP to avoid dependency setup. Initial dashboard will be a small local page; add React only if time remains.
- E2E acceptance: real Decisions radio trace; independent state success; baseline findings; isolated Codex UI diff; fresh retest; climate regression. Keep failures and raw traces local.

13:22 update: genuine radio baseline passed with 5 UI actions, 5 screens; climate baseline passed with 2 actions (22→21°C, passenger 22 unchanged). Verifier now terminates on success (policy verified-stop-v1); earlier exploratory climate timeout retained. Local dashboard http://127.0.0.1:3210 runs and renders in existing Chrome. Isolated Codex CLI proposal had a host/MCP startup timeout, then code-host-disabled failure; retrying with Apps disabled and execution host enabled. No UI redesign applied yet. Core code is not committed yet. Need fresh after tests, public evidence export, README and demo capture.

13:48: Core loop complete. Baseline radio 3/3 at 5 inputs; revised 4/4 at 1 input; climate regression passes 21/22°C. Actual Codex source proposal/diff reviewed and applied; structured-source flow works after tool-host failures. Chrome dashboard E2E passed and screenshot saved. 59-second genuine trace replay generated. Final-screen-inclusive reporting audit preserved original results and corrected counts to 6→2. Preparing source/evidence commit and push. Submission itself remains user-owned.

14:15 upgrade: GPT Image 2.5 Flare generated graphite/citron artwork via bundled imagegen CLI; integrated into dashboard and Unity premium skin with Inter typography. Voice worker implemented real OpenAI Realtime WebRTC (gpt-realtime-2.1/marin), secure server SDP exchange; API session config smoke 200. User approved microphone activation and short OpenAI exchange. Two missing original climate baselines passed; premium skin then passed 3 radio and 3 climate trials. One-click cycle implementation staged, not yet executed. Need live microphone/browser acceptance, click cycle E2E, real tab recording <=85s, export/check/push. Submission excluded per user.

15:12 full-HMI implementation complete before end-to-end testing: 24 capability tasks; generic verifier fixtures and full visible-action inventory; body/camera/native workspaces exposed by radio/full-HMI toggle; actual template hood/rear/charging local-state bug fixed; pointer deltas added for camera rotation. Natural instruction->finite task mapping, capability picker, full-suite/fresh-baseline endpoints, result filters, evidence-derived functionality/usability/infrastructure issues, cautious linked-retest resolutions and separate implementation fixes integrated. Starting complete final validation now; no claim of full test coverage yet.

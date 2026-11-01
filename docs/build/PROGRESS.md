# Glance build record

1 November, 13:02 London. Target: complete loop by 15:00, freeze 16:00, submit 16:45.

- Unity HMI and pointer driver verified; repository pushed.
- Project key securely created in ignored .env.local; live Decisions choice succeeded.
- Building synthetic radio UI, locked state/reset, visible-action adapter, runner and scoring next.
- Core runtime will use TypeScript with Node's native fetch/HTTP to avoid dependency setup. Initial dashboard will be a small local page; add React only if time remains.
- E2E acceptance: real Decisions radio trace; independent state success; baseline findings; isolated Codex UI diff; fresh retest; climate regression. Keep failures and raw traces local.

13:22 update: genuine radio baseline passed with 5 UI actions, 5 screens; climate baseline passed with 2 actions (22→21°C, passenger 22 unchanged). Verifier now terminates on success (policy verified-stop-v1); earlier exploratory climate timeout retained. Local dashboard http://127.0.0.1:3210 runs and renders in existing Chrome. Isolated Codex CLI proposal had a host/MCP startup timeout, then code-host-disabled failure; retrying with Apps disabled and execution host enabled. No UI redesign applied yet. Core code is not committed yet. Need fresh after tests, public evidence export, README and demo capture.

13:48: Core loop complete. Baseline radio 3/3 at 5 inputs; revised 4/4 at 1 input; climate regression passes 21/22°C. Actual Codex source proposal/diff reviewed and applied; structured-source flow works after tool-host failures. Chrome dashboard E2E passed and screenshot saved. 59-second genuine trace replay generated. Final-screen-inclusive reporting audit preserved original results and corrected counts to 6→2. Preparing source/evidence commit and push. Submission itself remains user-owned.

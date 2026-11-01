# Core validation

Baseline comparison policy: verified-stop-v1. The verifier ends each task as soon as its fixed state predicate and invariants are met. Earlier exploratory runs remain visible and are not mixed into this primary comparison.

- Radio baseline: three genuine API-driven runs; all succeeded in five UI inputs; screen counts are audited below.
- Climate baseline: a genuine API-driven run succeeded in two inputs, driver 22→21°C and passenger unchanged at 22°C.
- Earlier climate exploratory run reached 21°C but continued; then overshot and timed out. This exposed the termination bug fixed before comparison trials.
- Dashboard renders actual screenshots and trace choices in the existing Chrome session at http://127.0.0.1:3210.
- Key remains ignored in .env.local. Exported evidence contains API responses/usage, not authorization headers.

## Final measured results

- Three fixed-policy radio baseline runs passed: 5 inputs each.
- Three fresh revised-radio runs passed: 1 input each; a fourth run launched through the Chrome dashboard also passed with 1 input.
- Revised climate regression passed: two actual UI inputs, driver 21°C, passenger unchanged at 22°C.
- Source hashes are frozen in `tasks/versions.json`; baseline/revised labels are rejected if the current UI source does not match.
- Final-scene counting audit: reporting originally counted only pre-input screens. The corrected scorer consistently includes before/after/final screen IDs, giving radio **6 → 2 screens**. Original result objects remain in `recorded-result.json`; derived corrected results are exported alongside them. Success and action counts are unchanged. This audit does not change the actor, task predicates or UI trials.
- `scorerVersion: 2-final-screen-inclusive` and `policyVersion: verified-stop-v1` identify the reporting/evaluation contracts. The original exploratory runs are retained separately from the primary comparison.
- Codex proposal changed only Show/Menu. State, tasks, input executor and observation adapter were not modified by that proposal. Reviewed source and diff are under `docs/build/redesign/`.
- Existing logged-in Chrome validated run launch, measured comparison, selected-action replay and final-state playback. The browser authority timeout was retried successfully.
- A 59-second captioned replay assembled from genuine screenshots is `glance-demo.mp4`. It is not a live screen recording; source run IDs are in `demo-sources.json`.

Remaining submission work: team/contact details, the organizer's submission route and receipt, and optional hosted deployment. The dashboard is local and requires the native Unity Editor. Full browser deployment, general HMI ingestion, speech and image generation were not implemented.

Offline acceptance: `npm run replay` serves 11 archived runs and their PNGs without a key, rejects live execution, and rejects private-file paths. A stale Codex source proposal was rejected without changing the UI.

Final Unity console snapshot: no compilation failure and zero errors. One later warning reports an OS audio-device change/reset; it is retained in `final-console.json` and is not an HMI task failure.

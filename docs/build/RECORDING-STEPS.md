# Record the final live demo

Keep the submission video at most 90 seconds, showing the working product without slides. Record the longer run and speed up waits. Use the separate five-slide deck for presenting afterward.

1. Open http://127.0.0.1:3210, Unity Main in Play mode, and this repository in Codex. Hide terminals with credentials and unrelated personal tabs.
2. Start your screen recorder. In Glance use the fresh-HMI baseline control and wait for compilation/Play restart to finish before continuing.
3. Give a supported instruction such as selecting radio preset 2, or use the task picker. Show actual Unity pointer navigation and the independently verified result. Avoid touching Unity while an evaluation runs.
4. Show result filters and a usability finding with its trace/screenshots. Explain that task success and interaction friction are separate.
5. Run the complete Codex cycle once. It measures a baseline, proposes a bounded radio UI change, validates/applies it, recompiles and runs fresh radio/climate evaluations. Speed up API and compile waits. Show its actual saved source diff in Codex and the cycle stages in Glance. If the UI is already optimized, it may correctly produce no change; use the documented fresh baseline first.
6. Show the revised direct radio presets and the genuine 5→1 input example. Show the climate predicate (driver 21°C, passenger 22°C), keeping any extra inputs visible. Optionally show a supported native task such as the driver door or driving workspace.
7. End on evidence: real inputs, independent state verification, retained failures and linked source changes. Voice can summarize results through push-to-talk; use headphones in the noisy venue.

Suggested final edit: 0–10s problem/instruction; 10–30s live Unity task; 30–45s findings; 45–65s Codex diff and compile/retest sped up; 65–85s revised UI and comparison. Do not claim that all full-HMI defects are automatically fixed: the automated patch scope is radio presentation.

Upload your edited recording publicly, check its link in a private window without permission requests, and place it in the submission form. Repository: https://github.com/sanky369/glance. Project description: Glance turns natural-language tasks into real UI walkthroughs of a Unity automotive HMI, verifies outcomes independently, and turns measured friction into guarded Codex UI revisions with fresh retests.

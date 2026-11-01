# Glance — five-slide presentation plan

Status: preparation only; waiting for root commit confirmation before slide authoring.

Design: 16:9, Inter, warm ivory #edece7, graphite #101910, citron #c8e78d. Flat editorial composition, large typography, full-width images rather than cards. All foreground text editable. Exactly five slides; slide 1 is the problem opening, not an additional cover.

1. **A task can pass. The interface can still slow you down.**
   Dark automotive cockpit illustration on the right, editable headline on the left. One supporting line: “A successful tap sequence tells only part of the story.” Lower notes: navigation depth, small controls and unintended state changes need evidence. Illustration credit in speaker notes; no claim that it depicts the real product.
2. **Meet Glance**
   “Ask the HMI to do a task. See what actually happened.” Use the new real dashboard screenshot as the dominant evidence image. Editable 5 → 1 radio input example. Small qualification: selected recorded radio task, not a full-HMI coverage claim. Optional context: 20 available task contracts across the current instantiated HMI.
3. **The stack behind the evaluation**
   Flat typography with Unity/C# and Node/TypeScript as the runtime; Decisions gpt-6-luna for observed UI choices; Realtime gpt-realtime-2.1 + marin for conversation; GPT Image 2.5 Flare for generated product artwork. Codex gpt-6.1-sol supports implementation and a guarded source-proposal → apply → recompile → fresh-retest loop, with rollback. Keep runtime and model roles separate.
4. **From intent to verified result**
   Root-generated clean architecture image, based on existing project architecture. Caption: “A model’s claim is never the pass condition.” Include user instruction → catalog/task contract → Decisions/real UI input → Unity state verification → evidence/report; Codex guarded loop remains separate from voice and evaluator. Foreground title/caption editable.
5. **See Glance in action**
   Editable video insertion area. Minimal labels: “Demo video” and “Insert your recording here.” User can replace placeholder in presentation editor. Notes recommend 60–90 seconds: natural instruction, real HMI action, verifier result, reports, source change/retest evidence.

Evidence constraints: gear selection describes visible P/R/N/D UI state, not physical vehicle reverse. Four enum-only body-part entries are not instantiated and are excluded. Do not claim 24 working controls, 20/20 verified coverage, or 100% tests. Baseline radio selected cohort 3/3 and initial revised 4/4 are limited examples. Root will supply final suite verdict and commit before authoring.

Pending assets: new premium dashboard screenshot; updated generated architecture PNG; problem cockpit illustration in generation. Existing dashboard/assets/glance-studio.png may be reused only as a background.

Delivery: default saved editable Spaces presentation; local PPTX only if the destination is unavailable or a file is explicitly requested. Slide 5 remains an insertion placeholder, not an embedded or fabricated demo.

# Unity HMI pointer automation

The Editor-only driver is `unity-hmi/Assets/Glance/Editor/GlancePointerCommands.cs`. It sends virtual mouse events through Unity Input System; uGUI performs ordinary hit testing, pointer clicks and drags. It never invokes button callbacks, sample navigation methods or temperature setters.

## Diagnosis and fix

The original CLI command used the physical `Mouse.current` and called `InputSystem.Update()` synchronously from Editor-side execution. Its success response meant the event was injected, not that the Game view received it. The project's default policy routes pointers away from an unfocused Game view. Changing that policy alone still did not deliver the tested physical-mouse click. A dedicated virtual mouse, queued for the normal Dynamic input update with temporary background routing, successfully navigated the same UI.

The driver provides that proven path. It clones settings for the automation session, enables Game view/background input, and uses a dedicated virtual mouse. A hover frame precedes down; movement and release are delivered across player frames. A cloned UI action asset accepts only the automation mouse during the session, preventing hardware-pointer interference; the original asset is restored on cleanup. It restores settings and removes the device on `end`, Play mode exit or assembly reload. Unity destroys temporary `HideAndDontSave` input settings when replacing them; the driver preserves a copy when needed for restoration.

Coordinates use a screenshot's top-left origin. The driver maps them to the actual Game view target using `PlayModeView.GetMainPlayModeViewTargetSize`, rather than Editor-side `Screen.width/height`, which reported the wrong view dimensions. This internal Editor API is checked at runtime; unavailable calibration causes a failure instead of a guessed click. Keep the Game view layout stable during a gesture and avoid simultaneous manual input.

## Use from the Glance runner

Run from the repository root with Main playing. The `--caller`, `--skill`, `--project-path` and `--format` flags must precede the `--` parameter fence.

```sh
unity command glance.pointer begin --caller plugin --skill unity-cli --project-path "$PWD/unity-hmi" --format json
```

For a click, use equal start and end coordinates. For a drag, supply its endpoint and duration. These numbers are illustrative; obtain actual coordinates from the current HMI observation.

```sh
unity command glance.pointer gesture --caller plugin --skill unity-cli --project-path "$PWD/unity-hmi" --format json -- --x 100 --y 100 --end_x 100 --end_y 100 --width 1280 --height 720 --duration 0.12
unity command glance.pointer status --caller plugin --skill unity-cli --project-path "$PWD/unity-hmi" --format json
unity command glance.pointer end --caller plugin --skill unity-cli --project-path "$PWD/unity-hmi" --format json
```

Wait for `busy: false` before submitting another gesture or ending a session. Before a subsequent UI action, also wait for the target screen to settle and its controls to become interactable. The template sets its next scene active midway through a fade; active-scene equality alone does not establish readiness. The queue acknowledgement and completion status establish input delivery only; capture the resulting UI and independently verify the task. Coordinates must be finite and inside the supplied image; duration must be 0.05–2 seconds. Gestures are serialized. The template's temperature selector needs a held drag; the verified E2E uses 0.5 seconds, rather than a short movement that snaps back without advancing a choice.

The implementation is for the local Unity Editor. A native Player build would need a runtime version of this adapter; WebGL and Decisions integration are not established by this check.

## Repeatable end-to-end verification

```sh
python3 scripts/verify_unity_pointer.py
```

The test requires the isolated Editor playing Main and no other automation session. Its first click must change the current screen. It then clicks Control and Climate, waits for navigation controls to re-enable after each fade, and drags the left/driver temperature handle to 21°C. If already at 21°C, it first moves to 22°C through the UI so the repeat run still demonstrates a change. It checks that the passenger temperature is unchanged and the final console has no compilation or runtime errors.

The verifier reads widget geometry to construct inputs and reads `CarData` to check results. This is a deterministic integration test, not a Decisions-powered or purely visual agent trial. It does not write task state. Each new invocation saves `result.json`, `trace.json` and three screenshots in a timestamped directory under `docs/unity-hmi/evidence/pointer/`. Failures retain their trace and error. Earlier debugging attempts and the first passing run remain in that parent directory as historical evidence.

First passing run: Body Control → Climate; driver 16→17→18→19→20→21°C, passenger unchanged at 15°C; zero console errors/warnings. See [result](evidence/pointer/result.json) and [final screenshot](evidence/pointer/driver-21.png).

Final-driver verification: [passing result](evidence/pointer/20261101T123856490647Z/result.json), [input trace](evidence/pointer/20261101T123856490647Z/trace.public.json), [21°C screenshot](evidence/pointer/20261101T123856490647Z/driver-21.png). This run also proves 21→22→21°C using UI drags and verifies original focus settings and zero remaining automation mice after cleanup.

A second fresh-session check passed from Climate: [result](evidence/pointer/20261101T123953157787Z/result.json), [trace](evidence/pointer/20261101T123953157787Z/trace.public.json), [screenshot](evidence/pointer/20261101T123953157787Z/driver-21.png). Both final-driver runs pass with zero console errors or warnings and restored input settings.

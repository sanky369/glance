# Unity HMI setup and smoke check

Verified 1 November 2026. This establishes a running native Editor sample, not the complete Glance pipeline or an agent-driven usability trial.

## Source and setup

- Template: `com.unity.template.hmi@1.0.4`, found in the CLI catalog for `6000.3.0f1`. It was absent from the `6000.6.3f1` catalog.
- [Official archive](https://download.packages.unity.com/com.unity.template.hmi/-/com.unity.template.hmi-1.0.4.tgz), 122,437,005 bytes.
- SHA-1 `958426edcfbb8fa3f1ed5dae799e312f8382981f`, matched against the catalog's decoded integrity field before project creation.
- Package README requires Unity 6000.3 or later. Created and verified using the installed 6000.6.3f1 arm64 Editor.
- Catalog targets: macOS, Windows and Embedded Linux. WebGL compatibility has not been established; no Web build was attempted.
- CLI updated from 1.0.0-beta.8 to 1.0.0-beta.15 using Unity's official updater.
- `unity projects create` created the project from the local verified archive with `--no-cloud`. It installed Pipeline 0.9.1-exp.1.
- Full source metadata is in [template-source.json](template-source.json).

Creation command, for a fresh destination only (do not rerun over the existing project):

```sh
unity projects create unity-hmi --path "$PWD" \
  --editor-version 6000.6.3f1 \
  --template /tmp/glance-hmi-template-1.0.4.tgz \
  --no-cloud --non-interactive --format json
```

The archive is a temporary local download. Retrieve it from the official URL above if recreating later, and verify the recorded digest before use.

## Repeat the smoke check

Run these from the Glance repository root. The first command stays attached; run subsequent commands in another terminal. Stop if the Editor already contains someone else's active Play session.

```sh
unity open "$PWD/unity-hmi" --wait
```

```sh
unity status --until-ready --timeout 120 --project-path "$PWD/unity-hmi" --format json
unity command editor status --caller plugin --skill unity-cli --project-path "$PWD/unity-hmi" --format json
```

Starting from a stopped Editor:

```sh
unity command scene open --caller plugin --skill unity-cli --project-path "$PWD/unity-hmi" -- --path Assets/Scenes/Main.unity
unity command editor set_auto_tick --caller plugin --skill unity-cli --project-path "$PWD/unity-hmi"
unity command editor.playmode set_run_in_background --caller plugin --skill unity-cli --project-path "$PWD/unity-hmi"
unity command editor.playmode start --caller plugin --skill unity-cli --project-path "$PWD/unity-hmi"
unity status --until-ready --timeout 120 --project-path "$PWD/unity-hmi" --format json
```

Verify actual frame progression, capture the composited UI, and inspect the console:

```sh
unity command job wait --caller plugin --skill unity-cli --project-path "$PWD/unity-hmi" --format json -- --condition '{"member":"UnityEngine.Time.frameCount","op":"changed"}' --timeout 10
unity command capture game_view --caller plugin --skill unity-cli --project-path "$PWD/unity-hmi" --format json -- --source screen --save_path Screenshots/repeat-smoke.png
unity command console get --caller plugin --skill unity-cli --project-path "$PWD/unity-hmi" --format json
```

Require `met: true`, `timedOut: false`, compilation not failed, and no console errors. The capture command saves under `Assets/Screenshots/` despite its project-relative path wording; move verification images outside Assets when retaining them as documentation.

For a screen-loading smoke check, the verified sample API is:

```sh
unity command script eval --caller plugin --skill unity-cli --project-path "$PWD/unity-hmi" -- --code 'UnityEngine.Object.FindFirstObjectByType<Modules.Menus.Scripts.SceneSwitcherUI>().LoadScreenInMainViewport(Modules.Menus.Scripts.Scenes.HVAC);'
```

Use `Scenes.ADAS` for the traffic view. Allow its asynchronous transition to complete before inspecting or capturing the next screen. This is programmatic navigation and must not be reported as agent interaction evidence.

## Observed evidence

[Smoke result JSON](evidence/smoke-result.json) records two frame-progression checks and the final console result. Rendered screens:

- [Body Control](evidence/body-control.png)
- [HVAC](evidence/hvac.png)
- [ADAS traffic simulation](evidence/adas.png)

The Editor was left playing on ADAS with background ticking enabled. No template UI or scoring logic was redesigned in this setup step.

## Historical input limitation — subsequently fixed

The pointer limitation below describes the initial setup run. The later [pointer automation fix and E2E check](POINTER-AUTOMATION.md) verifies actual UI navigation and driver-temperature adjustment to 21°C. The radio task, Decisions integration and resettable verifier bridge remain separate work.

`runtime simulate_pointer` returned success for test inputs but did not switch scenes. Native click attempts also did not establish reliable navigation. At inspection, the Editor reported `Application.isFocused == false`. This is an observation, not a proven root cause.

`Screen.width/height` inside Editor-side eval reported the Editor view's size, while `PlayModeView.GetMainPlayModeViewTargetSize` reported the Game view's render size. Screenshot output size is also configurable and may differ from either. The eventual runner must calibrate coordinates against the actual tested render surface and verify each action's effect.

Pointer input, a stable capture size, an isolated editable Glance UI, and a resettable verification adapter remain to be completed before declaring the original Unity integration gate passed. Native rendering and state inspection are verified. Player builds, Web builds, agent operation and the radio task are not yet verified.

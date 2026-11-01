# Glance

AI-assisted inspection and improvement of automotive touchscreen prototypes, built for the DevDay Exchange Community Hackathon in London on 1 November 2026.

The planned journey is **test an HMI → measure interaction friction → let Codex revise the UI → run a fresh test and climate regression → compare results**. OpenAI Decisions API will select actions from the currently visible controls; independent code will execute inputs and verify outcomes. This is an assisted, instrumented walkthrough, not a measurement of human distraction or regulatory compliance.

## Current status

- Unity HMI template runs locally in the Editor: Body Control, Climate, Cluster and ADAS scenes.
- An Editor-only pointer driver performs real Input System mouse clicks and drags.
- Repeatable E2E checks navigate to Climate and set driver temperature to 21°C while preserving passenger temperature. Two final-driver runs passed with zero console errors/warnings and restored input settings.
- Decisions API integration, the synthetic radio task, scorer, results dashboard and automated Codex redesign loop remain planned.

## Repository layout

```text
unity-hmi/       Isolated Unity project: Assets, Packages and ProjectSettings
scripts/         Glance tooling and end-to-end verification
docs/unity-hmi/  Setup notes, pointer-driver usage and verification evidence
BUILD-BRIEF.md   Product scope and solo build plan
```

The Unity project is part of this repository, not a separate nested Git repository. Its generated Library, Temp, Logs, UserSettings and build folders are ignored.

Original verification traces remain local. Published `trace.public.json` copies replace absolute checkout paths with `<GLANCE_ROOT>`; screenshots and measured results are retained.

## Run the Unity sample

Verified with Unity **6000.6.3f1 arm64** on macOS, Unity CLI **1.0.0-beta.15**, and Pipeline package **0.9.1-exp.1**. The template specifies Unity 6000.3 or later; other Editor versions and platforms have not been verified here.

1. Clone this repository and install the required Unity Editor through Unity Hub.
2. Open `unity-hmi/` in the Editor. Allow Unity to resolve packages and rebuild its local Library.
3. Open `Assets/Scenes/Main.unity` and press Play. Use the bottom bar to switch screens.

With Unity CLI installed, run from the repository root:

```sh
unity open "$PWD/unity-hmi" --wait
```

No API key is needed for the current Unity sample or pointer check. No secrets belong in version control. The future API integration is not configured by these steps.

## Verify pointer input

With the isolated project playing Main, run in another terminal:

```sh
python3 scripts/verify_unity_pointer.py
```

Requires Python 3 and Unity CLI on PATH; no third-party Python dependencies. The check uses only mouse events to navigate and change temperature; it reads widget geometry and application state for verification. Each invocation saves screenshots, trace and results under a timestamped `docs/unity-hmi/evidence/pointer/` directory. Avoid simultaneous manual input during a test.

See [pointer automation](docs/unity-hmi/POINTER-AUTOMATION.md), [Unity setup](docs/unity-hmi/SETUP.md), and the [build brief](BUILD-BRIEF.md) for details and remaining limitations. This verifies the local Editor input layer, not a live Decisions-powered agent or a Player/Web build.

## Existing work and credits

Unity supplies **Industry HMI Template 1.0.4**, including vehicle/cabin/traffic assets, scenes and template scripts. The download was verified against the catalog digest. Original notices are preserved in [TEMPLATE-LICENSE.md](unity-hmi/TEMPLATE-LICENSE.md), [TEMPLATE-README.md](unity-hmi/TEMPLATE-README.md), and [third-party notices](unity-hmi/TEMPLATE-ThirdPartyNotice.txt). The Unity Companion License and bundled third-party licences continue to apply; this repository does not relicense those materials.

Glance additions made during the hackathon include the isolated project setup, pointer automation extension, E2E verification script, evidence and updated product/architecture planning. Codex helped diagnose input focus/device routing, implement the pointer driver, and verify UI navigation and climate adjustment. The architecture graphic describes the planned system; it does not establish that all components are implemented.

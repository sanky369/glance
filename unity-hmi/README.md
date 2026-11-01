# Glance Unity HMI

This folder is the isolated Unity project. The Glance task runner, scorer and dashboard belong outside it at the repository root.

Based on Unity's **Industry HMI Template 1.0.4**, package `com.unity.template.hmi`, created with Unity CLI and opened in **Unity 6000.6.3f1 (Apple Silicon)**. CLI automation is provided by `com.unity.pipeline` **0.9.1-exp.1**. No Unity Cloud project was created or linked.

From the repository root:

```sh
unity open "$PWD/unity-hmi" --wait
```

In the Editor, open `Assets/Scenes/Main.unity` and press Play. The bottom bar switches between Control, ADAS, Cluster and Climate. Main loads these screens additively; open Main rather than starting a screen alone.

`--wait` keeps the CLI launcher attached. During setup, the first launch reported ready but its Editor subsequently exited. Relaunching with `--wait` remained stable throughout verification; the cause of the first exit is not established.

Verified: the Body Control, HVAC and ADAS screens render; frames advance; the final console check reports zero errors and warnings. Screen navigation was exercised through the template's navigation API. Pointer-driven Climate navigation and driver-temperature adjustment to 21°C are now verified through actual Input System mouse events; see [pointer automation](../docs/unity-hmi/POINTER-AUTOMATION.md). No Glance radio task or state-export bridge has been implemented.

The template uses uGUI, URP, and Input System. Shared vehicle state is in `Assets/Scripts/ModuleScripts/CarData.cs`; climate controls expose separate `LeftTemperature` and `RightTemperature` values. Glance must choose a side explicitly when defining the climate task. The template's existing music strip is not the planned radio-preset task.

Keep Glance additions under `Assets/Glance/UI/` and `Assets/Glance/StateBridge/` when those are implemented. Preserve the supplied template as the baseline and keep evaluator code outside this project.

Setup evidence and repeatable checks: [../docs/unity-hmi/SETUP.md](../docs/unity-hmi/SETUP.md).

## Credits

Unity Technologies supplies the template, scenes, scripts and bundled assets. The original package README, Unity Companion License notice and third-party notices are preserved in `TEMPLATE-README.md`, `TEMPLATE-LICENSE.md` and `TEMPLATE-ThirdPartyNotice.txt`. Their respective terms continue to apply.

Unity automatically updated several package versions while opening the 6.3 template in 6.6, including URP 17.6.0, uGUI 2.6.0, Splines 2.9.1 and Cinemachine 6.6.0. The resolved versions are recorded in `Packages/manifest.json` and `Packages/packages-lock.json`.

Generated `Library/`, `Temp/`, `Logs/`, `UserSettings/` and build output are excluded by the repository's `.gitignore`. Keep raw Editor and licensing logs private; they can contain authentication material.

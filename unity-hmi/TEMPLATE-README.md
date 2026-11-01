# Industry HMI Template

A functional automotive **HMI (Human-Machine Interface)** template for Unity 6. Use it as a starting point for in-vehicle UI projects, or as a reference for HMI design patterns in Unity.

## Requirements

- **Unity 6000.3** or later
- **Universal Render Pipeline (URP)** and the **Input System** — both included as dependencies

## Getting started

1. In the **Unity Hub**, click **New project** and pick **Industry HMI Template**.
2. Open **`Assets/Scenes/Main.unity`** (it opens by default in a new project).
3. Press **Play** — the shell starts on the **Body Control** screen.
4. Use the **bottom bar** to switch screens and the **Color Selector** to switch color themes live.

## Screens

The `Main` scene is the shell — gear selector, date/time and battery in the top bar, screen switcher and Color Selector in the bottom bar — and loads each screen into its viewport:

| Screen | What it does |
|---|---|
| **Body Control** | Interactive 3D car — open and close the doors, hood, and trunk |
| **ADAS** | Driver-assistance visualization with a live traffic simulation — lanes, intersections, traffic lights, pedestrians, and lane changes |
| **Cluster** | Speed dial, battery level, and energy consumption gauges |
| **HVAC** | Climate controls — temperature, airflow, heated seats, auto and on/off |

Colors are driven by a `ThemeData` ScriptableObject; vehicle state shared across screens lives in a `CarData` ScriptableObject.

## Package dependencies

| Package | Version |
|---|---|
| Universal Render Pipeline (URP) | 17.3.0 |
| Input System | 1.20.0 |
| Cinemachine | 3.1.7 |
| Splines | 2.9.0 |
| 2D Sprite | 1.0.0 |
| UI (uGUI) | 2.0.0 |
| Test Framework | 1.6.0 |

## License

Licensed under the [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license). Bundled fonts (Michroma, Inter, Liberation Sans) ship under the SIL Open Font License — see `ThirdPartyNotice.txt`.

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

[InitializeOnLoad]
public static class SimulationToolbar
{
    private const string k_TimescaleGroup = "HMI Utilities/Timescale";
    private const string k_FramerateGroup = "HMI Utilities/Framerate";

    private static readonly float[] s_Timescales = { 0.5f, 1f, 2f };
    private static readonly int[] s_Framerates = { 10, 30, 60, 0 };

    static SimulationToolbar()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.delayCall += ResetToDefaults;
    }

    private static void ResetToDefaults()
    {
        Time.timeScale = 1f;
        Application.targetFrameRate = 0;
        MainToolbar.Refresh(k_TimescaleGroup);
        MainToolbar.Refresh(k_FramerateGroup);
    }

    [MainToolbarElement(k_TimescaleGroup, defaultDockPosition = MainToolbarDockPosition.Right)]
    private static IEnumerable<MainToolbarElement> TimescaleButtons()
    {
        foreach (float scale in s_Timescales)
        {
            float s = scale;
            string label = $"{s:0.#}x";
            MainToolbarContent content = new(label, $"Set Time.timeScale to {s}");
            yield return new MainToolbarToggle(content, Mathf.Approximately(Time.timeScale, s), value =>
            {
                if (value) Time.timeScale = s;
                MainToolbar.Refresh(k_TimescaleGroup);
            });
        }
    }

    [MainToolbarElement(k_FramerateGroup, defaultDockPosition = MainToolbarDockPosition.Right)]
    private static IEnumerable<MainToolbarElement> FramerateButtons()
    {
        foreach (int fps in s_Framerates)
        {
            int f = fps;
            string label = f == 0 ? "∞" : f.ToString();
            MainToolbarContent content = new(label, $"Set Application.targetFrameRate to {f}");
            yield return new MainToolbarToggle(content, Application.targetFrameRate == f, value =>
            {
                if (value) Application.targetFrameRate = f;
                MainToolbar.Refresh(k_FramerateGroup);
            });
        }
    }
}
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;

[InitializeOnLoad]
public static class ScenesToolbar
{
    private const string k_ScenesGroup = "HMI Utilities/Scene Buttons";

    private static readonly (string label, string path)[] s_Scenes =
    {
        ("Main",        "Assets/Scenes/Main.unity"),
        ("BodyControl", "Assets/Scenes/BodyControl.unity"),
        ("ADAS",        "Assets/Scenes/ADAS.unity"),
        ("Cluster",     "Assets/Scenes/Cluster.unity"),
        ("HVAC",        "Assets/Scenes/HVAC.unity"),
    };

    static ScenesToolbar() { }

    [MainToolbarElement(k_ScenesGroup, defaultDockPosition = MainToolbarDockPosition.Left)]
    private static IEnumerable<MainToolbarElement> SceneButtons()
    {
        foreach ((string label, string path) in s_Scenes)
        {
            string p = path;
            string l = $"{label}";
            MainToolbarContent content = new(l, $"Open scene {p}");
            yield return new MainToolbarButton(content, () =>
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
            });
        }
    }
}

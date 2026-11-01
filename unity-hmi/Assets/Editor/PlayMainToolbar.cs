using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;

[InitializeOnLoad]
public static class PlayMainToolbar
{
    private const string k_Group = "HMI Utilities/Play Main";
    private const string k_MainScenePath = "Assets/Scenes/Main.unity";

    private const string k_ReturnSceneKey = "PlayMainToolbar.ReturnScenePath";
    private const string k_ActiveKey = "PlayMainToolbar.Active";

    static PlayMainToolbar()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode) return;
        if (!SessionState.GetBool(k_ActiveKey, false)) return;

        string returnPath = SessionState.GetString(k_ReturnSceneKey, string.Empty);
        SessionState.EraseBool(k_ActiveKey);
        SessionState.EraseString(k_ReturnSceneKey);

        if (string.IsNullOrEmpty(returnPath)) return;
        EditorApplication.delayCall += () => EditorSceneManager.OpenScene(returnPath, OpenSceneMode.Single);
    }

    [MainToolbarElement(k_Group, defaultDockPosition = MainToolbarDockPosition.Left)]
    private static IEnumerable<MainToolbarElement> PlayMainButton()
    {
        MainToolbarContent content = new("▶ Main", "Open Main.unity and enter Play Mode; restores the current scene on exit.");
        yield return new MainToolbarButton(content, () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            string currentPath = EditorSceneManager.GetActiveScene().path;
            if (!string.IsNullOrEmpty(currentPath)) EditorSceneManager.SaveOpenScenes();
            SessionState.SetString(k_ReturnSceneKey, currentPath);
            SessionState.SetBool(k_ActiveKey, true);

            EditorSceneManager.OpenScene(k_MainScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        });
    }
}

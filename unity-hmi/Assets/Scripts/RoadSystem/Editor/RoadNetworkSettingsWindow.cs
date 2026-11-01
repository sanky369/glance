using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoadSystem.Editor
{
    /// <summary>
    /// Floating panel that mirrors a RoadNetworkManager's Inspector so the
    /// user can tune the network without keeping the manager selected.
    /// </summary>
    public class RoadNetworkSettingsWindow : EditorWindow
    {
        [MenuItem("Tools/Road System/Settings Panel")]
        public static void Open()
        {
            var w = GetWindow<RoadNetworkSettingsWindow>(false, "Road Network", true);
            w.minSize = new Vector2(280f, 320f);
        }

        [SerializeField] RoadNetworkManager m_Target;
        SerializedObject m_So;
        Vector2 m_Scroll;

        readonly List<RoadNetworkManager> m_Managers = new();
        readonly List<string> m_ManagerNames = new();

        void OnEnable()
        {
            EditorSceneManager_Subscribe();
            RefreshManagers();
            EnsureSerializedObject();
        }

        void OnDisable()
        {
            EditorSceneManager_Unsubscribe();
        }

        void EditorSceneManager_Subscribe()
        {
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += OnSceneOpened;
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        }

        void EditorSceneManager_Unsubscribe()
        {
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= OnSceneOpened;
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
        }

        void OnSceneOpened(Scene s, UnityEditor.SceneManagement.OpenSceneMode mode) => RefreshManagers();
        void OnActiveSceneChanged(Scene a, Scene b) => RefreshManagers();

        void RefreshManagers()
        {
            m_Managers.Clear();
            m_ManagerNames.Clear();
            var found = Object.FindObjectsByType<RoadNetworkManager>(FindObjectsSortMode.None);
            foreach (var m in found)
            {
                m_Managers.Add(m);
                m_ManagerNames.Add(m.name);
            }
            if (m_Target == null && m_Managers.Count > 0)
                m_Target = m_Managers[0];
            EnsureSerializedObject();
        }

        void EnsureSerializedObject()
        {
            m_So = m_Target != null ? new SerializedObject(m_Target) : null;
        }

        void OnGUI()
        {
            DrawHeader();

            if (m_Target == null)
            {
                EditorGUILayout.HelpBox(
                    "No RoadNetworkManager found in any open scene. " +
                    "Add one to a scene, then click Refresh.",
                    MessageType.Warning);
                return;
            }

            if (m_So == null) EnsureSerializedObject();
            m_So.Update();

            EditorGUILayout.Space(6);
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

            EditorGUI.BeginChangeCheck();

            // Iterate visible properties, skip the script field
            var prop = m_So.GetIterator();
            prop.NextVisible(true);
            while (prop.NextVisible(false))
            {
                if (prop.name == "m_Intersections") continue;
                EditorGUILayout.PropertyField(prop, true);
            }

            bool changed = EditorGUI.EndChangeCheck();
            m_So.ApplyModifiedProperties();

            if (changed)
            {
                var mgr = m_Target;
                EditorApplication.delayCall += () =>
                {
                    if (mgr == null) return;
                    mgr.SyncGlobalSettingsToIntersections();
                    mgr.SyncGlobalSettingsToRoads();
                    mgr.RebuildAllMeshes();
                    EditorUtility.SetDirty(mgr);
                };
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);
            DrawActions();
        }

        void DrawHeader()
        {
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Target Network", GUILayout.Width(100f));

                int idx = m_Target != null ? m_Managers.IndexOf(m_Target) : -1;
                int newIdx = EditorGUILayout.Popup(idx,
                    m_ManagerNames.Count > 0 ? m_ManagerNames.ToArray() : new[] { "(none)" });

                if (newIdx >= 0 && newIdx < m_Managers.Count && newIdx != idx)
                {
                    m_Target = m_Managers[newIdx];
                    EnsureSerializedObject();
                }

                if (GUILayout.Button("Refresh", GUILayout.Width(60f)))
                {
                    RefreshManagers();
                }
            }

            if (m_Target != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select in Hierarchy"))
                    {
                        Selection.activeGameObject = m_Target.gameObject;
                        EditorGUIUtility.PingObject(m_Target.gameObject);
                    }
                }
            }
        }

        void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Detect Intersections", GUILayout.Height(28f)))
            {
                Undo.RecordObject(m_Target, "Detect Intersections");
                m_Target.DetectAndBuildIntersections();
                EditorUtility.SetDirty(m_Target);
            }

            if (GUILayout.Button("Rebuild All Roads", GUILayout.Height(28f)))
            {
                Undo.RecordObject(m_Target, "Rebuild All Roads");
                m_Target.RebuildAll();
                EditorUtility.SetDirty(m_Target);
            }

            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("Clear Intersections"))
            {
                Undo.RecordObject(m_Target, "Clear Intersections");
                m_Target.ClearIntersections();
                EditorUtility.SetDirty(m_Target);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Bake", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Replaces each road / intersection MeshFilter with a saved Mesh asset " +
                "and disables the procedural scripts. Re-enable those components to " +
                "resume editing. Spline_VehiclePath splines are preserved.",
                MessageType.Info);

            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("Bake to Static Meshes", GUILayout.Height(28f)))
            {
                RoadNetworkBaker.Bake(m_Target);
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(1f, 0.85f, 0.5f);
            if (GUILayout.Button("Unbake (Re-enable Procedural)", GUILayout.Height(28f)))
            {
                RoadNetworkBaker.Unbake(m_Target);
            }
            GUI.backgroundColor = Color.white;
        }
    }
}

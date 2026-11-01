using UnityEditor;
using UnityEngine;

namespace RoadSystem.Editor
{
    [CustomEditor(typeof(RoadNetworkManager))]
    public class RoadNetworkManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();

            if (EditorGUI.EndChangeCheck())
            {
                var manager = (RoadNetworkManager)target;
                EditorApplication.delayCall += () =>
                {
                    if (manager != null)
                    {
                        manager.SyncGlobalSettingsToIntersections();
                        manager.SyncGlobalSettingsToRoads();
                        manager.RebuildAllMeshes();
                        EditorUtility.SetDirty(manager);
                    }
                };
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            var mgr = (RoadNetworkManager)target;

            if (GUILayout.Button("Detect Intersections", GUILayout.Height(30)))
            {
                Undo.RecordObject(mgr, "Detect Intersections");
                mgr.DetectAndBuildIntersections();
                EditorUtility.SetDirty(mgr);
            }

            if (GUILayout.Button("Rebuild All Roads", GUILayout.Height(30)))
            {
                Undo.RecordObject(mgr, "Rebuild All Roads");
                mgr.RebuildAll();
                EditorUtility.SetDirty(mgr);
            }

            EditorGUILayout.Space(4);

            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("Clear Intersections"))
            {
                Undo.RecordObject(mgr, "Clear Intersections");
                mgr.ClearIntersections();
                EditorUtility.SetDirty(mgr);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Bake", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Bake replaces each road / intersection's MeshFilter with a saved Mesh " +
                "asset (under Assets/RoadSystem/BakedMeshes/<network name>) and disables " +
                "the procedural scripts (RoadSegment, LaneSplineMirror, RoadIntersection). " +
                "Spline_VehiclePath SplineContainers are left untouched so driving logic " +
                "keeps working.\n\n" +
                "To resume procedural editing later, just re-enable those components.",
                MessageType.Info);

            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("Bake to Static Meshes", GUILayout.Height(30)))
            {
                RoadNetworkBaker.Bake(mgr);
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(1f, 0.85f, 0.5f);
            if (GUILayout.Button("Unbake (Re-enable Procedural)", GUILayout.Height(30)))
            {
                RoadNetworkBaker.Unbake(mgr);
            }
            GUI.backgroundColor = Color.white;
        }
    }

    [CustomEditor(typeof(RoadIntersection))]
    public class RoadIntersectionEditor : UnityEditor.Editor
    {
        float m_PrevCurvature;

        void OnEnable()
        {
            var ix = target as RoadIntersection;
            if (ix != null) m_PrevCurvature = ix.CornerCurvature;
        }

        public override void OnInspectorGUI()
        {
            var ix = (RoadIntersection)target;

            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();

            // When the local Corner Curvature slider changes,
            // push the new value to all per-corner entries
            if (!Mathf.Approximately(ix.CornerCurvature, m_PrevCurvature))
            {
                Undo.RecordObject(ix, "Sync Corner Curvature");
                var pc = ix.PerCornerCurvature;
                for (int i = 0; i < pc.Count; i++)
                    pc[i] = ix.CornerCurvature;
                m_PrevCurvature = ix.CornerCurvature;
                changed = true;
            }

            // Per-corner sliders (only editable when override is on)
            var perCorner = ix.PerCornerCurvature;
            if (perCorner != null && perCorner.Count > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Corner Curvature Overrides", EditorStyles.boldLabel);

                if (!ix.OverrideCurvature)
                {
                    EditorGUILayout.HelpBox(
                        "Enable 'Override Curvature' above to edit curvature independently from the global settings.",
                        MessageType.Info);
                }

                using (new EditorGUI.DisabledGroupScope(!ix.OverrideCurvature))
                {
                    for (int i = 0; i < perCorner.Count; i++)
                    {
                        float val = EditorGUILayout.Slider($"Corner {i}", perCorner[i], -1f, 1f);
                        if (!Mathf.Approximately(val, perCorner[i]))
                        {
                            Undo.RecordObject(ix, "Change Corner Curvature");
                            perCorner[i] = val;
                            changed = true;
                        }
                    }
                }
            }

            if (changed)
            {
                foreach (var t in targets)
                {
                    var interX = (RoadIntersection)t;
                    Undo.RecordObject(interX, "Update Intersection");
                    interX.RebuildMesh();
                    EditorUtility.SetDirty(interX);
                }
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Force Rebuild Mesh"))
            {
                foreach (var t in targets)
                    ((RoadIntersection)t).RebuildMesh();
            }
        }
    }

    [CustomEditor(typeof(RoadSegment))]
    public class RoadSegmentEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            // Draw all fields manually so we can disable m_SegmentsPerMeter
            // when m_OverrideSegmentsPerMeter is off
            var iterator = serializedObject.GetIterator();
            iterator.NextVisible(true); // skip script field
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(iterator);
            EditorGUI.EndDisabledGroup();

            var overrideProp = serializedObject.FindProperty("m_OverrideSegmentsPerMeter");

            while (iterator.NextVisible(false))
            {
                if (iterator.name == "m_SegmentsPerMeter")
                {
                    using (new EditorGUI.DisabledGroupScope(!overrideProp.boolValue))
                    {
                        EditorGUILayout.PropertyField(iterator);
                    }
                    if (!overrideProp.boolValue)
                    {
                        EditorGUILayout.HelpBox(
                            "Enable 'Override Segments Per Meter' below to edit this independently from the global setting in RoadNetworkManager.",
                            MessageType.Info);
                    }
                }
                else
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }
            }

            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();

            if (changed)
            {
                EditorApplication.delayCall += () =>
                {
                    foreach (var t in targets)
                        if (t != null) ((RoadSegment)t).RebuildMesh();
                };
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Force Rebuild Mesh"))
            {
                foreach (var t in targets)
                    ((RoadSegment)t).RebuildMesh();
            }
        }
    }
}

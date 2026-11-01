using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using HMI_Hub_Template;

/// <summary>
/// Sets the Y component of every knot Position to exactly 0 on all vehicle
/// lane splines (both road and intersection).  Nothing else is changed.
///
/// Run via:  Tools → Road Network → Flatten All Spline Knots to Y = 0
/// </summary>
public static class FlattenSplineKnots
{
    [MenuItem("Tools/Road Network/Flatten All Spline Knots to Y = 0")]
    public static void Run()
    {
        int fixedKnots = 0, fixedSplines = 0;

        var tags = Object.FindObjectsByType<RoadSplineTagComponent>(FindObjectsSortMode.InstanceID);
        foreach (var tag in tags)
        {
            var sc = tag.GetComponent<SplineContainer>();
            if (sc == null) continue;

            var sp      = sc.Spline;
            bool dirty  = false;

            for (int i = 0; i < sp.Count; i++)
            {
                var k = sp[i];
                if (k.Position.y == 0f) continue;

                Undo.RecordObject(sc, "Flatten Knot Y");
                k.Position = new float3(k.Position.x, 0f, k.Position.z);
                sp.SetKnot(i, k);
                fixedKnots++;
                dirty = true;
            }

            if (dirty)
            {
                EditorUtility.SetDirty(sc);
                fixedSplines++;
            }
        }

        if (fixedKnots > 0)
            EditorSceneManager.MarkAllScenesDirty();

        Debug.Log($"[FlattenKnots] Fixed {fixedKnots} knot(s) across {fixedSplines} spline(s).");
    }
}

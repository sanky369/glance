using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using HMI_Hub_Template;

/// <summary>
/// Eliminates the hitch/jitter that appears when a vehicle transitions from a
/// road spline onto an intersection spline by ensuring the intersection K0
/// exactly matches the road's last knot in position, rotation, and tangent.
///
/// For every road → intersection connection in LaneRegistry.nodeMap:
///
///   1. If the intersection K0 world position differs from the road kEnd world
///      position by more than kPosTol, move K0 to that position.
///
///   2. If the intersection K0 tangent direction differs from the road exit
///      tangent by more than kDirTol degrees, align K0's tangent to match.
///
///   3. After snapping K0, measure the angle between the road exit direction
///      and the chord K0 → K1.  If this exceeds kHarshDeg, insert one
///      AutoSmooth knot just inside K0 (along the road exit direction) so the
///      spline eases gently away from the entry heading rather than kinking.
///
/// K0 is set to TangentMode.Mirrored so the explicit tangent is preserved and
/// not overwritten by AutoSmooth.  All other knots remain untouched.
///
/// Run via:  Tools → Road Network → Snap Intersection Entry Knots to Roads
/// </summary>
public static class SnapIntersectionEntries
{
    const float kPosTol      = 0.005f; // metres  — position match threshold
    const float kDirTol      = 3f;     // degrees — tangent direction match threshold
    const float kHarshDeg    = 20f;    // degrees — insert smoothing knot above this
    const float kSmoothFrac  = 0.33f;  // smoothing knot placed this fraction along K0→K1
    const float kMaxSnapDist = 2.0f;   // metres  — skip if gap is implausibly large

    [MenuItem("Tools/Road Network/Snap Intersection Entry Knots to Roads")]
    public static void Run()
    {
        var reg = Object.FindFirstObjectByType<LaneRegistry>();
        if (reg == null) { Debug.LogError("[SnapIntEntry] LaneRegistry not found."); return; }

        if (reg.nodeMap == null || reg.nodeMap.Length == 0 ||
            reg.vehicleLanes == null || reg.vehicleLanes.Length == 0)
        {
            Debug.Log("[SnapIntEntry] Baking node map…");
            reg.BakeNodeMap();
        }

        int snapped = 0, inserted = 0, skipped = 0;

        foreach (var node in reg.nodeMap)
        {
            if (node?.sourceSpline == null) continue;

            // Only process road (non-intersection) source splines.
            var sTag = node.sourceSpline.GetComponent<RoadSplineTagComponent>();
            if (sTag == null || sTag.isIntersection) continue;

            var road     = node.sourceSpline;
            int roadLast = road.Spline.Count - 1;
            if (roadLast < 1) continue;

            Vector3 roadEndW  = road.transform.TransformPoint((Vector3)(float3)road.Spline[roadLast].Position);
            Vector3 roadExitW = WorldTangentOut(road, roadLast);  // normalized exit direction

            foreach (var intSC in node.nextSplines)
            {
                if (intSC == null) continue;
                var iTag = intSC.GetComponent<RoadSplineTagComponent>();
                if (iTag == null || !iTag.isIntersection) continue;

                var sp = intSC.Spline;
                if (sp.Count < 2) continue;

                Vector3 k0W     = intSC.transform.TransformPoint((Vector3)(float3)sp[0].Position);
                Vector3 k0DirW  = WorldTangentOut(intSC, 0);
                float   posDist = Vector3.Distance(k0W, roadEndW);
                float   dirDiff = Vector3.Angle(roadExitW, k0DirW);

                // Skip if already aligned.
                if (posDist <= kPosTol && dirDiff <= kDirTol) continue;

                // Skip implausibly large gaps (wrong connection in node map).
                if (posDist > kMaxSnapDist)
                {
                    Debug.LogWarning($"[SnapIntEntry] Gap {posDist:F2} m between '{road.name}' " +
                                     $"and '{intSC.name}' — skipped (>{kMaxSnapDist} m).");
                    skipped++;
                    continue;
                }

                Undo.RecordObject(intSC, "Snap Intersection Entry Knot");

                // ── Snap K0 to road exit ──────────────────────────────────────
                SetFirstKnot(intSC, roadEndW, roadExitW);
                snapped++;

                // ── Optionally insert a smoothing knot ────────────────────────
                // After snapping, check how harshly K0's tangent disagrees with
                // the K0→K1 chord.  A large angle means the first Bezier segment
                // would curve sharply right after the junction, causing a jitter.
                // Insert one AutoSmooth knot along the entry direction so the
                // spline eases gently away from the approach heading.

                // Re-read K1 world pos (index 1, unchanged).
                Vector3 k1W      = intSC.transform.TransformPoint((Vector3)(float3)sp[1].Position);
                float   k0k1Dist = Vector3.Distance(roadEndW, k1W);

                if (k0k1Dist > 0.01f)
                {
                    Vector3 chordDir = (k1W - roadEndW).normalized;
                    float   angle    = Vector3.Angle(roadExitW, chordDir);

                    if (angle > kHarshDeg)
                    {
                        // Place the smoothing knot in the spline's local space.
                        Vector3 smoothW     = roadEndW + kSmoothFrac * k0k1Dist * roadExitW;
                        float3  smoothLocal = (float3)intSC.transform.InverseTransformPoint(smoothW);
                        smoothLocal.y = 0f;

                        // Insert at index 1 — Unity recomputes AutoSmooth tangents
                        // for the new knot and its neighbours automatically.
                        sp.Insert(1, smoothLocal, TangentMode.AutoSmooth);
                        inserted++;
                    }
                }

                EditorUtility.SetDirty(intSC);
            }
        }

        if (snapped > 0)
            EditorSceneManager.MarkAllScenesDirty();

        Debug.Log($"[SnapIntEntry] Snapped {snapped} entry knot(s), " +
                  $"inserted {inserted} smoothing knot(s), skipped {skipped}.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// Sets K0 of sc to worldPos with tangent worldDir, using TangentMode.Mirrored
    /// so the explicit tangent is not overwritten by AutoSmooth on neighbouring edits.
    static void SetFirstKnot(SplineContainer sc, Vector3 worldPos, Vector3 worldDir)
    {
        var sp = sc.Spline;

        Vector3 localPos = sc.transform.InverseTransformPoint(worldPos);
        Vector3 localDir = sc.transform.InverseTransformDirection(worldDir).normalized;
        localPos.y = 0f;

        float yDeg = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
        var   rot  = (quaternion)Quaternion.Euler(0f, yDeg, 0f);

        // Handle magnitude: 1/3 of the chord to K1 gives C1-smooth blending.
        float mag = sp.Count > 1
            ? Vector3.Distance(localPos, (Vector3)(float3)sp[1].Position) / 3f
            : 0.05f;
        mag = Mathf.Max(mag, 0.001f);

        var k = sp[0];
        k.Position   = (float3)localPos;
        k.Rotation   = rot;
        k.TangentOut = new float3(0f, 0f,  mag);
        k.TangentIn  = new float3(0f, 0f, -mag);
        sp.SetKnot(0, k);

        // Mirrored keeps TangentOut as-is and sets TangentIn = -TangentOut.
        sp.SetTangentMode(0, TangentMode.Mirrored, BezierTangent.Out);

        // Nudge K1 so its AutoSmooth tangent re-evaluates from the new K0 position.
        if (sp.Count > 1)
            sp.SetTangentMode(1, sp.GetTangentMode(1));
    }

    /// World-space forward tangent at knot idx. Falls back to the chord
    /// between knot idx and its neighbour when the stored tangent is near-zero.
    static Vector3 WorldTangentOut(SplineContainer sc, int idx)
    {
        var     sp  = sc.Spline;
        var     k   = sp[idx];
        Vector3 loc = ((Quaternion)(quaternion)k.Rotation) * (Vector3)(float3)k.TangentOut;

        if (loc.sqrMagnitude > 1e-8f)
            return sc.transform.TransformDirection(loc).normalized;

        if (idx < sp.Count - 1)
        {
            Vector3 a = sc.transform.TransformPoint((Vector3)(float3)sp[idx].Position);
            Vector3 b = sc.transform.TransformPoint((Vector3)(float3)sp[idx + 1].Position);
            if ((b - a).sqrMagnitude > 1e-10f) return (b - a).normalized;
        }
        if (idx > 0)
        {
            Vector3 a = sc.transform.TransformPoint((Vector3)(float3)sp[idx - 1].Position);
            Vector3 b = sc.transform.TransformPoint((Vector3)(float3)sp[idx].Position);
            if ((b - a).sqrMagnitude > 1e-10f) return (b - a).normalized;
        }
        return sc.transform.forward;
    }
}

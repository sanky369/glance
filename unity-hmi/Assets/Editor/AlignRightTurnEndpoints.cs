using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using HMI_Hub_Template;

/// <summary>
/// Fixes right-turn intersection splines that are not precisely aligned to the
/// road splines they connect to.
///
/// Problem: SnapRoadSplinesToIntersections snaps a road's kEnd to whichever
/// intersection next-spline it processes last. When a road leads to a junction
/// with TWO routes (straight + right turn) it aligns to one but leaves the
/// other's K0 floating. The same mismatch can occur at the right turn exit.
///
/// This tool performs two passes using LaneRegistry.nodeMap:
///
///   Pass 1 — Road kEnd → Right Turn K0
///     For every non-intersection road with 2+ intersection next-splines,
///     find the turning one(s) and snap their K0 position + tangent to the
///     road's last knot.
///
///   Pass 2 — Right Turn kEnd → Next Road K0
///     For every right-turn intersection spline, if its last knot doesn't sit
///     exactly on the next road's K0, move the last knot there and match its
///     tangent.
///
/// Endpoints are changed to TangentMode.Mirrored with an explicit tangent that
/// matches the connecting spline's direction.  Interior knots remain AutoSmooth.
///
/// Run via:  Tools → Road Network → Align Right Turn Endpoints
/// </summary>
public static class AlignRightTurnEndpoints
{
    const float kSnapTol      = 0.005f;  // metres — closer than this = already aligned
    const float kTurnThreshDeg = 30f;    // > this angle → classify as a turning spline

    [MenuItem("Tools/Road Network/Align Right Turn Endpoints")]
    public static void Run()
    {
        var reg = Object.FindFirstObjectByType<LaneRegistry>();
        if (reg == null) { Debug.LogError("[AlignRightTurns] LaneRegistry not found."); return; }

        if (reg.nodeMap == null || reg.nodeMap.Length == 0 ||
            reg.vehicleLanes == null || reg.vehicleLanes.Length == 0)
        {
            Debug.Log("[AlignRightTurns] Baking node map…");
            reg.BakeNodeMap();
        }

        int snappedK0 = 0, snappedKEnd = 0;

        // ── Pass 1: Road kEnd → Right Turn K0 ───────────────────────────────
        // Only runs on roads that feed into 2+ intersection splines (straight + turn).
        foreach (var node in reg.nodeMap)
        {
            if (node?.sourceSpline == null) continue;
            var sTag = node.sourceSpline.GetComponent<RoadSplineTagComponent>();
            if (sTag == null || sTag.isIntersection) continue;  // road only

            // Collect intersection nextSplines for this road.
            var intNextList = node.nextSplines
                .Where(s => s != null &&
                            (s.GetComponent<RoadSplineTagComponent>()?.isIntersection ?? false))
                .ToList();

            if (intNextList.Count < 2) continue;  // only junctions with 2+ options

            // Road last-knot world position and exit tangent direction.
            var roadSp  = node.sourceSpline;
            int roadLast = roadSp.Spline.Count - 1;
            Vector3 roadEndWorldPos = roadSp.transform.TransformPoint(
                (Vector3)(float3)roadSp.Spline[roadLast].Position);
            Vector3 roadEndWorldDir = WorldTangentOut(roadSp, roadLast);

            foreach (var intSC in intNextList)
            {
                if (!IsTurnSpline(intSC)) continue;  // skip straight paths

                Vector3 k0World = intSC.transform.TransformPoint(
                    (Vector3)(float3)intSC.Spline[0].Position);

                if (Vector3.Distance(k0World, roadEndWorldPos) <= kSnapTol) continue;

                Undo.RecordObject(intSC, "Align Right Turn K0");
                SnapFirstKnot(intSC, roadEndWorldPos, roadEndWorldDir);
                EditorUtility.SetDirty(intSC);
                snappedK0++;
            }
        }

        // ── Pass 2: Right Turn kEnd → Next Road K0 ──────────────────────────
        foreach (var node in reg.nodeMap)
        {
            if (node?.sourceSpline == null) continue;
            var sTag = node.sourceSpline.GetComponent<RoadSplineTagComponent>();
            if (sTag == null || !sTag.isIntersection) continue;  // intersection only
            if (!IsTurnSpline(node.sourceSpline))      continue;  // turning only

            var turnSC  = node.sourceSpline;
            int turnLast = turnSC.Spline.Count - 1;
            Vector3 kEndWorld = turnSC.transform.TransformPoint(
                (Vector3)(float3)turnSC.Spline[turnLast].Position);

            foreach (var nextSC in node.nextSplines)
            {
                if (nextSC == null) continue;
                var nTag = nextSC.GetComponent<RoadSplineTagComponent>();
                if (nTag == null || nTag.isIntersection) continue;  // road only

                Vector3 nextK0World = nextSC.transform.TransformPoint(
                    (Vector3)(float3)nextSC.Spline[0].Position);

                if (Vector3.Distance(kEndWorld, nextK0World) <= kSnapTol) continue;

                Vector3 nextK0WorldDir = WorldTangentOut(nextSC, 0);

                Undo.RecordObject(turnSC, "Align Right Turn KEnd");
                SnapLastKnot(turnSC, nextK0World, nextK0WorldDir);
                EditorUtility.SetDirty(turnSC);
                snappedKEnd++;
            }
        }

        if (snappedK0 + snappedKEnd > 0)
            EditorSceneManager.MarkAllScenesDirty();

        Debug.Log($"[AlignRightTurns] Snapped {snappedK0} K0(s) to road exit, " +
                  $"{snappedKEnd} kEnd(s) to next road K0.");
    }

    // ── Snap helpers ─────────────────────────────────────────────────────────

    /// Moves K0 of sc to worldPos, sets its tangent direction to worldDir,
    /// and switches K0 to Mirrored so the explicit tangent is preserved.
    static void SnapFirstKnot(SplineContainer sc, Vector3 worldPos, Vector3 worldDir)
    {
        var sp = sc.Spline;

        Vector3 localPos = sc.transform.InverseTransformPoint(worldPos);
        Vector3 localDir = sc.transform.InverseTransformDirection(worldDir).normalized;

        float yDeg = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
        var   rot  = (quaternion)Quaternion.Euler(0f, yDeg, 0f);

        // Magnitude: 1/3 chord to next knot for C1 smoothness.
        float mag = sp.Count > 1
            ? Vector3.Distance(localPos, (Vector3)(float3)sp[1].Position) / 3f
            : 0.05f;

        var k = sp[0];
        k.Position   = (float3)localPos;
        k.Rotation   = rot;
        k.TangentOut = new float3(0f, 0f,  mag);
        k.TangentIn  = new float3(0f, 0f, -mag);
        sp.SetKnot(0, k);
        sp.SetTangentMode(0, TangentMode.Mirrored, BezierTangent.Out);

        // Force adjacent AutoSmooth knot to re-evaluate from the new K0 position.
        if (sp.Count > 1)
            sp.SetTangentMode(1, TangentMode.AutoSmooth);
    }

    /// Moves the last knot of sc to worldPos, aligns its tangent to worldDir,
    /// and switches it to Mirrored.
    static void SnapLastKnot(SplineContainer sc, Vector3 worldPos, Vector3 worldDir)
    {
        var sp   = sc.Spline;
        int last = sp.Count - 1;

        Vector3 localPos = sc.transform.InverseTransformPoint(worldPos);
        Vector3 localDir = sc.transform.InverseTransformDirection(worldDir).normalized;

        float yDeg = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
        var   rot  = (quaternion)Quaternion.Euler(0f, yDeg, 0f);

        // Magnitude: 1/3 chord from previous knot.
        float mag = last > 0
            ? Vector3.Distance(localPos, (Vector3)(float3)sp[last - 1].Position) / 3f
            : 0.05f;

        var k = sp[last];
        k.Position   = (float3)localPos;
        k.Rotation   = rot;
        k.TangentOut = new float3(0f, 0f,  mag);
        k.TangentIn  = new float3(0f, 0f, -mag);
        sp.SetKnot(last, k);
        // SetTangentMode auto-switches last knot to BezierTangent.In as main.
        sp.SetTangentMode(last, TangentMode.Mirrored);

        // Force adjacent AutoSmooth knot to re-evaluate from the new kEnd position.
        if (last > 0)
            sp.SetTangentMode(last - 1, TangentMode.AutoSmooth);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// World-space tangent-out direction at a given knot index.
    /// Falls back to chord direction if TangentOut is near-zero.
    static Vector3 WorldTangentOut(SplineContainer sc, int idx)
    {
        var     sp   = sc.Spline;
        var     k    = sp[idx];
        Vector3 tOut = ((Quaternion)(quaternion)k.Rotation) * (Vector3)(float3)k.TangentOut;

        if (tOut.sqrMagnitude > 1e-8f)
            return sc.transform.TransformDirection(tOut).normalized;

        // Fallback: chord to next (or from previous) knot.
        if (idx < sp.Count - 1)
        {
            Vector3 a = sc.transform.TransformPoint((Vector3)(float3)sp[idx].Position);
            Vector3 b = sc.transform.TransformPoint((Vector3)(float3)sp[idx + 1].Position);
            return (b - a).normalized;
        }
        if (idx > 0)
        {
            Vector3 a = sc.transform.TransformPoint((Vector3)(float3)sp[idx - 1].Position);
            Vector3 b = sc.transform.TransformPoint((Vector3)(float3)sp[idx].Position);
            return (b - a).normalized;
        }
        return Vector3.forward;
    }

    /// Returns true when the angle between the spline's first and last knot
    /// forward directions exceeds kTurnThreshDeg.
    static bool IsTurnSpline(SplineContainer sc)
    {
        var sp = sc.Spline;
        if (sp.Count < 2) return false;
        Vector3 d0 = KnotForward(sp[0]);
        Vector3 d1 = KnotForward(sp[sp.Count - 1]);
        return Vector3.Angle(d0, d1) > kTurnThreshDeg;
    }

    static Vector3 KnotForward(BezierKnot k)
    {
        Vector3 f = ((Quaternion)(quaternion)k.Rotation) * Vector3.forward;
        return new Vector3(f.x, 0f, f.z).normalized;
    }
}

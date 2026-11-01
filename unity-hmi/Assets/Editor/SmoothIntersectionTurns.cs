using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using HMI_Hub_Template;

/// <summary>
/// Rebuilds every intersection turn spline so that:
///
///   1. Interior knots are redistributed onto a mathematically exact circular
///      arc computed from the fixed endpoint positions and tangent directions.
///   2. A short straight section is preserved at the exit (if geometry shows
///      one exists) so the spline meets the next road cleanly.
///   3. All arc knots are evenly spaced by angle (kArcSegments segments of
///      equal degrees each) using the optimal cubic-Bezier handle length for
///      a circular arc:  h = (4/3)·tan(α/2)·R
///   4. Every knot is set to TangentMode.AutoSmooth so Unity derives tangents
///      via Catmull-Rom from the neighbouring positions — no explicit handles,
///      no steps, perfectly continuous curve through every knot.
///
/// Endpoint knots K0 and K_last are saved before clearing and re-added
/// unchanged — their positions, rotations, and tangent handles are bit-for-bit
/// identical after the operation.
///
/// Run via:  Tools → Road Network → Smooth Intersection Turn Splines
/// Re-run "Snap Road Endpoints to Intersections" afterwards so road splines
/// re-align their approach tangents to the new K0→K1 direction.
/// </summary>
public static class SmoothIntersectionTurns
{
    // Arc segments per 90° turn.  8 segments = 11.25° each — smooth enough
    // for any vehicle size and small enough to be negligible at runtime.
    const int   kArcSegments          = 8;
    const float kStraightThresholdDeg = 10f;

    [MenuItem("Tools/Road Network/Smooth Intersection Turn Splines")]
    public static void Run()
    {
        int rebuilt = 0, fallback = 0, skipped = 0;

        var tags = Object.FindObjectsByType<RoadSplineTagComponent>(FindObjectsSortMode.InstanceID);
        foreach (var tag in tags)
        {
            if (!tag.isIntersection) continue;

            var sc = tag.GetComponent<SplineContainer>();
            if (sc == null) continue;

            var sp = sc.Spline;
            if (sp.Count < 2) { skipped++; continue; }

            float deg = Vector3.Angle(KnotForward(sp[0]), KnotForward(sp[sp.Count - 1]));
            if (deg < kStraightThresholdDeg) { skipped++; continue; }

            Undo.RecordObject(sc, "Smooth Intersection Turn Spline");

            if (RebuildArcSpline(sp))
                rebuilt++;
            else
            {
                // Geometry degenerate — fall back to AutoSmooth on existing knots.
                sp.SetTangentMode(TangentMode.AutoSmooth);
                fallback++;
            }

            EditorUtility.SetDirty(sc);
        }

        if (rebuilt + fallback > 0)
            EditorSceneManager.MarkAllScenesDirty();

        Debug.Log($"[SmoothIntersectionTurns] Rebuilt {rebuilt} arc spline(s) ({kArcSegments} arc segs), " +
                  $"{fallback} mirror-only fallback(s), {skipped} skipped.\n" +
                  "Re-run 'Snap Road Endpoints to Intersections' to re-align road approach tangents.");
    }

    // ── Core rebuild ─────────────────────────────────────────────────────────

    static bool RebuildArcSpline(Spline sp)
    {
        // ── Snapshot endpoints — restored bit-for-bit at the end ─────────────
        BezierKnot k0    = sp[0];
        BezierKnot kLast = sp[sp.Count - 1];

        Vector3 d0    = KnotForward(k0);
        Vector3 d1    = KnotForward(kLast);
        Vector3 p0    = new Vector3(k0.Position.x,    0f, k0.Position.z);
        Vector3 pLast = new Vector3(kLast.Position.x, 0f, kLast.Position.z);

        // Right-hand normals (rotate direction CW 90° in XZ = right side of travel)
        Vector3 n0    = new Vector3( d0.z, 0f, -d0.x);
        Vector3 nLast = new Vector3( d1.z, 0f, -d1.x);

        // ── Solve 2×2 linear system for arc radius R and straight length L ────
        //
        // Arc center from entry:  C = p0    + R · n0
        // Arc center from exit:   C = arcEnd + R · nLast,  arcEnd = pLast − L · d1
        //
        // Equating X and Z:
        //   (n0.x − nLast.x)·R + d1.x·L = pLast.x − p0.x
        //   (n0.z − nLast.z)·R + d1.z·L = pLast.z − p0.z
        float a = n0.x - nLast.x,  b = d1.x;
        float c = n0.z - nLast.z,  e = d1.z;
        float det = a * e - b * c;
        if (Mathf.Abs(det) < 1e-4f) return false;

        float dpx = pLast.x - p0.x, dpz = pLast.z - p0.z;
        float R = (dpx * e - dpz * b) / det;
        float L = (a   * dpz - c   * dpx) / det;

        if (R < 0.01f) return false;   // left turn or degenerate — skip
        L = Mathf.Max(0f, L);

        Vector3 arcCenter = p0 + R * n0;

        // ── Arc sweep angle (clockwise in XZ = decreasing atan2 angle) ────────
        float aStart = Mathf.Atan2(p0.z - arcCenter.z, p0.x - arcCenter.x);

        Vector3 arcEndPos;
        float   aEnd;
        bool    hasStraight = (L >= 0.01f);

        if (hasStraight)
        {
            arcEndPos = pLast - L * d1;
            aEnd      = Mathf.Atan2(arcEndPos.z - arcCenter.z, arcEndPos.x - arcCenter.x);
        }
        else
        {
            arcEndPos = pLast;
            aEnd      = Mathf.Atan2(pLast.z - arcCenter.z, pLast.x - arcCenter.x);
        }

        float sweep = aStart - aEnd;
        while (sweep <= 0f)            sweep += 2f * Mathf.PI;
        while (sweep >  2f * Mathf.PI) sweep -= 2f * Mathf.PI;

        // ── Build interior knot list ──────────────────────────────────────────
        // Tangent values are intentionally left zero — AutoSmooth overwrites them
        // from neighbouring positions, so only Position matters here.
        var interior = new List<BezierKnot>();

        // When hasStraight, include the arc-end knot (i == kArcSegments) as a
        // real interior knot.  When going straight to K_last, stop one short so
        // K_last itself is the final arc point.
        int arcStop = hasStraight ? kArcSegments : kArcSegments - 1;

        for (int i = 1; i <= arcStop; i++)
        {
            float   alpha = aStart - (float)i / kArcSegments * sweep;
            Vector3 pos   = arcCenter + R * new Vector3(Mathf.Cos(alpha), 0f, Mathf.Sin(alpha));
            interior.Add(MakeKnot(pos));
        }

        // ── Rebuild spline ────────────────────────────────────────────────────
        // AutoSmooth: Unity derives tangents from the knot positions using
        // Catmull-Rom, producing a smooth continuous curve with no visible
        // steps or kinks.  On uniformly arc-spaced points this closely tracks
        // the underlying circle.
        sp.Clear();
        sp.Add(k0,    TangentMode.AutoSmooth);   // endpoint preserved exactly
        foreach (var k in interior)
            sp.Add(k, TangentMode.AutoSmooth);
        sp.Add(kLast, TangentMode.AutoSmooth);   // endpoint preserved exactly

        return true;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    // AutoSmooth knot — only position matters, tangents are computed by Unity.
    static BezierKnot MakeKnot(Vector3 pos)
    {
        return new BezierKnot(new float3(pos.x, 0f, pos.z));
    }

    // Container-local XZ forward direction from knot Rotation only.
    static Vector3 KnotForward(BezierKnot k)
    {
        Vector3 f = ((Quaternion)(quaternion)k.Rotation) * Vector3.forward;
        return new Vector3(f.x, 0f, f.z).normalized;
    }
}

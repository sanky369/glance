using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
using HMI_Hub_Template;

/// <summary>
/// LaneRegistry-driven road/intersection connector with Hermite position blending.
///
/// For each junction the nodeMap defines:
///
///   Pass 1 — Road Klast → Intersection K0
///     1. Trim road knots that overshoot the intersection entry.
///     2. Snap Klast to the intersection K0 world position.
///     3. Set Klast tangent to align with the intersection K0→K1 direction.
///     4. Hermite-blend the last kBlendFraction of the road: MOVE knot positions
///        from the original spline body smoothly into the snapped junction point,
///        so the car follows a clean arc rather than making a hard turn to get
///        onto the intersection.
///
///   Pass 2 — Intersection Klast → Road K0
///     Mirror of Pass 1: snap road K0 to the intersection exit, then
///     Hermite-blend the FIRST kBlendFraction of the departure road so it
///     eases away from the intersection direction into the road body.
///
///   Pass 3 — Y = 0 everywhere.
///
/// kBlendFraction (0.15) means the first/last 15% of each road's arc length
/// is reshaped; knots beyond that are untouched.
/// </summary>
public static class SnapRoadSplinesToIntersections
{
    const float kSnapEpsilon   = 0.01f;
    const float kBlendFraction = 0.15f;

    // ── Entry point ─────────────────────────────────────────────────────
    [MenuItem("Tools/Road Network/Snap Road Endpoints to Intersections")]
    public static void SnapEndpoints()
    {
        var reg = Object.FindFirstObjectByType<LaneRegistry>();
        if (reg == null) { Debug.LogError("[SnapRoad] LaneRegistry not found."); return; }

        if (reg.nodeMap == null || reg.nodeMap.Length == 0 ||
            reg.vehicleLanes == null || reg.vehicleLanes.Length == 0)
        {
            Debug.Log("[SnapRoad] Baking node map…");
            reg.BakeNodeMap();
        }
        if (reg.nodeMap == null || reg.nodeMap.Length == 0)
        { Debug.LogError("[SnapRoad] nodeMap empty after bake."); return; }

        int snapped = 0, pruned = 0, blended = 0, skipped = 0, yFixed = 0;

        // ── Pass 1: Road Klast → Intersection K0 ────────────────────────
        foreach (var node in reg.nodeMap)
        {
            if (node?.sourceSpline == null) continue;
            var sTag = node.sourceSpline.GetComponent<RoadSplineTagComponent>();
            if (sTag == null || sTag.isIntersection) continue;

            foreach (var intSC in node.nextSplines)
            {
                if (intSC == null) continue;
                var iTag = intSC.GetComponent<RoadSplineTagComponent>();
                if (iTag == null || !iTag.isIntersection) continue;

                var   sp     = intSC.Spline;
                Vector3 k0W  = intSC.transform.TransformPoint((Vector3)sp[0].Position);

                SnapLastKnot(node.sourceSpline, k0W, ref snapped, ref pruned, ref skipped);

                if (sp.Count >= 2)
                {
                    Vector3 k1W = intSC.transform.TransformPoint((Vector3)sp[1].Position);
                    AlignAndBlendEnd(node.sourceSpline, k0W, k1W, ref blended);
                }
            }
        }

        // ── Pass 2: Intersection Klast → Road K0 ────────────────────────
        foreach (var node in reg.nodeMap)
        {
            if (node?.sourceSpline == null) continue;
            var sTag = node.sourceSpline.GetComponent<RoadSplineTagComponent>();
            if (sTag == null || !sTag.isIntersection) continue;

            var sp    = node.sourceSpline.Spline;
            int last  = sp.Count - 1;
            Vector3 klW = node.sourceSpline.transform.TransformPoint((Vector3)sp[last].Position);

            Vector3 exitDir = Vector3.forward;
            if (last >= 1)
            {
                Vector3 penW = node.sourceSpline.transform.TransformPoint((Vector3)sp[last - 1].Position);
                exitDir = (klW - penW).normalized;
            }

            foreach (var roadSC in node.nextSplines)
            {
                if (roadSC == null) continue;
                var rTag = roadSC.GetComponent<RoadSplineTagComponent>();
                if (rTag == null || rTag.isIntersection) continue;

                SnapFirstKnot(roadSC, klW, ref snapped, ref pruned, ref skipped);
                AlignAndBlendStart(roadSC, exitDir, ref blended);
            }
        }

        // ── Pass 3: Y = 0 on every knot ─────────────────────────────────
        foreach (var tag in Object.FindObjectsByType<RoadSplineTagComponent>(FindObjectsSortMode.InstanceID))
        {
            var sc = tag.GetComponent<SplineContainer>();
            if (sc == null) continue;
            var sp = sc.Spline;
            bool d = false;
            for (int i = 0; i < sp.Count; i++)
            {
                var k = sp[i];
                if (k.Position.y != 0f)
                {
                    k.Position = new float3(k.Position.x, 0f, k.Position.z);
                    sp.SetKnot(i, k);
                    yFixed++; d = true;
                }
            }
            if (d) EditorUtility.SetDirty(sc);
        }

        if (snapped > 0 || pruned > 0 || blended > 0 || yFixed > 0)
        {
            EditorSceneManager.MarkAllScenesDirty();
            EditorUtility.SetDirty(reg);
        }
        Debug.Log($"[SnapRoad] Snapped {snapped} | Pruned {pruned} | " +
                  $"Hermite-blended {blended} knots | Y=0 fixed {yFixed} | Tight {skipped}");
    }

    // ── Position snap: last knot ─────────────────────────────────────────
    static void SnapLastKnot(SplineContainer sc, Vector3 tgt,
                              ref int snapped, ref int pruned, ref int skipped)
    {
        var sp = sc.Spline; var tf = sc.transform; bool d = false;
        while (sp.Count > 2)
        {
            Vector3 wL  = tf.TransformPoint((Vector3)sp[sp.Count - 1].Position);
            Vector3 wP  = tf.TransformPoint((Vector3)sp[sp.Count - 2].Position);
            Vector3 dir = wL - wP;
            if (dir.sqrMagnitude < 1e-6f) { sp.RemoveAt(sp.Count - 1); pruned++; d = true; continue; }
            if (Vector3.Dot(tgt - wL, dir.normalized) < 0f) { sp.RemoveAt(sp.Count - 1); pruned++; d = true; }
            else break;
        }
        var k = sp[sp.Count - 1];
        float dist = Vector3.Distance(tf.TransformPoint((Vector3)k.Position), tgt);
        if (dist <= kSnapEpsilon) skipped++;
        else { k.Position = (float3)tf.InverseTransformPoint(tgt); sp.SetKnot(sp.Count - 1, k); d = true; snapped++; }
        if (d) EditorUtility.SetDirty(sc);
    }

    // ── Position snap: first knot ────────────────────────────────────────
    static void SnapFirstKnot(SplineContainer sc, Vector3 tgt,
                               ref int snapped, ref int pruned, ref int skipped)
    {
        var sp = sc.Spline; var tf = sc.transform; bool d = false;
        while (sp.Count > 2)
        {
            Vector3 wF  = tf.TransformPoint((Vector3)sp[0].Position);
            Vector3 wS  = tf.TransformPoint((Vector3)sp[1].Position);
            Vector3 dir = wF - wS;
            if (dir.sqrMagnitude < 1e-6f) { sp.RemoveAt(0); pruned++; d = true; continue; }
            if (Vector3.Dot(tgt - wF, dir.normalized) < 0f) { sp.RemoveAt(0); pruned++; d = true; }
            else break;
        }
        var k = sp[0];
        float dist = Vector3.Distance(tf.TransformPoint((Vector3)k.Position), tgt);
        if (dist <= kSnapEpsilon) skipped++;
        else { k.Position = (float3)tf.InverseTransformPoint(tgt); sp.SetKnot(0, k); d = true; snapped++; }
        if (d) EditorUtility.SetDirty(sc);
    }

    // ── Align endpoint + Hermite blend: approach (road end → intersection) ─
    static void AlignAndBlendEnd(SplineContainer sc,
                                  Vector3 junctionW, Vector3 intK1W,
                                  ref int blended)
    {
        var sp = sc.Spline; var tf = sc.transform;
        int last = sp.Count - 1;
        if (last < 1) return;

        // Endpoint tangent: align with intersection K0 → K1 direction
        Vector3 fwdW  = (intK1W - junctionW).normalized;
        Vector3 fwdL  = tf.InverseTransformDirection(fwdW).normalized;
        float   endSeg = Vector3.Distance((Vector3)sp[last].Position, (Vector3)sp[last - 1].Position);
        float   endMag = Mathf.Max(endSeg / 3f, 0.001f);
        var kEnd = sp[last];
        kEnd.Rotation   = quaternion.identity;
        kEnd.TangentOut = (float3)( fwdL * endMag);
        kEnd.TangentIn  = (float3)(-fwdL * endMag);
        sp.SetKnot(last, kEnd);
        blended++;

        // Hermite blend: move intermediate knots in the last kBlendFraction of arc
        float[] arc    = ArcFromStart(sp);
        float   total  = arc[last];
        float   bLen   = total * kBlendFraction;

        // Find blend-start = last knot outside the blend zone
        int bStart = 0;
        for (int i = last - 1; i >= 0; i--)
        {
            if (total - arc[i] >= bLen) { bStart = i; break; }
        }
        if (bStart >= last - 1) { EditorUtility.SetDirty(sc); return; }

        // Anchors: p0 at bStart (untouched), p1 at last (junction)
        Vector3 p0 = (Vector3)sp[bStart].Position;
        // V0: original travel direction at bStart, scaled by blend-zone length
        Vector3 rawV0 = (Vector3)((Quaternion)sp[bStart].Rotation * (float3)sp[bStart].TangentOut);
        Vector3 v0 = (rawV0.sqrMagnitude > 1e-8f ? rawV0.normalized : ((Vector3)sp[bStart + 1].Position - p0).normalized) * bLen;

        Vector3 p1 = (Vector3)sp[last].Position;
        Vector3 v1 = fwdL * bLen;   // junction entry direction, same scale

        float kMag = bLen / Mathf.Max(last - bStart - 1, 1) / 3f;

        for (int i = bStart + 1; i < last; i++)
        {
            float t = Mathf.Clamp01((arc[i] - arc[bStart]) / bLen);

            Vector3 newPos = HPosn(t, p0, v0, p1, v1);
            Vector3 newTan = HTang(t, p0, v0, p1, v1).normalized;
            float   mag    = Mathf.Max(kMag, 0.001f);

            var k = sp[i];
            k.Position   = (float3)newPos;
            k.Rotation   = quaternion.identity;
            k.TangentOut = (float3)( newTan * mag);
            k.TangentIn  = (float3)(-newTan * mag);
            sp.SetKnot(i, k);
            blended++;
        }
        EditorUtility.SetDirty(sc);
    }

    // ── Align endpoint + Hermite blend: departure (intersection → road start) ─
    static void AlignAndBlendStart(SplineContainer sc,
                                    Vector3 exitDirW,
                                    ref int blended)
    {
        var sp = sc.Spline; var tf = sc.transform;
        if (sp.Count < 2) return;

        // Endpoint tangent: align with intersection exit direction
        Vector3 fwdL  = tf.InverseTransformDirection(exitDirW).normalized;
        float   seg   = Vector3.Distance((Vector3)sp[0].Position, (Vector3)sp[1].Position);
        float   mag0  = Mathf.Max(seg / 3f, 0.001f);
        var kStart = sp[0];
        kStart.Rotation   = quaternion.identity;
        kStart.TangentOut = (float3)( fwdL * mag0);
        kStart.TangentIn  = (float3)(-fwdL * mag0);
        sp.SetKnot(0, kStart);
        blended++;

        // Hermite blend: move intermediate knots in the first kBlendFraction of arc
        int     n     = sp.Count;
        float[] arc   = ArcFromStart(sp);
        float   total = arc[n - 1];
        float   bLen  = total * kBlendFraction;

        // Find blend-end = first knot at or beyond bLen from K[0]
        int bEnd = n - 1;
        for (int i = 1; i < n; i++) { if (arc[i] >= bLen) { bEnd = i; break; } }
        if (bEnd <= 1) { EditorUtility.SetDirty(sc); return; }

        Vector3 p0 = (Vector3)sp[0].Position;
        Vector3 v0 = fwdL * bLen;

        Vector3 p1 = (Vector3)sp[bEnd].Position;
        Vector3 rawV1 = (Vector3)((Quaternion)sp[bEnd].Rotation * (float3)sp[bEnd].TangentOut);
        Vector3 v1 = (rawV1.sqrMagnitude > 1e-8f ? rawV1.normalized : ((Vector3)sp[bEnd].Position - (Vector3)sp[bEnd - 1].Position).normalized) * bLen;

        float kMag = bLen / Mathf.Max(bEnd - 1, 1) / 3f;

        for (int i = 1; i < bEnd; i++)
        {
            float t = Mathf.Clamp01(arc[i] / bLen);

            Vector3 newPos = HPosn(t, p0, v0, p1, v1);
            Vector3 newTan = HTang(t, p0, v0, p1, v1).normalized;
            float   mag    = Mathf.Max(kMag, 0.001f);

            var k = sp[i];
            k.Position   = (float3)newPos;
            k.Rotation   = quaternion.identity;
            k.TangentOut = (float3)( newTan * mag);
            k.TangentIn  = (float3)(-newTan * mag);
            sp.SetKnot(i, k);
            blended++;
        }
        EditorUtility.SetDirty(sc);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    static float[] ArcFromStart(Spline sp)
    {
        int n = sp.Count;
        var a = new float[n];
        for (int i = 1; i < n; i++)
            a[i] = a[i - 1] + Vector3.Distance((Vector3)sp[i].Position, (Vector3)sp[i - 1].Position);
        return a;
    }

    // Cubic Hermite position  H(t) with endpoint tangents m0, m1
    static Vector3 HPosn(float t, Vector3 p0, Vector3 m0, Vector3 p1, Vector3 m1)
    {
        float t2 = t * t, t3 = t2 * t;
        return ( 2*t3 - 3*t2 + 1)*p0 + (t3 - 2*t2 + t)*m0
             + (-2*t3 + 3*t2    )*p1 + (t3 -   t2    )*m1;
    }

    // Cubic Hermite tangent  dH/dt
    static Vector3 HTang(float t, Vector3 p0, Vector3 m0, Vector3 p1, Vector3 m1)
    {
        float t2 = t * t;
        return ( 6*t2 - 6*t    )*p0 + (3*t2 - 4*t + 1)*m0
             + (-6*t2 + 6*t    )*p1 + (3*t2 - 2*t    )*m1;
    }
}

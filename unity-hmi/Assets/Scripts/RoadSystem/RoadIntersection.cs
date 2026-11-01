using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace RoadSystem
{
    [Serializable]
    public class IntersectionArm
    {
        public RoadSegment road;
        public float connectionT;
        public bool isEndpoint;

        // Recomputed on every RebuildMesh — never persist these.
        // Without [NonSerialized] each rebuild marks the scene dirty.
        [NonSerialized] public Vector3 leftEdge;
        [NonSerialized] public Vector3 rightEdge;
        [NonSerialized] public Vector3 center;
        [NonSerialized] public float angle;
    }

    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class RoadIntersection : MonoBehaviour
    {
        [SerializeField] List<IntersectionArm> m_Arms = new();

        [Header("Override")]
        [Tooltip("When enabled, global changes from RoadNetworkManager won't affect this intersection.")]
        [SerializeField] bool m_OverrideCurvature;

        [Header("Corner Arc")]
        [Tooltip("-1 = max inward, 0 = straight cut, 1 = max outward (nearly right angle)")]
        [SerializeField, Range(-1f, 1f)] float m_CornerCurvature = -0.5f;
        [SerializeField, Range(2, 12)] int m_CornerSegments = 6;

        [Header("Per-Corner Curvature")]
        [Tooltip("Override curvature for each corner individually. Auto-sized to match corner count.")]
        [SerializeField] List<float> m_PerCornerCurvature = new();

        [Header("Texture")]
        [SerializeField, Min(0.01f)] float m_TextureScale = 0.2f;

        [Header("Render")]
        [Tooltip("Lift the intersection mesh in world Y to avoid z-fighting with road meshes.")]
        [SerializeField, Range(0f, 0.1f)] float m_YOffset = 0.005f;

        Mesh m_Mesh;

        // Cached lists to avoid GC allocations on every rebuild
        readonly List<EdgePoint> m_EdgePts = new();
        readonly List<Vector3> m_Perimeter = new();
        readonly List<Vector3> m_LocalVerts = new();
        readonly List<Vector3> m_Vertices = new();
        readonly List<Vector3> m_NormalsList = new();
        readonly List<Vector2> m_UVs = new();
        readonly List<int> m_Indices = new();

        public List<IntersectionArm> Arms => m_Arms;

        public float CornerCurvature
        {
            get => m_CornerCurvature;
            set => m_CornerCurvature = Mathf.Clamp(value, -1f, 1f);
        }

        public int CornerSegments
        {
            get => m_CornerSegments;
            set => m_CornerSegments = Mathf.Clamp(value, 2, 12);
        }

        public float TextureScale
        {
            get => m_TextureScale;
            set => m_TextureScale = Mathf.Max(0.01f, value);
        }

        public float YOffset
        {
            get => m_YOffset;
            set => m_YOffset = Mathf.Clamp(value, 0f, 0.1f);
        }

        public bool OverrideCurvature
        {
            get => m_OverrideCurvature;
            set => m_OverrideCurvature = value;
        }

        public List<float> PerCornerCurvature => m_PerCornerCurvature;

        public void SetArms(List<IntersectionArm> arms)
        {
            m_Arms = arms;
            RebuildMesh();
        }

        void OnEnable()
        {
            if (m_Arms.Count > 0)
                RebuildMesh();
        }

        void OnDisable()
        {
            if (m_Mesh != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(m_Mesh);
#else
                Destroy(m_Mesh);
#endif
            }
        }

        // Draw a small pickable gizmo only when the mesh is empty.
        // When a real mesh exists the user can click it directly,
        // and we don't want to steal clicks from the road surface.
        void OnDrawGizmos()
        {
            if (m_Mesh != null && m_Mesh.vertexCount > 0) return;
            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, 1.5f);
        }

        struct EdgePoint
        {
            public Vector3 pos;
            public int armIndex;
            public float angle;
        }

        void EnsureMesh()
        {
            if (m_Mesh == null)
            {
                m_Mesh = new Mesh { name = "IntersectionMesh" };
                GetComponent<MeshFilter>().sharedMesh = m_Mesh;
            }
        }

        public void RebuildMesh()
        {
            EnsureMesh();
            m_Mesh.Clear();

            if (m_Arms == null || m_Arms.Count < 2)
                return;

            Vector3 center = transform.position;

            for (int i = 0; i < m_Arms.Count; i++)
            {
                var arm = m_Arms[i];
                if (arm.road == null) continue;
                arm.road.EvaluateRoadAt(arm.connectionT, out Vector3 c, out Vector3 l, out Vector3 r);
                arm.center = c;
                arm.leftEdge = l;
                arm.rightEdge = r;
                arm.angle = Mathf.Atan2(c.z - center.z, c.x - center.x);
            }

            BuildFillMesh(center);
        }

        void BuildFillMesh(Vector3 worldCenter)
        {
            m_EdgePts.Clear();

            for (int i = 0; i < m_Arms.Count; i++)
            {
                var arm = m_Arms[i];
                if (arm.road == null) continue;
                m_EdgePts.Add(new EdgePoint
                {
                    pos = arm.leftEdge, armIndex = i,
                    angle = Mathf.Atan2(arm.leftEdge.z - worldCenter.z,
                                        arm.leftEdge.x - worldCenter.x)
                });
                m_EdgePts.Add(new EdgePoint
                {
                    pos = arm.rightEdge, armIndex = i,
                    angle = Mathf.Atan2(arm.rightEdge.z - worldCenter.z,
                                        arm.rightEdge.x - worldCenter.x)
                });
            }

            if (m_EdgePts.Count < 3) return;
            m_EdgePts.Sort(CompareEdgeByAngleDesc);
            m_Perimeter.Clear();

            int count = m_EdgePts.Count;

            // Pre-count corners to sync per-corner curvature list.
            // Through-road gaps (same road split into two virtual arms,
            // e.g., the side without a branch on a T-intersection) don't
            // count as corners — they stay as straight road edges.
            int totalCorners = 0;
            for (int i = 0; i < count; i++)
            {
                int a = m_EdgePts[i].armIndex;
                int b = m_EdgePts[(i + 1) % count].armIndex;
                if (a != b && !IsThroughRoadGap(a, b))
                    totalCorners++;
            }
            SyncPerCornerList(totalCorners);

            int cornerIdx = 0;
            for (int i = 0; i < count; i++)
            {
                var curr = m_EdgePts[i];
                var next = m_EdgePts[(i + 1) % count];

                m_Perimeter.Add(curr.pos);

                if (curr.armIndex == next.armIndex) continue;
                if (IsThroughRoadGap(curr.armIndex, next.armIndex)) continue;

                int ci = cornerIdx++;

                float dist = Vector3.Distance(curr.pos, next.pos);
                if (dist < 0.05f || m_CornerSegments < 2) continue;

                Vector3 P0 = curr.pos;
                Vector3 P2 = next.pos;
                Vector3 mid = (P0 + P2) * 0.5f;

                if (IsGapUnderRoad(mid, worldCenter)) continue;

                Vector3 perp0 = P0 - m_Arms[curr.armIndex].center;
                perp0.y = 0;
                float pLen0 = perp0.magnitude;
                if (pLen0 > 0.001f) perp0 /= pLen0; else perp0 = Vector3.right;

                Vector3 perp2 = P2 - m_Arms[next.armIndex].center;
                perp2.y = 0;
                float pLen2 = perp2.magnitude;
                if (pLen2 > 0.001f) perp2 /= pLen2; else perp2 = Vector3.forward;

                Vector3 outCorner = LineLineXZ(P0, perp0, P2, perp2);
                outCorner.y = worldCenter.y;

                Vector3 D0 = (m_Arms[curr.armIndex].center - worldCenter);
                D0.y = 0; D0 = D0.normalized;
                Vector3 D2 = (m_Arms[next.armIndex].center - worldCenter);
                D2.y = 0; D2 = D2.normalized;

                Vector3 inCorner = LineLineXZ(P0, D0, P2, D2);
                inCorner.y = worldCenter.y;

                if (!IsFinite(outCorner))
                {
                    Vector3 outDir = (mid - worldCenter);
                    outDir.y = 0;
                    float odLen = outDir.magnitude;
                    outDir = odLen > 0.001f ? outDir / odLen : Vector3.forward;
                    outCorner = mid + outDir * dist * 0.5f;
                }
                if (!IsFinite(inCorner))
                {
                    Vector3 inDir = (worldCenter - mid);
                    inDir.y = 0;
                    float idLen = inDir.magnitude;
                    inDir = idLen > 0.001f ? inDir / idLen : Vector3.back;
                    inCorner = mid + inDir * dist * 0.5f;
                }

                float maxOff = dist * 3f;
                if (Vector3.Distance(outCorner, mid) > maxOff)
                    outCorner = mid + (outCorner - mid).normalized * maxOff;
                if (Vector3.Distance(inCorner, mid) > maxOff)
                    inCorner = mid + (inCorner - mid).normalized * maxOff;

                float effectiveCurvature = m_PerCornerCurvature[ci];

                // For negative curvature, attenuate in narrow angular gaps
                // to prevent the Bezier curve from crossing the fan center
                if (effectiveCurvature < 0f)
                {
                    Vector3 toP0 = P0 - worldCenter; toP0.y = 0;
                    Vector3 toP2 = P2 - worldCenter; toP2.y = 0;
                    float mag0 = toP0.magnitude;
                    float mag2 = toP2.magnitude;
                    if (mag0 > 0.001f && mag2 > 0.001f)
                    {
                        float cosGap = Vector3.Dot(toP0 / mag0, toP2 / mag2);
                        float gapAngle = Mathf.Acos(Mathf.Clamp(cosGap, -1f, 1f));
                        const float kThreshold = Mathf.PI * 0.39f; // ~70°
                        if (gapAngle < kThreshold)
                            effectiveCurvature *= gapAngle / kThreshold;
                    }
                }

                Vector3 controlPt;
                if (effectiveCurvature >= 0f)
                    controlPt = Vector3.Lerp(mid, outCorner, effectiveCurvature);
                else
                    controlPt = Vector3.Lerp(mid, inCorner, -effectiveCurvature);

                // Safety check: ensure the actual Bezier curve stays clear of
                // worldCenter so the triangle fan from center to perimeter doesn't
                // fold over itself. Sample the curve and fall back to a straight
                // cut (controlPt = mid) only if any sample gets dangerously close
                // to the center. This is correct for both inner concave corners
                // and outer convex corners — the latter need controlPt on the
                // opposite side of center to bulge outward, but the resulting
                // Bezier samples still stay far from center, so they pass.
                {
                    Vector3 cToMidVec = mid - worldCenter; cToMidVec.y = 0;
                    float cToMidLen = cToMidVec.magnitude;
                    if (cToMidLen > 0.001f)
                    {
                        const int kSafetySamples = 8;
                        float minBezDistSq = float.MaxValue;
                        for (int s = 1; s < kSafetySamples; s++)
                        {
                            float st = (float)s / kSafetySamples;
                            float su = 1f - st;
                            Vector3 bp = su * su * P0 + 2f * su * st * controlPt + st * st * P2;
                            Vector3 d = bp - worldCenter; d.y = 0;
                            float dSq = d.sqrMagnitude;
                            if (dSq < minBezDistSq) minBezDistSq = dSq;
                        }
                        float minSafe = Mathf.Max(0.05f, cToMidLen * 0.15f);
                        if (minBezDistSq < minSafe * minSafe)
                            controlPt = mid;
                    }
                }

                for (int j = 1; j < m_CornerSegments; j++)
                {
                    float t = (float)j / m_CornerSegments;
                    float u = 1f - t;
                    m_Perimeter.Add(u * u * P0 + 2f * u * t * controlPt + t * t * P2);
                }
            }

            if (m_Perimeter.Count < 3) return;

            // Lift all mesh vertices uniformly in world Y so the intersection
            // renders above the road surface and avoids z-fighting.
            Vector3 yLift = Vector3.up * m_YOffset;
            Vector3 localCenter = transform.InverseTransformPoint(worldCenter + yLift);
            m_LocalVerts.Clear();
            for (int i = 0; i < m_Perimeter.Count; i++)
                m_LocalVerts.Add(transform.InverseTransformPoint(m_Perimeter[i] + yLift));

            m_Vertices.Clear();
            m_NormalsList.Clear();
            m_UVs.Clear();
            m_Indices.Clear();

            m_Vertices.Add(localCenter);
            m_NormalsList.Add(Vector3.up);
            m_UVs.Add(new Vector2(worldCenter.x * m_TextureScale,
                                   worldCenter.z * m_TextureScale));

            for (int i = 0; i < m_LocalVerts.Count; i++)
            {
                m_Vertices.Add(m_LocalVerts[i]);
                m_NormalsList.Add(Vector3.up);
                Vector3 wp = m_Perimeter[i];
                m_UVs.Add(new Vector2(wp.x * m_TextureScale,
                                       wp.z * m_TextureScale));
            }

            for (int i = 0; i < m_LocalVerts.Count; i++)
            {
                int nxt = (i + 1) % m_LocalVerts.Count;
                m_Indices.Add(0);
                m_Indices.Add(i + 1);
                m_Indices.Add(nxt + 1);
            }

            m_Mesh.SetVertices(m_Vertices);
            m_Mesh.SetNormals(m_NormalsList);
            m_Mesh.SetUVs(0, m_UVs);
            m_Mesh.SetIndices(m_Indices, MeshTopology.Triangles, 0);
            m_Mesh.RecalculateBounds();
        }

        void SyncPerCornerList(int cornerCount)
        {
            while (m_PerCornerCurvature.Count < cornerCount)
                m_PerCornerCurvature.Add(m_CornerCurvature);
            if (m_PerCornerCurvature.Count > cornerCount)
                m_PerCornerCurvature.RemoveRange(cornerCount, m_PerCornerCurvature.Count - cornerCount);
        }

        /// <summary>
        /// 2D (XZ) line-line intersection. Returns float.PositiveInfinity components on parallel lines.
        /// </summary>
        static Vector3 LineLineXZ(Vector3 P0, Vector3 D0, Vector3 P2, Vector3 D2)
        {
            float det = D0.x * (-D2.z) - D0.z * (-D2.x);
            if (Mathf.Abs(det) < 0.0001f)
                return new Vector3(float.PositiveInfinity, 0, float.PositiveInfinity);
            float t = ((P2.x - P0.x) * (-D2.z) - (P2.z - P0.z) * (-D2.x)) / det;
            return P0 + D0 * t;
        }

        /// <summary>
        /// Returns true when the gap between two arms is a straight
        /// continuation of a road and should NOT receive a corner arc.
        /// Two cases qualify:
        ///   1. Both arms are virtual halves of the same through-road
        ///      (mid-body connection split into two non-endpoint arms).
        ///   2. Both arms are endpoints of two distinct roads that meet
        ///      the intersection collinearly — e.g., a T-junction whose
        ///      "through" road is built from two separate road segments
        ///      meeting end-to-end. The back side of such a T has no
        ///      branch and should remain a straight road edge.
        /// </summary>
        bool IsThroughRoadGap(int armA, int armB)
        {
            if (armA == armB) return false;
            if (armA < 0 || armB < 0) return false;
            if (armA >= m_Arms.Count || armB >= m_Arms.Count) return false;

            var a = m_Arms[armA];
            var b = m_Arms[armB];
            if (a.road == null || b.road == null) return false;

            // Case 1: same road, mid-body split.
            if (a.road == b.road && !a.isEndpoint && !b.isEndpoint)
                return true;

            // Case 2: two distinct road endpoints meeting collinearly.
            // arm.angle is the heading from the intersection center to
            // arm.center, so two collinear continuations differ by ~180°.
            if (a.isEndpoint && b.isEndpoint && a.road != b.road)
            {
                float deltaDeg = Mathf.Abs(Mathf.DeltaAngle(
                    a.angle * Mathf.Rad2Deg, b.angle * Mathf.Rad2Deg));
                const float kCollinearWindowDeg = 20f;
                if (Mathf.Abs(deltaDeg - 180f) < kCollinearWindowDeg)
                    return true;
            }

            return false;
        }

        bool IsGapUnderRoad(Vector3 gapMid, Vector3 worldCenter)
        {
            float midAngle = Mathf.Atan2(gapMid.z - worldCenter.z,
                                          gapMid.x - worldCenter.x) * Mathf.Rad2Deg;

            for (int a = 0; a < m_Arms.Count; a++)
            {
                if (m_Arms[a].road == null) continue;

                float armAngle = m_Arms[a].angle * Mathf.Rad2Deg;
                float leftAngle = Mathf.Atan2(m_Arms[a].leftEdge.z - worldCenter.z,
                                               m_Arms[a].leftEdge.x - worldCenter.x) * Mathf.Rad2Deg;
                float rightAngle = Mathf.Atan2(m_Arms[a].rightEdge.z - worldCenter.z,
                                                m_Arms[a].rightEdge.x - worldCenter.x) * Mathf.Rad2Deg;
                float halfSpan = Mathf.Abs(Mathf.DeltaAngle(leftAngle, rightAngle)) * 0.5f;

                if (Mathf.Abs(Mathf.DeltaAngle(midAngle, armAngle)) < halfSpan)
                    return true;
            }
            return false;
        }

        static int CompareEdgeByAngleDesc(EdgePoint a, EdgePoint b)
        {
            return b.angle.CompareTo(a.angle);
        }

        static bool IsFinite(Vector3 v)
        {
            return !float.IsInfinity(v.x) && !float.IsNaN(v.x)
                && !float.IsInfinity(v.z) && !float.IsNaN(v.z);
        }
    }
}

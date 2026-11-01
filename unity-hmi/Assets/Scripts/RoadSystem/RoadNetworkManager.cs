#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Splines;
#endif

using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace RoadSystem
{
    [ExecuteInEditMode]
    public class RoadNetworkManager : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] float m_SnapDistance = 2f;
        [SerializeField] Material m_RoadMaterial;
        [Tooltip("If set, intersections use this material instead of the road material.")]
        [SerializeField] Material m_IntersectionMaterial;

        [Header("Road Mesh")]
        [Tooltip("Global density. Pushed to all RoadSegments without 'Override Segments Per Meter'.")]
        [SerializeField, Range(0.1f, 1.5f)] float m_RoadSegmentsPerMeter = 0.5f;

        [Header("Intersection Corners")]
        [Tooltip("Extra distance to trim roads back from the intersection, creating space for corner arcs.")]
        [SerializeField, Min(0f)] float m_CornerPullback = 0.5f;
        [Tooltip("When OFF, lane splines under each RoadSegment ignore the corner pullback / width " +
                 "trim and keep their full untrimmed range. The visible road mesh is still trimmed. " +
                 "Useful when downstream driving logic needs lane spline endpoints close to the " +
                 "intersection's internal splines (within their connection tolerance).")]
        [SerializeField] bool m_TrimLaneSplines = true;
        [Tooltip("Arc shape: -1 = max inward, 0 = straight cut, 1 = max outward (nearly right angle).")]
        [SerializeField, Range(-1f, 1f)] float m_CornerCurvature = -0.5f;
        [SerializeField, Range(2, 12)] int m_CornerSegments = 6;
        [SerializeField, Min(0.01f)] float m_IntersectionTextureScale = 0.2f;
        [Tooltip("Lift intersections in world Y to render them above the road and avoid z-fighting.")]
        [SerializeField, Range(0f, 0.1f)] float m_IntersectionYOffset = 0.005f;

        [Header("Runtime")]
        [SerializeField, HideInInspector] List<RoadIntersection> m_Intersections = new();

        public float SnapDistance => m_SnapDistance;
        public Material RoadMaterial => m_RoadMaterial;
        public bool TrimLaneSplines => m_TrimLaneSplines;

        bool m_RebuildScheduled;

        // Cached set of child splines for fast lookup in event callbacks.
        // Rebuilt on every full rebuild so it stays in sync with the hierarchy.
        readonly HashSet<Spline> m_ChildSplines = new();

        // ───────────────── lifecycle & auto-rebuild ─────────────────

        void OnEnable()
        {
            RefreshChildSplineCache();
            Spline.Changed += OnAnySplineChanged;
#if UNITY_EDITOR
            EditorSplineUtility.AfterSplineWasModified += OnAnySplineModified;
            Undo.undoRedoPerformed += ScheduleRebuild;
#endif
        }

        void OnDisable()
        {
            Spline.Changed -= OnAnySplineChanged;
#if UNITY_EDITOR
            EditorSplineUtility.AfterSplineWasModified -= OnAnySplineModified;
            Undo.undoRedoPerformed -= ScheduleRebuild;
#endif
            m_ChildSplines.Clear();
        }

        void OnTransformChildrenChanged()
        {
            RefreshChildSplineCache();
            ScheduleRebuild();
        }

        void RefreshChildSplineCache()
        {
            m_ChildSplines.Clear();
            var roads = GetComponentsInChildren<RoadSegment>();
            foreach (var r in roads)
            {
                var s = r.Spline;
                if (s != null) m_ChildSplines.Add(s);
            }
        }

        void OnAnySplineChanged(Spline s, int k, SplineModification mod)
        {
            if (m_ChildSplines.Contains(s)) ScheduleRebuild();
        }

#if UNITY_EDITOR
        void OnAnySplineModified(Spline s)
        {
            if (m_ChildSplines.Contains(s)) ScheduleRebuild();
        }
#endif

        public void ScheduleRebuild()
        {
            if (m_RebuildScheduled) return;
            m_RebuildScheduled = true;
#if UNITY_EDITOR
            EditorApplication.delayCall += DeferredRebuild;
#endif
        }

        void DeferredRebuild()
        {
            m_RebuildScheduled = false;
            if (this == null) return;

            // Auto-rebuilds (from spline edits, undo, hierarchy changes)
            // only refresh meshes — they DO NOT destroy and recreate
            // intersection GameObjects. Recreating breaks user selection
            // and any external references. The user must click
            // "Detect Intersections" explicitly to update topology.
            SyncGlobalSettingsToIntersections();
            SyncGlobalSettingsToRoads();
            RebuildAllMeshes();
        }

        // ─────────────────────── public API ───────────────────────

        public void RebuildAll()
        {
            var roads = GetComponentsInChildren<RoadSegment>();

            foreach (var r in roads)
            {
                r.startTrim = 0f;
                r.endTrim = 1f;
                if (!r.OverrideSegmentsPerMeter)
                    r.SegmentsPerMeter = m_RoadSegmentsPerMeter;
            }

            DetectAndBuildIntersections(roads);
            SyncGlobalSettingsToIntersections();

            foreach (var r in roads)
                r.RebuildMesh();

            foreach (var ix in m_Intersections)
                if (ix != null) ix.RebuildMesh();

            RefreshChildSplineCache();
        }

        /// <summary>
        /// Push global curvature/segments/textureScale to all non-overridden
        /// intersections, then rebuild their meshes. Use this when only
        /// visual parameters changed and topology detection can be skipped.
        /// </summary>
        public void SyncGlobalSettingsToIntersections()
        {
            foreach (var ix in m_Intersections)
            {
                if (ix == null) continue;

                // Y offset is always applied — it's a global render-order
                // setting independent of curvature override.
                ix.YOffset = m_IntersectionYOffset;

                if (ix.OverrideCurvature) continue;
                ix.CornerCurvature = m_CornerCurvature;
                ix.CornerSegments = m_CornerSegments;
                ix.TextureScale = m_IntersectionTextureScale;
                var pc = ix.PerCornerCurvature;
                for (int i = 0; i < pc.Count; i++)
                    pc[i] = m_CornerCurvature;
            }
        }

        /// <summary>
        /// Push global SegmentsPerMeter to all RoadSegments without override.
        /// </summary>
        public void SyncGlobalSettingsToRoads()
        {
            var roads = GetComponentsInChildren<RoadSegment>();
            foreach (var r in roads)
            {
                if (r.OverrideSegmentsPerMeter) continue;
                r.SegmentsPerMeter = m_RoadSegmentsPerMeter;
            }
        }

        /// <summary>
        /// Rebuild all road and intersection meshes without re-detecting topology.
        /// </summary>
        public void RebuildAllMeshes()
        {
            var roads = GetComponentsInChildren<RoadSegment>();
            foreach (var r in roads) r.RebuildMesh();
            foreach (var ix in m_Intersections)
                if (ix != null) ix.RebuildMesh();
        }

        public void DetectAndBuildIntersections()
        {
            DetectAndBuildIntersections(GetComponentsInChildren<RoadSegment>());
        }

        struct SavedOverrideData
        {
            public Vector3 position;
            public float curvature;
            public int segments;
            public List<float> perCornerCurvature;
            public float textureScale;
        }

        void DetectAndBuildIntersections(RoadSegment[] roads)
        {
            // Save override data before destroying intersections
            var savedOverrides = new List<SavedOverrideData>();
            foreach (var ix in m_Intersections)
            {
                if (ix == null || !ix.OverrideCurvature) continue;
                savedOverrides.Add(new SavedOverrideData
                {
                    position = ix.transform.position,
                    curvature = ix.CornerCurvature,
                    segments = ix.CornerSegments,
                    perCornerCurvature = new List<float>(ix.PerCornerCurvature),
                    textureScale = ix.TextureScale,
                });
            }

            ClearIntersections(roads);

            if (roads.Length < 2) return;

            var endpoints = new List<(RoadSegment road, float t, Vector3 pos)>();
            foreach (var road in roads)
            {
                if (road.Spline == null || road.Spline.Count < 2) continue;
                road.EvaluateRoadAt(0f, out Vector3 startC, out _, out _);
                road.EvaluateRoadAt(1f, out Vector3 endC, out _, out _);
                endpoints.Add((road, 0f, startC));
                endpoints.Add((road, 1f, endC));
            }

            var used = new bool[endpoints.Count];
            var clusters = new List<List<(RoadSegment road, float t, Vector3 pos)>>();

            for (int i = 0; i < endpoints.Count; i++)
            {
                if (used[i]) continue;
                var cluster = new List<(RoadSegment road, float t, Vector3 pos)> { endpoints[i] };
                var clusterIndices = new List<int>();
                for (int j = i + 1; j < endpoints.Count; j++)
                {
                    if (used[j]) continue;
                    if (Vector3.Distance(endpoints[i].pos, endpoints[j].pos) < m_SnapDistance)
                    {
                        cluster.Add(endpoints[j]);
                        clusterIndices.Add(j);
                    }
                }
                if (cluster.Count >= 2)
                {
                    clusters.Add(cluster);
                    used[i] = true;
                    foreach (var idx in clusterIndices)
                        used[idx] = true;
                }
            }

            for (int i = 0; i < endpoints.Count; i++)
            {
                if (used[i]) continue;
                var ep = endpoints[i];
                foreach (var road in roads)
                {
                    if (road == ep.road) continue;
                    if (road.Spline == null) continue;

                    SplineUtility.GetNearestPoint(
                        road.Spline,
                        (float3)road.transform.InverseTransformPoint(ep.pos),
                        out float3 nearestLocal, out float nearestT);

                    Vector3 nearestWorld = road.transform.TransformPoint((Vector3)nearestLocal);
                    float dist = Vector3.Distance(ep.pos, nearestWorld);
                    if (nearestT < 0.02f || nearestT > 0.98f) continue;
                    float surfaceThreshold = (ep.road.Width + road.Width) * 0.5f + m_SnapDistance;
                    if (dist < surfaceThreshold)
                    {
                        used[i] = true;
                        var cluster = new List<(RoadSegment road, float t, Vector3 pos)>
                        {
                            ep,
                            (road, nearestT, nearestWorld)
                        };
                        for (int k = 0; k < endpoints.Count; k++)
                        {
                            if (used[k]) continue;
                            float kThreshold = (endpoints[k].road.Width + road.Width) * 0.5f + m_SnapDistance;
                            if (Vector3.Distance(endpoints[k].pos, nearestWorld) < kThreshold)
                            {
                                cluster.Add(endpoints[k]);
                                used[k] = true;
                            }
                        }
                        clusters.Add(cluster);
                        break;
                    }
                }
            }

            // Phase 3: Body-body crossing — find where two road splines
            // come within snap distance of each other in their mid-sections
            DetectBodyBodyCrossings(roads, clusters);

            foreach (var cluster in clusters)
            {
                if (IsCollinearJunction(cluster))
                    continue;

                Vector3 center = Vector3.zero;
                foreach (var c in cluster) center += c.pos;
                center /= cluster.Count;

                foreach (var c in cluster)
                    ApplyTrim(c.road, c.t);

                var arms = new List<IntersectionArm>();
                foreach (var c in cluster)
                {
                    bool isEp = c.t < 0.01f || c.t > 0.99f;
                    if (!isEp)
                    {
                        float offsetT = GetTrimOffsetT(c.road, c.t);
                        arms.Add(new IntersectionArm
                        {
                            road = c.road,
                            connectionT = Mathf.Max(0.001f, c.t - offsetT),
                            isEndpoint = false
                        });
                        arms.Add(new IntersectionArm
                        {
                            road = c.road,
                            connectionT = Mathf.Min(0.999f, c.t + offsetT),
                            isEndpoint = false
                        });
                    }
                    else
                    {
                        float trimmedT = c.t < 0.5f ? c.road.startTrim : c.road.endTrim;
                        arms.Add(new IntersectionArm
                        {
                            road = c.road,
                            connectionT = trimmedT,
                            isEndpoint = true
                        });
                    }
                }

                CreateIntersectionObject(center, arms);
            }

            // Restore override data to matching intersections
            foreach (var so in savedOverrides)
            {
                RoadIntersection best = null;
                float bestDist = float.MaxValue;
                foreach (var ix in m_Intersections)
                {
                    if (ix == null) continue;
                    float d = Vector3.Distance(ix.transform.position, so.position);
                    if (d < bestDist) { bestDist = d; best = ix; }
                }
                if (best != null && bestDist < m_SnapDistance)
                {
                    best.OverrideCurvature = true;
                    best.CornerCurvature = so.curvature;
                    best.CornerSegments = so.segments;
                    best.TextureScale = so.textureScale;
                    var pc = best.PerCornerCurvature;
                    int count = Mathf.Min(pc.Count, so.perCornerCurvature.Count);
                    for (int i = 0; i < count; i++)
                        pc[i] = so.perCornerCurvature[i];
                    best.RebuildMesh();
                }
            }
        }

        public void ClearIntersections()
        {
            ClearIntersections(GetComponentsInChildren<RoadSegment>());
        }

        void ClearIntersections(RoadSegment[] roads)
        {
            // Destroy any RoadIntersection in the hierarchy, not just the
            // ones tracked in m_Intersections. The list can fall out of sync
            // after domain reloads, scene reopens, or bake/unbake cycles,
            // leaving orphan intersections that the user can't get rid of.
            var allInChildren = GetComponentsInChildren<RoadIntersection>(true);
            foreach (var ix in allInChildren)
            {
                if (ix == null) continue;
#if UNITY_EDITOR
                Undo.DestroyObjectImmediate(ix.gameObject);
#else
                DestroyImmediate(ix.gameObject);
#endif
            }
            m_Intersections.Clear();

            foreach (var r in roads)
            {
                r.startTrim = 0f;
                r.endTrim = 1f;
            }

            // Destroying child intersections triggered OnTransformChildrenChanged,
            // which scheduled a DeferredRebuild that would re-create everything.
            // Cancel it so 'Clear' actually clears.
#if UNITY_EDITOR
            EditorApplication.delayCall -= DeferredRebuild;
#endif
            m_RebuildScheduled = false;
        }

        // ─────────────────────── internals ───────────────────────

        void DetectBodyBodyCrossings(RoadSegment[] roads,
            List<List<(RoadSegment road, float t, Vector3 pos)>> clusters)
        {
            // Build a set of road pairs already connected by earlier phases
            var connectedPairs = new HashSet<(int, int)>();
            foreach (var cl in clusters)
            {
                for (int x = 0; x < cl.Count; x++)
                {
                    for (int y = x + 1; y < cl.Count; y++)
                    {
                        int idxA = System.Array.IndexOf(roads, cl[x].road);
                        int idxB = System.Array.IndexOf(roads, cl[y].road);
                        if (idxA >= 0 && idxB >= 0)
                        {
                            int lo = Mathf.Min(idxA, idxB);
                            int hi = Mathf.Max(idxA, idxB);
                            connectedPairs.Add((lo, hi));
                        }
                    }
                }
            }

            for (int a = 0; a < roads.Length; a++)
            {
                var roadA = roads[a];
                if (roadA.Spline == null || roadA.Spline.Count < 2) continue;
                float lengthA = roadA.Spline.GetLength();
                if (lengthA < 0.1f) continue;

                for (int b = a + 1; b < roads.Length; b++)
                {
                    if (connectedPairs.Contains((a, b))) continue;

                    var roadB = roads[b];
                    if (roadB.Spline == null || roadB.Spline.Count < 2) continue;

                    int samples = Mathf.Max(8, Mathf.CeilToInt(lengthA * 0.5f));
                    float bestDist = float.MaxValue;
                    float bestTA = 0f, bestTB = 0f;
                    Vector3 bestPosA = Vector3.zero, bestPosB = Vector3.zero;

                    for (int s = 1; s < samples; s++)
                    {
                        float tA = (float)s / samples;
                        if (tA < 0.02f || tA > 0.98f) continue;

                        roadA.EvaluateRoadAt(tA, out Vector3 posA, out _, out _);

                        SplineUtility.GetNearestPoint(
                            roadB.Spline,
                            (float3)roadB.transform.InverseTransformPoint(posA),
                            out float3 nearLocal, out float tB);

                        if (tB < 0.02f || tB > 0.98f) continue;

                        Vector3 posB = roadB.transform.TransformPoint((Vector3)nearLocal);
                        float dist = Vector3.Distance(posA, posB);

                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestTA = tA;
                            bestTB = tB;
                            bestPosA = posA;
                            bestPosB = posB;
                        }
                    }

                    float bodyThreshold = (roadA.Width + roadB.Width) * 0.5f + m_SnapDistance;
                    if (bestDist > bodyThreshold) continue;

                    var crossing = new List<(RoadSegment road, float t, Vector3 pos)>
                    {
                        (roadA, bestTA, bestPosA),
                        (roadB, bestTB, bestPosB)
                    };

                    clusters.Add(crossing);
                }
            }
        }

        /// <summary>
        /// Returns true when the cluster is just two collinear road endpoints
        /// that continue in the same direction — no intersection needed.
        /// </summary>
        bool IsCollinearJunction(List<(RoadSegment road, float t, Vector3 pos)> cluster)
        {
            if (cluster.Count != 2) return false;

            var a = cluster[0];
            var b = cluster[1];

            bool aIsEndpoint = a.t < 0.01f || a.t > 0.99f;
            bool bIsEndpoint = b.t < 0.01f || b.t > 0.99f;
            if (!aIsEndpoint || !bIsEndpoint) return false;

            if (a.road.Spline == null || a.road.Spline.Count < 2) return false;
            if (b.road.Spline == null || b.road.Spline.Count < 2) return false;

            Vector3 dirA = GetRoadDirectionAtEndpoint(a.road, a.t);
            Vector3 dirB = GetRoadDirectionAtEndpoint(b.road, b.t);

            if (dirA.sqrMagnitude < 0.0001f || dirB.sqrMagnitude < 0.0001f)
                return false;

            float dot = Mathf.Abs(Vector3.Dot(dirA.normalized, dirB.normalized));
            const float collinearThreshold = 0.95f; // ~18° tolerance

            if (dot < collinearThreshold) return false;

            float widthRatio = Mathf.Min(a.road.Width, b.road.Width)
                             / Mathf.Max(a.road.Width, b.road.Width);

            return widthRatio > 0.8f;
        }

        Vector3 GetRoadDirectionAtEndpoint(RoadSegment road, float t)
        {
            // Sample slightly inward from the endpoint to get a valid tangent,
            // since SplineUtility.Evaluate can return zero at exact endpoints
            const float inset = 0.02f;
            float sampleT = t < 0.5f ? inset : 1f - inset;

            SplineUtility.Evaluate(road.Spline, sampleT, out _, out float3 dir, out _);

            if (math.lengthsq(dir) > 0.0001f)
                return road.transform.TransformDirection((Vector3)(float3)math.normalizesafe(dir));

            // Fallback: direction between first and last knot
            var spline = road.Spline;
            Vector3 k0 = road.transform.TransformPoint((Vector3)spline[0].Position);
            Vector3 k1 = road.transform.TransformPoint((Vector3)spline[spline.Count - 1].Position);
            return (k1 - k0).normalized;
        }

        float GetTrimOffsetT(RoadSegment road, float connectionT)
        {
            if (road.Spline == null) return 0.05f;
            float splineLength = road.Spline.GetLength();
            if (splineLength < 0.001f) return 0.05f;
            return (road.Width * 0.5f + m_CornerPullback) / splineLength;
        }

        void ApplyTrim(RoadSegment road, float connectionT)
        {
            if (road.Spline == null) return;
            float splineLength = road.Spline.GetLength();
            if (splineLength < 0.001f) return;

            float trimAmount = (road.Width * 0.5f + m_CornerPullback) / splineLength;

            if (connectionT < 0.05f)
                road.startTrim = Mathf.Max(road.startTrim, trimAmount);
            else if (connectionT > 0.95f)
                road.endTrim = Mathf.Min(road.endTrim, 1f - trimAmount);
        }

        void CreateIntersectionObject(Vector3 center, List<IntersectionArm> arms)
        {
            var go = new GameObject($"Intersection_{arms.Count}way_{m_Intersections.Count}");
            go.transform.SetParent(transform);
            go.transform.position = center;

#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(go, "Create Intersection");
#endif

            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m_IntersectionMaterial != null
                ? m_IntersectionMaterial
                : m_RoadMaterial;

            var intersection = go.GetComponent<RoadIntersection>();
            if (intersection == null) intersection = go.AddComponent<RoadIntersection>();
            intersection.CornerCurvature = m_CornerCurvature;
            intersection.CornerSegments = m_CornerSegments;
            intersection.TextureScale = m_IntersectionTextureScale;
            intersection.YOffset = m_IntersectionYOffset;
            intersection.SetArms(arms);

            m_Intersections.Add(intersection);
        }
    }
}

using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace RoadSystem
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SplineContainer), typeof(MeshFilter), typeof(MeshRenderer))]
    public class RoadSegment : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] float m_Width = 4f;
        [SerializeField, Range(0.1f, 1.5f)] float m_SegmentsPerMeter = 0.5f;
        [Tooltip("When enabled, RoadNetworkManager won't overwrite this road's segments-per-meter.")]
        [SerializeField] bool m_OverrideSegmentsPerMeter;

        [Tooltip("How many times the texture repeats along the entire road in the V direction. " +
                 "1 = the texture covers the whole road exactly once (V goes 0..1). " +
                 "2 = repeats twice over the whole road (V goes 0..2). " +
                 "Larger values = denser repeats. " +
                 "U (across the road) is always 0..1 regardless of this value.")]
        [SerializeField, Min(0.001f)] float m_Tiling = 1f;

        [Header("Lane Splines")]
        [Tooltip("Hard cap on the number of knots per lane spline (shared across all " +
                 "child LaneSplineMirror components on this road). 0 = no cap, lane " +
                 "knot count is driven entirely by SamplesPerMeter on each lane mirror.")]
        [SerializeField, Min(0)] int m_KnotSamples = 0;

        [Tooltip("Multiplier applied to the auto-computed Bezier tangent length at " +
                 "every lane knot (shared across all child LaneSplineMirror components " +
                 "on this road). 1 = chord-matched (analytic optimum for many-knot, " +
                 "small-angle approximation). Increase to round off Bezier joints on " +
                 "long sweeping arcs without adding more knots — try 1.1–1.5. " +
                 "Going much above ~2 starts producing S-shaped overshoot.")]
        [SerializeField, Range(0.5f, 4f)] float m_TangentScale = 1f;

        // t-range [0,1] — intersections can trim the rendered portion
        [HideInInspector] public float startTrim = 0f;
        [HideInInspector] public float endTrim = 1f;

        SplineContainer m_Container;
        Mesh m_Mesh;

        readonly List<Vector3> m_Positions = new();
        readonly List<Vector3> m_Normals = new();
        readonly List<Vector2> m_UVs = new();
        readonly List<int> m_Indices = new();

        // Reused across RebuildMesh to avoid GC allocs when collecting derived
        // splines (lanes + pedestrian paths).
        static readonly List<LaneSplineMirror> s_LaneMirrorBuffer = new();
        static readonly List<PedestrianSpline> s_PedestrianBuffer = new();

        public float Width => m_Width;

        public float SegmentsPerMeter
        {
            get => m_SegmentsPerMeter;
            set => m_SegmentsPerMeter = Mathf.Clamp(value, 0.1f, 1.5f);
        }

        public bool OverrideSegmentsPerMeter
        {
            get => m_OverrideSegmentsPerMeter;
            set => m_OverrideSegmentsPerMeter = value;
        }

        /// <summary>
        /// Number of texture repeats along the entire road in the V direction.
        /// 1 = texture covers the whole road exactly once (V: 0..1).
        /// U (across the road) is always 0..1.
        /// </summary>
        public float Tiling
        {
            get => m_Tiling;
            set => m_Tiling = Mathf.Max(0.001f, value);
        }

        /// <summary>
        /// Hard cap on the number of knots per child lane spline. 0 = no cap.
        /// Read by every child <see cref="LaneSplineMirror"/> in Sync().
        /// </summary>
        public int KnotSamples
        {
            get => m_KnotSamples;
            set => m_KnotSamples = Mathf.Max(0, value);
        }

        /// <summary>
        /// Multiplier on auto-computed Bezier tangent length for every child
        /// lane knot. 1 = chord-matched optimum; higher values round off the
        /// joints on sweeping arcs.
        /// </summary>
        public float TangentScale
        {
            get => m_TangentScale;
            set => m_TangentScale = Mathf.Clamp(value, 0.5f, 4f);
        }

        public SplineContainer Container
        {
            get
            {
                if (m_Container == null)
                    m_Container = GetComponent<SplineContainer>();
                return m_Container;
            }
        }

        public Spline Spline => Container != null && Container.Splines.Count > 0
            ? Container.Splines[0]
            : null;

        void OnEnable()
        {
            m_Container = GetComponent<SplineContainer>();
            RebuildMesh();
        }

        void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            // Inspector tweaks of Width / Tiling / SegmentsPerMeter / KnotSamples /
            // TangentScale all need a mesh + lane-mirror refresh.
            // RebuildMesh() ends with SyncLaneMirrors() so this covers both.
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

        /// <summary>
        /// Returns the world-space position and left/right edge points at normalized parameter t.
        /// </summary>
        public void EvaluateRoadAt(float t, out Vector3 center, out Vector3 left, out Vector3 right)
        {
            var spline = Spline;
            if (spline == null || spline.Count < 2)
            {
                center = left = right = transform.position;
                return;
            }

            SplineUtility.Evaluate(spline, t, out float3 pos, out float3 dir, out float3 up);

            if (math.lengthsq(dir) < 0.0001f)
                dir = new float3(0, 0, 1);

            var scale = transform.lossyScale;
            var safeScale = new float3(
                Mathf.Approximately(scale.x, 0f) ? 1f : 1f / scale.x,
                Mathf.Approximately(scale.y, 0f) ? 1f : 1f / scale.y,
                Mathf.Approximately(scale.z, 0f) ? 1f : 1f / scale.z);
            var tangent = math.normalizesafe(math.cross(up, dir)) * safeScale;

            float halfW = m_Width * 0.5f;
            center = transform.TransformPoint((Vector3)(float3)pos);
            left   = transform.TransformPoint((Vector3)(float3)(pos - tangent * halfW));
            right  = transform.TransformPoint((Vector3)(float3)(pos + tangent * halfW));
        }

        public void RebuildMesh()
        {
            if (m_Mesh == null)
            {
                m_Mesh = new Mesh { name = "RoadSegmentMesh" };
                GetComponent<MeshFilter>().sharedMesh = m_Mesh;
            }
            m_Mesh.Clear();

            var spline = Spline;
            if (spline == null || spline.Count < 2)
                return;
            m_Positions.Clear();
            m_Normals.Clear();
            m_UVs.Clear();
            m_Indices.Clear();

            float length = spline.GetLength();
            if (length < 0.001f) return;

            float tRange = endTrim - startTrim;
            float rangeLength = length * tRange;
            int segments = Mathf.Max(1, Mathf.CeilToInt(rangeLength * m_SegmentsPerMeter));
            int steps = segments + 1;

            float halfW = m_Width * 0.5f;
            float accumulatedDist = 0f;
            float3 prevPos = float3.zero;
            float invRangeLength = rangeLength > 1e-4f ? 1f / rangeLength : 0f;

            for (int i = 0; i < steps; i++)
            {
                float localT = (float)i / segments;
                float t = math.lerp(startTrim, endTrim, localT);

                SplineUtility.Evaluate(spline, t, out float3 pos, out float3 dir, out float3 up);

                if (math.lengthsq(dir) < 0.0001f)
                    dir = new float3(0, 0, 1);

                var scale = transform.lossyScale;
                var safeScale = new float3(
                    Mathf.Approximately(scale.x, 0f) ? 1f : 1f / scale.x,
                    Mathf.Approximately(scale.y, 0f) ? 1f : 1f / scale.y,
                    Mathf.Approximately(scale.z, 0f) ? 1f : 1f / scale.z);
                var tangent = math.normalizesafe(math.cross(up, dir)) * safeScale;

                if (i > 0)
                    accumulatedDist += math.length(pos - prevPos);
                prevPos = pos;

                m_Positions.Add((Vector3)(float3)(pos - tangent * halfW));
                m_Positions.Add((Vector3)(float3)(pos + tangent * halfW));
                m_Normals.Add((Vector3)(float3)up);
                m_Normals.Add((Vector3)(float3)up);

                float v = accumulatedDist * invRangeLength * m_Tiling;
                m_UVs.Add(new Vector2(0f, v));
                m_UVs.Add(new Vector2(1f, v));
            }

            for (int i = 0; i < segments; i++)
            {
                int n = i * 2;
                m_Indices.Add(n + 0);
                m_Indices.Add(n + 2);
                m_Indices.Add(n + 1);

                m_Indices.Add(n + 1);
                m_Indices.Add(n + 2);
                m_Indices.Add(n + 3);
            }

            m_Mesh.SetVertices(m_Positions);
            m_Mesh.SetNormals(m_Normals);
            m_Mesh.SetUVs(0, m_UVs);
            m_Mesh.SetIndices(m_Indices, MeshTopology.Triangles, 0);
            m_Mesh.RecalculateBounds();

            // Push trim/parameter changes down to all derived splines (lanes +
            // pedestrian paths) so their Spline_* splines and endpoint markers
            // stop at the same Corner Pullback point as the road mesh (when
            // they have trim enabled), and so they pick up new TangentScale /
            // KnotSamples values from this RoadSegment.
            SyncLaneMirrors();
            SyncPedestrianSplines();
        }

        void SyncLaneMirrors()
        {
            s_LaneMirrorBuffer.Clear();
            GetComponentsInChildren(true, s_LaneMirrorBuffer);
            for (int i = 0; i < s_LaneMirrorBuffer.Count; i++)
            {
                var m = s_LaneMirrorBuffer[i];
                if (m != null && m.Source == this)
                    m.Sync();
            }
            s_LaneMirrorBuffer.Clear();
        }

        void SyncPedestrianSplines()
        {
            s_PedestrianBuffer.Clear();
            GetComponentsInChildren(true, s_PedestrianBuffer);
            for (int i = 0; i < s_PedestrianBuffer.Count; i++)
            {
                var p = s_PedestrianBuffer[i];
                if (p != null && p.Source == this)
                    p.Sync();
            }
            s_PedestrianBuffer.Clear();
        }
    }
}

using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace RoadSystem
{
    /// <summary>
    /// Drives a pedestrian SplineContainer from a source RoadSegment's centerline
    /// by applying a fixed signed perpendicular offset (m_Offset, in meters).
    /// Intended for sidewalk / pedestrian path splines that run alongside a road.
    ///
    /// Conceptually a stripped-down <see cref="LaneSplineMirror"/>:
    ///   * No lane index / lane count / lane width math — just a single signed
    ///     offset from centerline.
    ///   * Same Source / Reverse / SamplesPerMeter / TrimSpline semantics.
    ///   * Reads TangentScale and KnotSamples from the source RoadSegment so
    ///     pedestrian splines stay visually consistent with the lane splines on
    ///     the same road.
    ///
    /// Recommended placement (mirrors the lane convention):
    ///   * Component lives on a 'pedestrian ref' GameObject — that is the
    ///     transform we position / rotate to face the walking direction.
    ///   * The pedestrian SplineContainer it writes to lives on a child named
    ///     'Spline_PedestrianPath' (auto-resolved if Pedestrian Spline is left null).
    ///   * road_start / road_end markers are siblings of Spline_PedestrianPath
    ///     under the same pedestrian ref.
    ///
    /// Direction convention:
    ///   * The pedestrian spline's knots are ALWAYS written in the local +Z
    ///     direction (knot 0 = walking start, knot last = walking end).
    ///   * Reverse rotates the pedestrian ref 180° around Y so simulation code
    ///     can read world walking direction via
    ///       pedestrianRef.TransformDirection(spline.EvaluateTangent(t)).
    /// </summary>
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [AddComponentMenu("Road System/Pedestrian Spline")]
    public class PedestrianSpline : MonoBehaviour
    {
        [Tooltip("Source RoadSegment whose centerline drives this pedestrian spline. " +
                 "If null, the nearest RoadSegment in parents is used.")]
        [SerializeField] RoadSegment m_Source;

        [Tooltip("Signed perpendicular distance from the centerline (meters). " +
                 "Positive = right side of the source spline's forward direction; " +
                 "negative = left side. Typical value is roughly " +
                 "(RoadWidth / 2 + sidewalk margin) — e.g. 9.5 for a 16m road " +
                 "with ~1.5m of curb + sidewalk on each side.")]
        [SerializeField] float m_Offset = 9.5f;

        [Tooltip("Reverse spline direction (use to flip the +Z walking direction " +
                 "of the pedestrian path).")]
        [SerializeField] bool m_Reverse;

        [Header("Curvature Sampling")]
        [Tooltip("How densely the pedestrian spline samples along the source road. " +
                 "Higher values let the path more accurately follow tight curves of " +
                 "the source centerline, at the cost of more knots. The pedestrian " +
                 "spline always has at least as many knots as the source spline " +
                 "(or 2, whichever is greater).")]
        [SerializeField, Range(0.1f, 10f)] float m_SamplesPerMeter = 1f;

        // KnotSamples (hard cap on knot count) and TangentScale (Bezier tangent
        // length multiplier) are NOT duplicated here — they are read from the
        // source RoadSegment so all derived splines (lane + pedestrian) on a
        // road stay visually consistent.

        [Header("Intersection Trimming")]
        [Tooltip("If enabled, the pedestrian spline (and road_start / road_end markers) " +
                 "follow the source RoadSegment's startTrim / endTrim, which means " +
                 "they get pulled back at intersections by the Corner Pullback amount " +
                 "— matching the road mesh exactly.\n\n" +
                 "If disabled (default), the pedestrian spline always spans the FULL " +
                 "source road regardless of corner pullback. Pedestrians can walk all " +
                 "the way to the road's logical endpoints and you handle crossings " +
                 "with separate intersection logic.")]
        [SerializeField] bool m_TrimPedestrianSpline = false;

        [Header("Endpoint Markers (optional)")]
        [Tooltip("Transform placed at the pedestrian spline START " +
                 "(local +Z = walking direction). " +
                 "Typically a sibling of the pedestrian spline under this 'pedestrian ref'.")]
        [SerializeField] Transform m_RoadStart;
        [Tooltip("Transform placed at the pedestrian spline END.")]
        [SerializeField] Transform m_RoadEnd;

        [Header("Pedestrian Spline Target")]
        [Tooltip("SplineContainer that this component writes the pedestrian spline data " +
                 "to. Recommended placement: this component lives on a 'pedestrian ref' " +
                 "GameObject and points at the SplineContainer on the child " +
                 "'Spline_PedestrianPath'. If left null, auto-resolve will look for: " +
                 "a child named 'Spline_PedestrianPath', then a SplineContainer on " +
                 "this GameObject, then any descendant SplineContainer.")]
        [SerializeField] SplineContainer m_PedestrianSpline;

        SplineContainer m_Container;

        public RoadSegment Source
        {
            get => m_Source;
            set { m_Source = value; Sync(); }
        }

        public float Offset
        {
            get => m_Offset;
            set { m_Offset = value; Sync(); }
        }

        public bool Reverse
        {
            get => m_Reverse;
            set { m_Reverse = value; Sync(); }
        }

        public float SamplesPerMeter
        {
            get => m_SamplesPerMeter;
            set { m_SamplesPerMeter = Mathf.Clamp(value, 0.1f, 10f); Sync(); }
        }

        public SplineContainer Container
        {
            get
            {
                if (m_Container == null)
                    m_Container = ResolveContainer();
                return m_Container;
            }
        }

        SplineContainer ResolveContainer()
        {
            if (m_PedestrianSpline != null) return m_PedestrianSpline;

            // Preferred placement: this component is on a 'pedestrian ref' and
            // the SplineContainer lives on a child named 'Spline_PedestrianPath'.
            var child = transform.Find("Spline_PedestrianPath");
            if (child != null)
            {
                var sc = child.GetComponent<SplineContainer>();
                if (sc != null) return sc;
            }

            // Fallback: same-GameObject SplineContainer (matches LaneSplineMirror's
            // legacy placement, in case someone wires it that way).
            var local = GetComponent<SplineContainer>();
            if (local != null) return local;

            // Last resort: any SplineContainer in descendants.
            return GetComponentInChildren<SplineContainer>(true);
        }

        void OnEnable()
        {
            m_Container = ResolveContainer();
            ResolveSourceIfMissing();
            Spline.Changed += OnAnySplineChanged;
            Sync();
        }

        void OnDisable()
        {
            Spline.Changed -= OnAnySplineChanged;
        }

        void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            ResolveSourceIfMissing();
            Sync();
        }

        void OnTransformParentChanged()
        {
            ResolveSourceIfMissing();
            Sync();
        }

        void ResolveSourceIfMissing()
        {
            if (m_Source == null)
                m_Source = GetComponentInParent<RoadSegment>();
        }

        void OnAnySplineChanged(Spline s, int knot, SplineModification mod)
        {
            if (m_Source != null && m_Source.Spline == s)
                Sync();
        }

        [ContextMenu("Sync Now")]
        public void Sync()
        {
            if (Container == null) return;
            if (m_Source == null) return;

            var srcSpline = m_Source.Spline;
            if (srcSpline == null || srcSpline.Count < 2) return;

            // Trim handling (same convention as LaneSplineMirror).
            float trimStart = m_TrimPedestrianSpline ? Mathf.Clamp01(m_Source.startTrim) : 0f;
            float trimEnd   = m_TrimPedestrianSpline ? Mathf.Clamp01(m_Source.endTrim)   : 1f;
            float trimRange = trimEnd - trimStart;
            if (trimRange < 1e-4f) return;

            // Pedestrian offset is just the user-set signed distance.
            float offset = m_Offset;

            // Sample count: shared logic with LaneSplineMirror — density-driven,
            // floored by source knot count, optionally capped by RoadSegment.KnotSamples.
            float trimmedLength = srcSpline.GetLength() * trimRange;
            int densityCount = Mathf.CeilToInt(trimmedLength * m_SamplesPerMeter) + 1;
            int n = Mathf.Max(srcSpline.Count, densityCount);
            n = Mathf.Max(2, n);
            int knotSamples = m_Source.KnotSamples;
            if (knotSamples > 0)
                n = Mathf.Min(n, Mathf.Max(2, knotSamples));

            // ── 1. Sample world positions and source velocities ──
            var pedWorldPos = new Vector3[n];
            var pedWorldVel = new Vector3[n];
            var srcTransform = m_Source.transform;

            for (int i = 0; i < n; i++)
            {
                float u = (n > 1) ? (i / (float)(n - 1)) : 0f;
                float t = trimStart + u * trimRange;

                m_Source.EvaluateRoadAt(t, out Vector3 worldCenter, out Vector3 _, out Vector3 worldRight);
                Vector3 perpDir = (worldRight - worldCenter);
                float perpLen = perpDir.magnitude;

                SplineUtility.Evaluate(srcSpline, t, out float3 _p, out float3 dir, out float3 up);
                if (math.lengthsq(dir) < 1e-6f) dir = new float3(0, 0, 1);
                if (math.lengthsq(up)  < 1e-6f) up  = new float3(0, 1, 0);

                if (perpLen < 1e-5f)
                    perpDir = (Vector3)(float3)math.normalizesafe(math.cross(up, dir));
                else
                    perpDir /= perpLen;

                pedWorldPos[i] = worldCenter + perpDir * offset;
                pedWorldVel[i] = srcTransform.TransformVector((Vector3)dir);
            }

            // ── 2. Determine the pedestrian-ref WORLD transform ──
            // Place THIS transform at the first knot in walking order, with rotation
            // facing the walking direction (negated for Reverse). The SplineContainer
            // is expected to be on a child; legacy same-GameObject placement also
            // supported.
            bool legacyPlacement = (Container.transform == transform);
            Transform parentT = legacyPlacement
                ? (transform.parent != null ? transform.parent : transform)
                : transform;
            Transform pedT = Container.transform;

            int startSrcIdx = m_Reverse ? (n - 1) : 0;
            Vector3 startWorldPos = pedWorldPos[startSrcIdx];
            Vector3 startWorldDir = m_Reverse ? -pedWorldVel[startSrcIdx] : pedWorldVel[startSrcIdx];
            Vector3 worldUp = srcTransform.up;

            Quaternion parentWorldRot = (startWorldDir.sqrMagnitude > 1e-6f && worldUp.sqrMagnitude > 1e-6f)
                ? Quaternion.LookRotation(startWorldDir.normalized, worldUp)
                : Quaternion.identity;

            if ((parentT.position - startWorldPos).sqrMagnitude > 1e-8f)
                parentT.position = startWorldPos;
            if (Quaternion.Angle(parentT.rotation, parentWorldRot) > 0.01f)
                parentT.rotation = parentWorldRot;

            // ── 3. Write pedestrian spline knots in pedestrian LOCAL space ──
            if (Container.Splines.Count == 0)
                Container.AddSpline(new Spline());
            var dstSpline = Container.Splines[0];
            dstSpline.Resize(n);

            // Same Mirrored tangent mode used by LaneSplineMirror — symmetric
            // handles, no kinks, intuitive editing in the Scene view.
            for (int i = 0; i < n; i++)
            {
                if (dstSpline.GetTangentMode(i) != TangentMode.Mirrored)
                    dstSpline.SetTangentMode(i, TangentMode.Mirrored);
            }

            float dirSign = m_Reverse ? -1f : 1f;
            float segmentScale = (n > 1) ? (1f / (n - 1)) : 1f;
            float tangentScale = m_Source.TangentScale;

            for (int i = 0; i < n; i++)
            {
                int srcIdx = m_Reverse ? (n - 1 - i) : i;

                Vector3 worldPos = pedWorldPos[srcIdx];
                Vector3 localPos = pedT.InverseTransformPoint(worldPos);

                Vector3 worldVel = pedWorldVel[srcIdx] * (dirSign * trimRange * segmentScale * tangentScale);
                Vector3 worldTanOut = worldVel / 3f;
                Vector3 worldTanIn  = -worldTanOut;

                Vector3 localTanIn  = pedT.InverseTransformVector(worldTanIn);
                Vector3 localTanOut = pedT.InverseTransformVector(worldTanOut);

                BezierKnot newKnot = new BezierKnot(
                    new float3(localPos.x, localPos.y, localPos.z),
                    new float3(localTanIn.x,  localTanIn.y,  localTanIn.z),
                    new float3(localTanOut.x, localTanOut.y, localTanOut.z),
                    quaternion.identity);

                if (!ApproximatelyEqual(dstSpline[i], newKnot))
                    dstSpline[i] = newKnot;
            }

            if (dstSpline.Closed != srcSpline.Closed)
                dstSpline.Closed = srcSpline.Closed;

            // ── 4. Update endpoint markers ──
            UpdateEndpointMarkers(dstSpline, pedT, parentT);
        }

        void UpdateEndpointMarkers(Spline dstSpline, Transform pedT, Transform parentT)
        {
            if (m_RoadStart == null && m_RoadEnd == null) return;
            if (dstSpline == null || dstSpline.Count < 2) return;

            Vector3 startWorld = pedT.TransformPoint((Vector3)(float3)dstSpline[0].Position);
            Vector3 endWorld   = pedT.TransformPoint((Vector3)(float3)dstSpline[dstSpline.Count - 1].Position);
            Quaternion walkRot = parentT.rotation;

            if (m_RoadStart != null)
            {
                if ((m_RoadStart.position - startWorld).sqrMagnitude > 1e-8f)
                    m_RoadStart.position = startWorld;
                if (Quaternion.Angle(m_RoadStart.rotation, walkRot) > 0.01f)
                    m_RoadStart.rotation = walkRot;
            }
            if (m_RoadEnd != null)
            {
                if ((m_RoadEnd.position - endWorld).sqrMagnitude > 1e-8f)
                    m_RoadEnd.position = endWorld;
                if (Quaternion.Angle(m_RoadEnd.rotation, walkRot) > 0.01f)
                    m_RoadEnd.rotation = walkRot;
            }
        }

        const float k_Eps = 1e-4f;

        static bool ApproximatelyEqual(BezierKnot a, BezierKnot b)
        {
            return math.lengthsq(a.Position - b.Position) < k_Eps * k_Eps
                && math.lengthsq(a.TangentIn - b.TangentIn) < k_Eps * k_Eps
                && math.lengthsq(a.TangentOut - b.TangentOut) < k_Eps * k_Eps;
        }
    }
}

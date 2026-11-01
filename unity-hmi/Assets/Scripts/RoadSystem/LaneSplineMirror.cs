using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace RoadSystem
{
    /// <summary>
    /// Drives a lane SplineContainer from a source RoadSegment's centerline by
    /// applying a perpendicular offset of
    ///   (LaneIndex - (LaneCount - 1) * 0.5) * LaneWidth
    /// Used to mirror a multi-lane road's centerline into per-lane vehicle paths.
    ///
    /// Recommended placement (current convention):
    ///   * This component lives on the 'road ref' GameObject — that is the
    ///     transform we position / rotate to face the lane's driving direction.
    ///   * The lane SplineContainer it writes to lives on a child named
    ///     'Spline_VehiclePath' (auto-resolved if Lane Spline is left null).
    ///   * road_start / road_end markers are siblings of Spline_VehiclePath
    ///     under the same road ref.
    ///
    /// Legacy placement (still supported for backward compatibility):
    ///   * This component sits on the same GameObject as the lane SplineContainer
    ///     (the Spline_VehiclePath itself); the parent of that GameObject is
    ///     treated as the road ref.
    ///
    /// Direction convention (matches the legacy simulation system):
    ///   * The lane spline's knots are ALWAYS written in the lane's local +Z
    ///     direction (knot 0 = driving start, knot last = driving end).
    ///   * For "reverse" lanes the road ref transform is rotated 180° around Y
    ///     so that local +Z corresponds to world -direction-of-travel.
    ///   * The simulation reads world direction via
    ///       roadRef.TransformDirection(spline.EvaluateTangent(t))
    ///     which therefore returns the correct driving direction in both cases.
    /// </summary>
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [AddComponentMenu("Road System/Lane Spline Mirror")]
    public class LaneSplineMirror : MonoBehaviour
    {
        [Tooltip("Source RoadSegment whose centerline drives this lane spline. " +
                 "If null, the nearest RoadSegment in parents is used.")]
        [SerializeField] RoadSegment m_Source;

        [Tooltip("Total number of lanes on the parent road.")]
        [SerializeField, Min(1)] int m_LaneCount = 4;

        [Tooltip("Index of this lane: 0 = leftmost, LaneCount-1 = rightmost.")]
        [SerializeField, Min(0)] int m_LaneIndex;

        [Tooltip("Width of a single lane (meters).")]
        [SerializeField, Min(0.1f)] float m_LaneWidth = 4f;

        [Tooltip("Reverse spline direction (use for opposite-direction lanes in a two-way road).")]
        [SerializeField] bool m_Reverse;

        [Header("Curvature Sampling")]
        [Tooltip("How densely the lane spline samples along the source road. " +
                 "Higher values let the lane spline more accurately follow tight " +
                 "curves of the source centerline, at the cost of more knots. " +
                 "The lane always has at least as many knots as the source spline " +
                 "(or 2, whichever is greater).")]
        [SerializeField, Range(0.1f, 10f)] float m_SamplesPerMeter = 1f;

        // KnotSamples (hard cap on knot count) and TangentScale (Bezier tangent
        // length multiplier) used to live here per-lane. They moved to RoadSegment
        // so all lanes on a road share one consistent value — that's what you
        // almost always want, and it gives a single place in the Inspector to
        // adjust spline quality for the whole road.

        [Header("Intersection Trimming")]
        [Tooltip("If enabled, the lane spline (and road_start / road_end markers) " +
                 "follow the source RoadSegment's startTrim / endTrim, which means " +
                 "they get pulled back at intersections by the Corner Pullback " +
                 "amount — matching the road mesh exactly.\n\n" +
                 "If disabled (default), the lane spline always spans the FULL " +
                 "source road regardless of corner pullback. Vehicles will drive " +
                 "all the way to the road's logical endpoints.")]
        [SerializeField] bool m_TrimLaneSpline = false;

        [Header("Endpoint Markers (optional)")]
        [Tooltip("Transform placed at the lane spline START (lane local +Z = driving direction). " +
                 "This is typically a sibling of the lane spline under this 'road ref'.")]
        [SerializeField] Transform m_RoadStart;
        [Tooltip("Transform placed at the lane spline END.")]
        [SerializeField] Transform m_RoadEnd;

        [Header("Lane Spline Target")]
        [Tooltip("SplineContainer that this component writes the lane spline data to. " +
                 "Recommended placement: this component lives on a 'road ref' GameObject " +
                 "and points at the SplineContainer on the child 'Spline_VehiclePath'. " +
                 "If left null, auto-resolve will look for: a child named " +
                 "'Spline_VehiclePath', then a SplineContainer on this GameObject " +
                 "(legacy placement), then any descendant SplineContainer.")]
        [SerializeField] SplineContainer m_LaneSpline;

        SplineContainer m_Container;

        public RoadSegment Source
        {
            get => m_Source;
            set { m_Source = value; Sync(); }
        }

        public int LaneCount
        {
            get => m_LaneCount;
            set { m_LaneCount = Mathf.Max(1, value); Sync(); }
        }

        public int LaneIndex
        {
            get => m_LaneIndex;
            set { m_LaneIndex = Mathf.Max(0, value); Sync(); }
        }

        public float LaneWidth
        {
            get => m_LaneWidth;
            set { m_LaneWidth = Mathf.Max(0.1f, value); Sync(); }
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
            if (m_LaneSpline != null) return m_LaneSpline;

            // Preferred placement: this component is on the 'road ref' and the
            // SplineContainer lives on a child named 'Spline_VehiclePath'.
            var child = transform.Find("Spline_VehiclePath");
            if (child != null)
            {
                var sc = child.GetComponent<SplineContainer>();
                if (sc != null) return sc;
            }

            // Legacy placement: this component is on the same GameObject as
            // the SplineContainer it controls.
            var local = GetComponent<SplineContainer>();
            if (local != null) return local;

            // Last resort: pick any SplineContainer in descendants.
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
            // Only react to changes on the source spline.
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

            // Trim handling. When m_TrimLaneSpline is ON we honor the source
            // RoadSegment's startTrim/endTrim (which RoadNetworkManager pulls
            // back at intersections via Corner Pullback), so the lane spline +
            // endpoint markers stop at the same point as the road mesh.
            // When OFF (default), the lane spans the full source road and
            // ignores corner pullback entirely.
            float trimStart = m_TrimLaneSpline ? Mathf.Clamp01(m_Source.startTrim) : 0f;
            float trimEnd   = m_TrimLaneSpline ? Mathf.Clamp01(m_Source.endTrim)   : 1f;
            float trimRange = trimEnd - trimStart;
            if (trimRange < 1e-4f) return; // Fully trimmed — leave previous data alone.

            // Lane offset (signed perpendicular distance from centerline).
            float offset = (m_LaneIndex - (m_LaneCount - 1) * 0.5f) * m_LaneWidth;

            // Sample count: from density × trimmed length, never less than the
            // source's own knot count nor less than 2; optionally capped by the
            // shared KnotSamples on the source RoadSegment (0 = uncapped).
            float trimmedLength = srcSpline.GetLength() * trimRange;
            int densityCount = Mathf.CeilToInt(trimmedLength * m_SamplesPerMeter) + 1;
            int n = Mathf.Max(srcSpline.Count, densityCount);
            n = Mathf.Max(2, n);
            int knotSamples = m_Source.KnotSamples;
            if (knotSamples > 0)
                n = Mathf.Min(n, Mathf.Max(2, knotSamples));

            // ── 1. Sample lane WORLD positions and velocities along the trimmed range ──
            var laneWorldPos = new Vector3[n];
            var laneWorldVel = new Vector3[n]; // source velocity (dS/dt) at the sample, in world
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

                laneWorldPos[i] = worldCenter + perpDir * offset;
                // TransformVector preserves magnitude — important for reproducing
                // source spline parameterization in the lane.
                laneWorldVel[i] = srcTransform.TransformVector((Vector3)dir);
            }

            // ── 2. Determine the road-ref WORLD transform ───────────────────────────────
            // Driving start = first knot in driving order; driving direction = source
            // tangent at that knot, negated for reversed lanes.
            //
            // Placement-aware:
            //   * New placement (this component on 'road ref'): position THIS transform.
            //     The lane SplineContainer lives on a child (typically Spline_VehiclePath).
            //   * Legacy placement (this component on Spline_VehiclePath, beside the
            //     SplineContainer): position the PARENT (= road ref).
            bool legacyPlacement = (Container.transform == transform);
            Transform parentT = legacyPlacement
                ? (transform.parent != null ? transform.parent : transform)
                : transform;
            // The transform whose local space the spline knots live in.
            Transform laneT = Container.transform;

            int startSrcIdx = m_Reverse ? (n - 1) : 0;
            Vector3 startWorldPos = laneWorldPos[startSrcIdx];
            Vector3 startWorldDir = m_Reverse ? -laneWorldVel[startSrcIdx] : laneWorldVel[startSrcIdx];
            Vector3 worldUp = srcTransform.up;

            Quaternion parentWorldRot = (startWorldDir.sqrMagnitude > 1e-6f && worldUp.sqrMagnitude > 1e-6f)
                ? Quaternion.LookRotation(startWorldDir.normalized, worldUp)
                : Quaternion.identity;

            // Apply road-ref transform (skip if effectively unchanged to avoid prefab churn).
            if ((parentT.position - startWorldPos).sqrMagnitude > 1e-8f)
                parentT.position = startWorldPos;
            if (Quaternion.Angle(parentT.rotation, parentWorldRot) > 0.01f)
                parentT.rotation = parentWorldRot;

            // ── 3. Write lane spline knots in lane LOCAL space, going +Z ────────────────
            // For reverse lanes the source data is consumed in reverse order so that
            // the local +Z direction always matches the driving direction.
            // Tangent magnitudes are scaled by trimRange so the lane spline (parameter
            // u in [0,1]) reproduces the same shape as the trimmed source segment.
            if (Container.Splines.Count == 0)
                Container.AddSpline(new Spline());
            var dstSpline = Container.Splines[0];
            dstSpline.Resize(n);

            // Tag every knot as Mirrored BEFORE writing values. TangentMode
            // is per-knot metadata stored on the Spline (not on the
            // BezierKnot struct), so assigning dstSpline[i] = newKnot below
            // doesn't update it. We want Mirrored so:
            //   * In/Out tangents stay collinear AND equal in magnitude
            //     (no kinks, fully symmetric handles at every knot).
            //   * Editing one tangent in the Scene view auto-mirrors the
            //     other in both direction and length.
            // Setting mode first means the subsequent SetKnot calls run
            // their EnforceTangentMode pass against Mirrored, and since
            // our computed tangents already satisfy In = -Out, the values
            // pass through unchanged.
            for (int i = 0; i < n; i++)
            {
                if (dstSpline.GetTangentMode(i) != TangentMode.Mirrored)
                    dstSpline.SetTangentMode(i, TangentMode.Mirrored);
            }

            float dirSign = m_Reverse ? -1f : 1f;
            // Tangent handles on a Bezier knot are expressed in SEGMENT-local
            // parameter space (each segment goes 0..1). With n knots there are
            // n-1 segments, so a unit step in the lane's full-spline parameter
            // u corresponds to (n-1) units of segment parameter. Hence the
            // velocity used for tangent handles must be divided by (n-1) to
            // avoid over-shooting bulges when n > 2.
            float segmentScale = (n > 1) ? (1f / (n - 1)) : 1f;

            for (int i = 0; i < n; i++)
            {
                int srcIdx = m_Reverse ? (n - 1 - i) : i;

                Vector3 worldPos = laneWorldPos[srcIdx];
                Vector3 localPos = laneT.InverseTransformPoint(worldPos);

                // Lane velocity at this knot in SEGMENT-local parameter space:
                //   dL/du_full   = sourceVelocity * (±trimRange)
                //   dL/du_seg    = dL/du_full * (1 / (n-1))
                // RoadSegment.TangentScale multiplies the resulting tangent
                // length so the user can round off Bezier segments on sweeping
                // arcs without having to raise SamplesPerMeter (which adds knots).
                float tangentScale = m_Source.TangentScale;
                Vector3 worldLaneVel = laneWorldVel[srcIdx] * (dirSign * trimRange * segmentScale * tangentScale);

                // Cubic Bezier velocity at a knot equals 3 * TangentOut.
                Vector3 worldTanOut = worldLaneVel / 3f;
                Vector3 worldTanIn  = -worldTanOut;

                Vector3 localTanIn  = laneT.InverseTransformVector(worldTanIn);
                Vector3 localTanOut = laneT.InverseTransformVector(worldTanOut);

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

            // ── 4. Update endpoint markers ──────────────────────────────────────────────
            UpdateEndpointMarkers(dstSpline, laneT, parentT);
        }

        void UpdateEndpointMarkers(Spline dstSpline, Transform laneT, Transform parentT)
        {
            if (m_RoadStart == null && m_RoadEnd == null) return;
            if (dstSpline == null || dstSpline.Count < 2) return;

            // Markers live in the road-ref frame. Position them at the lane
            // spline endpoints so they automatically inherit the driving-direction
            // rotation from the road ref.
            Vector3 startWorld = laneT.TransformPoint((Vector3)(float3)dstSpline[0].Position);
            Vector3 endWorld   = laneT.TransformPoint((Vector3)(float3)dstSpline[dstSpline.Count - 1].Position);
            Quaternion driveRot = parentT.rotation;

            if (m_RoadStart != null)
            {
                if ((m_RoadStart.position - startWorld).sqrMagnitude > 1e-8f)
                    m_RoadStart.position = startWorld;
                if (Quaternion.Angle(m_RoadStart.rotation, driveRot) > 0.01f)
                    m_RoadStart.rotation = driveRot;
            }
            if (m_RoadEnd != null)
            {
                if ((m_RoadEnd.position - endWorld).sqrMagnitude > 1e-8f)
                    m_RoadEnd.position = endWorld;
                if (Quaternion.Angle(m_RoadEnd.rotation, driveRot) > 0.01f)
                    m_RoadEnd.rotation = driveRot;
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

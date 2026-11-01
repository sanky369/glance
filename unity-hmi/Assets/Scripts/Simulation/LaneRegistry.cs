using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using System.Linq;
using Unity.Mathematics;
using UnityEngine.Profiling;

namespace HMI_Hub_Template
{
    public class LaneRegistry : Singleton<LaneRegistry>
    {
        [Header("Splines (Auto-Populated on Bake)")]
        public SplineContainer[] vehicleLanes;
        public SplineContainer[] pedestrianLanes;
        
        [Header("Adjacency Data")]
        public AdjacentLanes[] adjacentVehicleLanes;
        
        [Tooltip("Maximum side-to-side distance between two lanes for them to count as adjacent. Roughly one lane width")]
        public float maxAdjacentDistance = 6.0f;
        [Tooltip("Slack for longitudinal sampling mismatch between two lane splines")]
        public float maxLongitudinalMismatch = 25.0f;
        [Tooltip("How parallel do they need to be? (1.0 = perfectly parallel, 0.9 = slight curve allowed)")]
        public float parallelThreshold = 0.95f; 
        [Tooltip("Minimum side-to-side distance to not mistake the lane straight ahead as adjacent")]
        public float minLateralDistance = 1.5f;

        [Header("Intersections & Nodes")]
        [Tooltip("How close world-space knots must be to connect (meters).")]
        public float connectionTolerance = 2.0f; 
        public CrosswalkSetupHelper[] crosswalkHelpers;

        [HideInInspector] public CrosswalkData[] crosswalks;
        [HideInInspector] public LaneSimData[] vehicleLaneData;
        
        [Header("Node Map (Debug via References)")]
        public SplineNode[] nodeMap;

        private float[] _pedestrianLaneLengthsWorld;
        private float[] _pedestrianLaneLengthsLocal;

        // ── Arc-length LUTs ──────────────────────────────────────────────────
        // Built once at startup; O(log LUT_SIZE) lookup at runtime instead of
        // the O(knots) SplineUtility.ConvertIndexUnit for better performance
        private const int LUT_SIZE = 128;

        private struct SplineLUT
        {
            // arcAtSample[i] = cumulative LOCAL arc length at t = i / (LUT_SIZE-1)
            public float[] arcAtSample;
            public float   localLength;
            public float   worldLength;
            
            public float WorldDistToT(float worldDist)
            {
                if (worldLength <= 0f || localLength <= 0f) return 0f;
                float localDist = Mathf.Clamp(worldDist * (localLength / worldLength),
                                              0f, localLength);
                
                int lo = 0, hi = LUT_SIZE - 1;
                while (lo < hi - 1)
                {
                    int mid = (lo + hi) >> 1;
                    if (arcAtSample[mid] <= localDist) lo = mid;
                    else                               hi = mid;
                }

                float segArc = arcAtSample[hi] - arcAtSample[lo];
                float frac   = segArc > 1e-8f ? (localDist - arcAtSample[lo]) / segArc : 0f;
                return Mathf.Clamp01((lo + frac) / (LUT_SIZE - 1));
            }

            public float TToWorldDist(float t)
            {
                if (localLength <= 0f || worldLength <= 0f) return 0f;
                t = Mathf.Clamp01(t);
                float idx  = t * (LUT_SIZE - 1);
                int   lo   = (int)idx;
                int   hi   = Mathf.Min(lo + 1, LUT_SIZE - 1);
                float localDist = Mathf.Lerp(arcAtSample[lo], arcAtSample[hi], idx - lo);
                return localDist * (worldLength / localLength);
            }
        }

        private SplineLUT[] _vehicleLUTs;
        private SplineLUT[] _pedestrianLUTs;

        private static SplineLUT BuildLUT(Spline spline, float worldLength)
        {
            var lut = new SplineLUT
            {
                arcAtSample = new float[LUT_SIZE],
                worldLength = worldLength
            };

            float   step = 1f / (LUT_SIZE - 1);
            Vector3 prev = (Vector3)spline.EvaluatePosition(0f);
            lut.arcAtSample[0] = 0f;

            for (int i = 1; i < LUT_SIZE; i++)
            {
                Vector3 curr = (Vector3)spline.EvaluatePosition(i * step);
                lut.arcAtSample[i] = lut.arcAtSample[i - 1] + Vector3.Distance(prev, curr);
                prev = curr;
            }

            lut.localLength = lut.arcAtSample[LUT_SIZE - 1];
            return lut;
        }
        
        [System.Serializable]
        public class AdjacentLanes
        {
            public int leftLaneId = -1;
            public int rightLaneId = -1;
        }

        [System.Serializable]
        public class SplineNode
        {
            public SplineContainer sourceSpline;
            public List<SplineContainer> nextSplines = new List<SplineContainer>();
            [HideInInspector] public int[] nextSplineIds;
        }

        private void Awake()
        {
            if (nodeMap == null || nodeMap.Length != vehicleLanes.Length)
            {
Profiler.BeginSample("BakeNodeMap");
                BakeNodeMap();
Profiler.EndSample();
            }
            
Profiler.BeginSample("InitializeSimData");
            InitializeSimData();
Profiler.EndSample();
Profiler.BeginSample("InitializeCrosswalks");
            InitializeCrosswalks();
Profiler.EndSample();
        }

        private void InitializeSimData()
        {
Profiler.BeginSample("Pedestrians");
            // ── Pedestrian lanes ────────────────────────────────────────────
            _pedestrianLaneLengthsWorld = new float[pedestrianLanes.Length];
            _pedestrianLaneLengthsLocal = new float[pedestrianLanes.Length];
            _pedestrianLUTs             = new SplineLUT[pedestrianLanes.Length];
            for (int i = 0; i < pedestrianLanes.Length; i++)
            {
Profiler.BeginSample("OnePedestrian");
                if (pedestrianLanes[i] == null) continue;
                float worldLen = pedestrianLanes[i].Spline.CalculateLength(pedestrianLanes[i].transform.localToWorldMatrix);
                _pedestrianLaneLengthsWorld[i] = worldLen;
                _pedestrianLUTs[i]             = BuildLUT(pedestrianLanes[i].Spline, worldLen);
                _pedestrianLaneLengthsLocal[i] = _pedestrianLUTs[i].localLength;
Profiler.EndSample();
            }
Profiler.EndSample();

Profiler.BeginSample("VehicleLanes");
            // ── Vehicle lanes ───────────────────────────────────────────────
            vehicleLaneData = new LaneSimData[vehicleLanes.Length];
            _vehicleLUTs    = new SplineLUT[vehicleLanes.Length];
            for (int i = 0; i < vehicleLanes.Length; i++)
            {
                if (vehicleLanes[i] == null) continue;

Profiler.BeginSample("OneLane");
Profiler.BeginSample("Spline.CalculateLength");
                float worldLen = vehicleLanes[i].Spline.CalculateLength(vehicleLanes[i].transform.localToWorldMatrix);
Profiler.EndSample();
Profiler.BeginSample("BuildLUT");
                _vehicleLUTs[i] = BuildLUT(vehicleLanes[i].Spline, worldLen);
Profiler.EndSample();

                RoadSplineTagComponent tag = vehicleLanes[i].GetComponent<RoadSplineTagComponent>();

                vehicleLaneData[i] = new LaneSimData
                {
                    length      = worldLen,
                    localLength = _vehicleLUTs[i].localLength,
                    speedLimit  = tag != null ? tag.speedLimit : 35f,
                    accelerationMultiplier = tag != null ? tag.accelerationMultiplier : 1.0f,
                    nextLanes   = nodeMap[i].nextSplineIds,
                    adjacentLanes = new AdjacentLanesData
                    {
                        leftLaneId  = adjacentVehicleLanes[i].leftLaneId,
                        rightLaneId = adjacentVehicleLanes[i].rightLaneId
                    },
                    isIntersection = tag != null && tag.isIntersection
                };
Profiler.EndSample();
            }
Profiler.EndSample();
        }

        private void InitializeCrosswalks()
        {
            if (crosswalkHelpers == null || crosswalkHelpers.Length == 0) return;

            crosswalks = new CrosswalkData[crosswalkHelpers.Length];
            for (int i = 0; i < crosswalkHelpers.Length; i++)
            {
                CrosswalkSetupHelper helper = crosswalkHelpers[i];
                int vId = System.Array.IndexOf(vehicleLanes, helper.vehicleSpline);
                int pId = System.Array.IndexOf(pedestrianLanes, helper.pedestrianSpline);

                crosswalks[i] = new CrosswalkData()
                {
                    crosswalkName = helper.crosswalkName,
                    vehicleLaneId = vId, 
                    distanceAlongVehicleLane = helper.vehicleDistance,
                    pedestrianLaneId = pId,
                    distanceAlongPedestrianLane = helper.pedestrianDistance,
                    triggerRadius = helper.triggerRadius,
                    stopLineOffset = helper.stopLineOffset,
                    isOccupied = false
                };
            }
        }

        [ContextMenu("Bake Node Map")]
        public void BakeNodeMap()
        {
            var vehicleTags = Object.FindObjectsByType<RoadSplineTagComponent>(FindObjectsSortMode.InstanceID);
            var pedestrianTags = Object.FindObjectsByType<PedestrianSplineTagComponent>(FindObjectsSortMode.InstanceID);

            // exclude null entries (CM-1674).
            var validVehicleLanes = new List<SplineContainer>();
            foreach (var tag in vehicleTags)
            {
                SplineContainer container = tag.GetComponent<SplineContainer>();
                if (container == null)
                {
                    Debug.LogWarning($"[LaneRegistry] '{tag.gameObject.name}' has a RoadSplineTagComponent but no SplineContainer - skipping it during Bake Node Map.", tag.gameObject);
                    continue;
                }
                validVehicleLanes.Add(container);
            }
            vehicleLanes = validVehicleLanes.ToArray();

            var validPedestrianLanes = new List<SplineContainer>();
            foreach (var tag in pedestrianTags)
            {
                SplineContainer container = tag.GetComponent<SplineContainer>();
                if (container == null)
                {
                    Debug.LogWarning($"[LaneRegistry] '{tag.gameObject.name}' has a PedestrianSplineTagComponent but no SplineContainer - skipping it during Bake Node Map.", tag.gameObject);
                    continue;
                }
                validPedestrianLanes.Add(container);
            }
            pedestrianLanes = validPedestrianLanes.ToArray();
            
            adjacentVehicleLanes = new AdjacentLanes[vehicleLanes.Length];
            for (int i = 0; i < vehicleLanes.Length; i++)
            {
                adjacentVehicleLanes[i] = new AdjacentLanes();
            }
            
            for (int i = 0; i < vehicleLanes.Length; i++)
            {
                SplineContainer currentLane = vehicleLanes[i];
                
                RoadSplineTagComponent currentRoadSplineTagComponent = currentLane.GetComponent<RoadSplineTagComponent>();
                if (currentRoadSplineTagComponent != null && currentRoadSplineTagComponent.isIntersection) continue;

                Vector3 currentMidPos = currentLane.transform.TransformPoint((Vector3)currentLane.Spline.EvaluatePosition(0.5f));
                Vector3 currentForward = currentLane.transform.TransformDirection((Vector3)currentLane.Spline.EvaluateTangent(0.5f)).normalized;

                Vector3 currentRight = Vector3.Cross(Vector3.up, currentForward).normalized;

                for (int j = 0; j < vehicleLanes.Length; j++)
                {
                    if (i == j) continue;
                    
                    SplineContainer otherLane = vehicleLanes[j];

                    RoadSplineTagComponent otherTag = otherLane.GetComponent<RoadSplineTagComponent>();
                    if (otherTag != null && otherTag.isIntersection) continue;

                    Vector3 otherMidPos = otherLane.transform.TransformPoint((Vector3)otherLane.Spline.EvaluatePosition(0.5f));
                    Vector3 otherForward = otherLane.transform.TransformDirection((Vector3)otherLane.Spline.EvaluateTangent(0.5f)).normalized;
                    
                    
                    float forwardAlignment = Vector3.Dot(currentForward, otherForward);
                    if (forwardAlignment < parallelThreshold) continue;
                    
                    Vector3 directionToOther = otherMidPos - currentMidPos;
                    float lateralDistance = Vector3.Dot(directionToOther, currentRight);
                    float longitudinalDistance = Mathf.Abs(Vector3.Dot(directionToOther, currentForward));
                    
                    if (longitudinalDistance > maxLongitudinalMismatch) continue;
                    if (Mathf.Abs(lateralDistance) < minLateralDistance) continue;
                    if (Mathf.Abs(lateralDistance) > maxAdjacentDistance) continue;
                    
                    if (lateralDistance > 0)
                    {
                        adjacentVehicleLanes[i].rightLaneId = j;
                    }
                    else
                    {
                        adjacentVehicleLanes[i].leftLaneId = j;
                    }
                }
            }
            
            nodeMap = new SplineNode[vehicleLanes.Length];
            int connectionCount = 0;

            for (int i = 0; i < vehicleLanes.Length; i++)
            {
                SplineContainer current = vehicleLanes[i];
                List<SplineContainer> nextRefs = new List<SplineContainer>();
                List<int> nextIds = new List<int>();
                
                int lastKnotIndex = current.Spline.Count - 1;
                Vector3 worldEnd = current.transform.TransformPoint((Vector3)current.Spline[lastKnotIndex].Position);

                for (int j = 0; j < vehicleLanes.Length; j++)
                {
                    if (i == j) continue;
                    
                    Vector3 worldTargetStart = vehicleLanes[j].transform.TransformPoint((Vector3)vehicleLanes[j].Spline[0].Position);

                    float dist = Vector3.Distance(worldEnd, worldTargetStart);
                    if (dist <= connectionTolerance)
                    {
                        nextRefs.Add(vehicleLanes[j]);
                        nextIds.Add(j);
                        connectionCount++;
                    }
                }

                nodeMap[i] = new SplineNode()
                {
                    sourceSpline = current,
                    nextSplines = nextRefs,
                    nextSplineIds = nextIds.ToArray()
                };
            }

            for (int i = 0; i < vehicleLanes.Length; i++)
            {
                if (vehicleLanes[i] == null) continue;
                if (adjacentVehicleLanes[i].leftLaneId != -1 || adjacentVehicleLanes[i].rightLaneId != -1) continue;
                RoadSplineTagComponent laneTag = vehicleLanes[i].GetComponent<RoadSplineTagComponent>();
                if (laneTag != null && laneTag.isIntersection) continue;
                Transform group = vehicleLanes[i].transform.parent != null ? vehicleLanes[i].transform.parent.parent : null;
                Debug.LogWarning($"[LaneRegistry] Lane {i} ({(group != null ? group.name : vehicleLanes[i].name)}) baked with no neighbour on either side - lane changes from it will always be blocked.", vehicleLanes[i]);
            }

            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
            Debug.Log($"[LaneRegistry] Bake Success: {vehicleLanes.Length} lanes, {connectionCount} connections found, adjacencies mapped.");
        }

        public Vector3 GetVehicleWorldPosition(int splineId, float worldDistance)
        {
            if (splineId < 0 || splineId >= vehicleLanes.Length) return Vector3.zero;
            SplineContainer container = vehicleLanes[splineId];
            if (container == null) return Vector3.zero;
            float t = WorldDistToT(splineId, worldDistance);
            return container.transform.TransformPoint((Vector3)container.Spline.EvaluatePosition(t));
        }

        public Vector3 GetVehicleWorldDirection(int splineId, float worldDistance)
        {
            if (splineId < 0 || splineId >= vehicleLanes.Length) return Vector3.forward;
            SplineContainer container = vehicleLanes[splineId];
            if (container == null) return Vector3.forward;
            float t = WorldDistToT(splineId, worldDistance);
            return container.transform.TransformDirection((Vector3)container.Spline.EvaluateTangent(t)).normalized;
        }

        // World-space arc distance → Bezier parameter t via precomputed LUT.
        private float WorldDistToT(int splineId, float worldDistance)
            => _vehicleLUTs[splineId].WorldDistToT(worldDistance);

        public float GetVehicleLaneLength(int splineId)
        {
            if (vehicleLaneData == null || splineId < 0 || splineId >= vehicleLaneData.Length) return 0f;
            return vehicleLaneData[splineId].length;
        }

        public float GetPedestrianLaneLength(int splineId)
        {
            if (_pedestrianLaneLengthsWorld == null || splineId < 0 || splineId >= _pedestrianLaneLengthsWorld.Length) return 0f;
            return _pedestrianLaneLengthsWorld[splineId];
        }

        public int[] GetNextVehicleLanes(int splineId)
        {
            if (vehicleLaneData == null || splineId < 0 || splineId >= vehicleLaneData.Length) return null;
            return vehicleLaneData[splineId].nextLanes;
        }

        public float GetEquivalentDistanceOnSpline(int targetSplineId, Vector3 worldPosition)
        {
            if (targetSplineId < 0 || targetSplineId >= vehicleLanes.Length) return 0f;
            SplineContainer container = vehicleLanes[targetSplineId];
            if (container == null) return 0f;

            Vector3 localPos = container.transform.InverseTransformPoint(worldPosition);
            SplineUtility.GetNearestPoint(container.Spline, localPos, out _, out float t);

            // t → world arc distance via LUT (avoids ConvertIndexUnit)
Profiler.BeginSample("TToWorldDist");
            float f = _vehicleLUTs[targetSplineId].TToWorldDist(t);
Profiler.EndSample();
            return f;
        }

        public Vector3 GetPedestrianWorldPosition(int splineId, float worldDistance)
        {
            if (splineId < 0 || splineId >= pedestrianLanes.Length) return Vector3.zero;
            SplineContainer container = pedestrianLanes[splineId];
            if (container == null) return Vector3.zero;
            //float t = PedWorldDistToT(splineId, worldDistance);
Profiler.BeginSample("WorldDistToT");
            float t = _pedestrianLUTs[splineId].WorldDistToT(worldDistance);
Profiler.EndSample();
Profiler.BeginSample("EvaluatePosition");
            Vector3 pos = (Vector3)container.Spline.EvaluatePosition(t);
Profiler.EndSample();
            return container.transform.TransformPoint(pos);
        }

        public Vector3 GetPedestrianWorldDirection(int splineId, float worldDistance)
        {
            if (splineId < 0 || splineId >= pedestrianLanes.Length) return Vector3.forward;
            SplineContainer container = pedestrianLanes[splineId];
            if (container == null) return Vector3.forward;
Profiler.BeginSample("WorldDistToT");
            float t = _pedestrianLUTs[splineId].WorldDistToT(worldDistance);
Profiler.EndSample();
Profiler.BeginSample("EvaluateTangent");
            Vector3 dir = (Vector3)container.Spline.EvaluateTangent(t);
Profiler.EndSample();
Profiler.BeginSample("normalize");
            dir = container.transform.TransformDirection(dir).normalized;
Profiler.EndSample();
            return dir;
        }

        //private float PedWorldDistToT(int splineId, float worldDistance)
        //    => _pedestrianLUTs[splineId].WorldDistToT(worldDistance);
        
        [ContextMenu("Auto-Detect Crosswalks")]
        public void AutoDetectCrosswalks()
        {
            Transform root = transform.Find("Auto_Crosswalks");
            if (root != null)
            {
                DestroyImmediate(root.gameObject);
            }
            
            root = new GameObject("Auto_Crosswalks").transform;
            root.SetParent(this.transform);
            root.localPosition = Vector3.zero;

            List<CrosswalkSetupHelper> detectedCrosswalks = new List<CrosswalkSetupHelper>();
            float stepSize = 1.0f; 
            float overlapThreshold = 1.5f;

            for (int p = 0; p < pedestrianLanes.Length; p++)
            {
                SplineContainer pedSpline = pedestrianLanes[p];
                if (pedSpline == null) continue;
                
                float pedLength = pedSpline.Spline.CalculateLength(pedSpline.transform.localToWorldMatrix);

                for (int v = 0; v < vehicleLanes.Length; v++)
                {
                    SplineContainer vehSpline = vehicleLanes[v];
                    if (vehSpline == null) continue;

                    float vehLength = vehSpline.Spline.CalculateLength(vehSpline.transform.localToWorldMatrix);
                    
                    for (float d = 0; d < pedLength; d += stepSize)
                    {
                        float pedT = d / pedLength;
                        Vector3 pedWorldPos = pedSpline.transform.TransformPoint((Vector3)pedSpline.Spline.EvaluatePosition(pedT));
                        
                        Vector3 localPedPos = vehSpline.transform.InverseTransformPoint(pedWorldPos);
                        SplineUtility.GetNearestPoint(vehSpline.Spline, localPedPos, out Unity.Mathematics.float3 nearestLocal, out float vehT);
                        Vector3 nearestVehWorldPos = vehSpline.transform.TransformPoint((Vector3)nearestLocal);
                        
                        if (Vector3.Distance(pedWorldPos, nearestVehWorldPos) <= overlapThreshold)
                        {
                            GameObject crosswalkObj = new GameObject($"Auto_Ped{p}_Veh{v}");
                            crosswalkObj.transform.SetParent(root);
                            crosswalkObj.transform.position = pedWorldPos; 
                            
                            CrosswalkSetupHelper helper = crosswalkObj.AddComponent<CrosswalkSetupHelper>();
                            helper.crosswalkName = crosswalkObj.name;
                            helper.pedestrianSpline = pedSpline;
                            helper.pedestrianDistance = d;
                            helper.vehicleSpline = vehSpline;
                            helper.vehicleDistance = vehT * vehLength;
                            helper.triggerRadius = 3.0f;
                            helper.stopLineOffset = 2.5f;
                            
                            detectedCrosswalks.Add(helper);

                            d += 5.0f; //skip ahead
                        }
                    }
                }
            }

            crosswalkHelpers = detectedCrosswalks.ToArray();
            Debug.Log($"[LaneRegistry] Auto-detected {crosswalkHelpers.Length} conflict zones!");
            
        #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
        #endif
        }
    }
}
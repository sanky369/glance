using System;
using System.Collections.Generic;
using System.Linq;
using Modules;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HMI_Hub_Template
{
    public class SimulationSystem : MonoBehaviour
    {
        public StartingSimulationData startData;
        public HeroSimSettings heroSettings;
        public DataCore dataCore;
        public HeroSensorDataLayer heroSensorDataLayer;
        public CarData heroCarData;
        
        public event Action OnLaneChangeBlockedWarning;
        
        private int _nextVehicleId = 1;
        private int _nextPedestrianId = 1;

        private Dictionary<int, List<int>> _vehiclesByLane = new Dictionary<int, List<int>>();

        private List<IntersectionController> _intersections = new List<IntersectionController>();
        
        private readonly List<int>    _validRespawnLanes  = new List<int>();

        // Redistribution system — pre-allocated to avoid per-tick garbage
        private float _redistributionTimer = 0f;
        private readonly List<int> _redistributionTargetLanes   = new List<int>();
        private readonly List<int> _redistributionVehicleQueue  = new List<int>();
        private readonly List<float> _redistributionLaneSortBuf = new List<float>();

        private readonly HashSet<int> _crosswalkLaneIds   = new HashSet<int>();
        private Comparison<int> _vehicleSortComparison;

        // Per-frame lookup: laneId → distanceAlongSpline of the vehicle nearest to
        // the start of that lane.  Built once before the main vehicle loop so that
        // the cross-spline buffer check can read it in O(1) per vehicle.
        private readonly Dictionary<int, float> _laneNearestToStart = new Dictionary<int, float>();

        // Index of the hero car currently mid lane-change
        private int _mergingVehicleIdx = -1;
        
        public static bool IsPaused = false;

        private const float BrakeSettleEpsilon = 0.2f;

        // While merging, traffic in the lane being vacated is released once the car is guaranteed to clear of the old lane
        private const float LaneChangeClearProgress = 0.55f;
        private const float LaneChangeClearMargin = 5f;

        // Forward distance a full lane change consumes at the given speed
        private float LaneChangeForwardDistance(float speed)
        {
            return startData.laneChangeDuration > 0f
                ? Mathf.Clamp(speed * startData.laneChangeDuration,
                    startData.minLaneChangeSpeed * startData.laneChangeDuration,
                    startData.laneChangeDistance)
                : startData.laneChangeDistance;
        }

        void Start()
        {
            dataCore.activeVehicles.Clear();
            dataCore.activePedestrians.Clear();

            //sort cache
            _vehicleSortComparison = (a, b) => dataCore.activeVehicles[b].distanceAlongSpline.CompareTo(dataCore.activeVehicles[a].distanceAlongSpline);

            InitializeTraffic();
            InitializePedestrians();

            _intersections = GameObject.FindObjectsByType<IntersectionController>(FindObjectsSortMode.None).ToList();
        }

        private void InitializeTraffic()
        {
            int vehicleTypeCount = System.Enum.GetValues(typeof(VehicleType)).Length;

            for (int laneId = 0; laneId < LaneRegistry.Instance.vehicleLaneData.Length; laneId++)
            {
                if (LaneRegistry.Instance.vehicleLaneData[laneId].isIntersection) continue; //skip spawning in intersections

                float laneLength = LaneRegistry.Instance.vehicleLaneData[laneId].length;
                
                int spawnCount = Mathf.Min(startData.vehiclesPerLane, Mathf.FloorToInt(laneLength / (startData.bufferDistance * 1.5f)));
                if (spawnCount <= 0) continue; 

                float spacing = laneLength / spawnCount;

                if (!_vehiclesByLane.ContainsKey(laneId))
                {
                    _vehiclesByLane[laneId] = new List<int>();
                }

                for (int i = 0; i < spawnCount; i++)
                {
                    float currentDistance = i * spacing;
                    float randomSpeed = Random.Range(startData.minSpeed, startData.maxSpeed);
                    VehicleType randomType = (VehicleType)Random.Range(0, vehicleTypeCount);

                    Vector3 startPos = LaneRegistry.Instance.GetVehicleWorldPosition(laneId, currentDistance);
                    Vector3 startDir = LaneRegistry.Instance.GetVehicleWorldDirection(laneId, currentDistance);
                    Quaternion startRot = startDir != Vector3.zero ? Quaternion.LookRotation(startDir) : Quaternion.identity;

                    VehicleData newVehicle = new VehicleData()
                    {
                        vehicleId = _nextVehicleId++,
                        vehicleType = randomType,
                        splineId = laneId,
                        distanceAlongSpline = currentDistance,
                        currentSpeed = randomSpeed,
                        desiredSpeed = randomSpeed,
                        position = startPos,
                        rotation = startRot,
                        isChangingLanes = false
                    };

                    dataCore.activeVehicles.Add(newVehicle);
                }
            }
        }

        private void InitializePedestrians()
        {
            _crosswalkLaneIds.Clear();
            var pedTags = UnityEngine.Object.FindObjectsByType<PedestrianSplineTagComponent>(FindObjectsSortMode.None);
            foreach (var tag in pedTags)
            {
                if (!tag.isCrosswalk) continue;
                var sc = tag.GetComponent<UnityEngine.Splines.SplineContainer>();
                int id = System.Array.IndexOf(LaneRegistry.Instance.pedestrianLanes, sc);
                if (id >= 0) _crosswalkLaneIds.Add(id);
            }

            for (int laneId = 0; laneId < LaneRegistry.Instance.pedestrianLanes.Length; laneId++)
            {
                float laneLength = LaneRegistry.Instance.GetPedestrianLaneLength(laneId);
                if (laneLength <= 0f) continue;

                bool isCrosswalkLane = _crosswalkLaneIds.Contains(laneId);

                if (isCrosswalkLane)
                {
                    SpawnPedestrian(laneId, laneLength * 0.5f, isWaiting: true);
                }
                else
                {
                    float spacing = Mathf.Max(startData.sidewalkPedSpacing, 1f);
                    int count = Mathf.Max(1, Mathf.RoundToInt(laneLength / spacing));
                    float gap = laneLength / count;
                    for (int i = 0; i < count; i++)
                        SpawnPedestrian(laneId, i * gap, isWaiting: false);
                }
            }
        }

        private void SpawnPedestrian(int laneId, float dist, bool isWaiting)
        {
            float walkSpeed = Random.Range(startData.minPedestrianSpeed, startData.maxPedestrianSpeed);

            Vector3 pos = LaneRegistry.Instance.GetPedestrianWorldPosition(laneId, dist);
            Vector3 dir = LaneRegistry.Instance.GetPedestrianWorldDirection(laneId, dist);
            Quaternion rot = dir != Vector3.zero ? Quaternion.LookRotation(dir) : Quaternion.identity;

            dataCore.activePedestrians.Add(new PedestrianData
            {
                pedestrianId = _nextPedestrianId++,
                splineId = laneId,
                distanceAlongSpline = dist,
                desiredSpeed = walkSpeed,
                currentSpeed = isWaiting ? 0f : walkSpeed,
                isWaiting = isWaiting,
                position = pos,
                rotation = rot
            });
        }
        
        void Update()
        {
            if (IsPaused) return;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
                {
                    RequestHeroLaneChange(true); // Left
                }
                if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
                {
                    RequestHeroLaneChange(false); // Right
                }
                
                if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
                {
                    AdjustHeroSpeed(5f); // Increase target speed by 5
                }
                if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
                {
                    AdjustHeroSpeed(-5f); // Decrease target speed by 5
                }
            }
            
            float dt = Time.deltaTime;
            
            foreach (var laneList in _vehiclesByLane.Values)
            {
                laneList.Clear();
            }

            _mergingVehicleIdx = -1;

            for (int i = 0; i < dataCore.activeVehicles.Count; i++)
            {
                if (dataCore.activeVehicles[i].isChangingLanes)
                    _mergingVehicleIdx = i;

                int laneId = dataCore.activeVehicles[i].splineId;
                if (!_vehiclesByLane.ContainsKey(laneId)) 
                    _vehiclesByLane[laneId] = new List<int>();
                    
                _vehiclesByLane[laneId].Add(i);
            }

            if (LaneRegistry.Instance.crosswalks != null)
            {
                for (int i = 0; i < LaneRegistry.Instance.crosswalks.Length; i++)
                {
                    LaneRegistry.Instance.crosswalks[i].isOccupied = false;
                }
            }
            
            ProcessPedestrians(dt);
            ProcessTraffic(dt);

            _redistributionTimer += dt;
            float interval = startData.redistributionInterval > 0f ? startData.redistributionInterval : 1f;
            if (_redistributionTimer >= interval)
            {
                _redistributionTimer = 0f;
                RedistributeVehicles();
            }

            UpdateHeroSensors();
        }

        private void ProcessPedestrians(float dt)
        {
            for (int i = 0; i < dataCore.activePedestrians.Count; i++)
            {
                PedestrianData ped = dataCore.activePedestrians[i];
                float laneLen = LaneRegistry.Instance.GetPedestrianLaneLength(ped.splineId);
                bool isCwLane = _crosswalkLaneIds.Contains(ped.splineId);

                if (isCwLane)
                {
                    //crosswalk peds are permanent idle props — never move, always waiting.
                    ped.isWaiting = true;
                    ped.currentSpeed = 0f;
                }
                else
                {
                    //sidewalk peds walk continuously.
                    ped.isWaiting = false;
                    ped.currentSpeed = ped.desiredSpeed;

                    ped.distanceAlongSpline += ped.currentSpeed * dt;
                    if (ped.distanceAlongSpline >= laneLen)
                        ped.distanceAlongSpline %= laneLen;
                }
                
                ped.position = LaneRegistry.Instance.GetPedestrianWorldPosition(ped.splineId, ped.distanceAlongSpline);
                Vector3 fwd = LaneRegistry.Instance.GetPedestrianWorldDirection(ped.splineId, ped.distanceAlongSpline);
                if (fwd != Vector3.zero) ped.rotation = Quaternion.LookRotation(fwd);

                dataCore.activePedestrians[i] = ped;
            }
        }

        /// <summary>
        /// Returns true when the pedestrian lane currently has the Walk signal.
        /// Queries IntersectionController.IsPedestrianLaneOpen which is set when
        /// the phase containing this pedestrian spline is Green.
        /// Falls back to checking the crosswalk's conflict vehicle lane light state
        /// for scenes where pedestrianCrossings has not been configured yet.
        /// </summary>
        private bool IsPedestrianLaneOpen(int pedestrianLaneId)
        {
            //explicit pedestrian spline phase assignment
            bool anyConfigured = false;
            foreach (var ic in _intersections)
                foreach (var phase in ic.phases)
                    if (phase.pedestrianCrossings != null && phase.pedestrianCrossings.Count > 0)
                    { anyConfigured = true; break; }

            if (anyConfigured)
            {
                foreach (var ic in _intersections)
                    if (ic.IsPedestrianLaneOpen(pedestrianLaneId))
                        return true;
                return false;
            }

            //infer from conflict vehicle lane light state
            if (LaneRegistry.Instance.crosswalks != null)
            {
                foreach (var cw in LaneRegistry.Instance.crosswalks)
                {
                    if (cw.pedestrianLaneId != pedestrianLaneId) continue;
                    foreach (var ic in _intersections)
                        foreach (var light in ic.ManagedLights)
                            if (light.vehicleLaneId == cw.vehicleLaneId)
                                return light.state == TrafficLightState.Red;
                }
            }

            return false;
        }

        private void ProcessTraffic(float dt)
        {
            //build cross-spline look-ahead table
            _laneNearestToStart.Clear();
            for (int vi = 0; vi < dataCore.activeVehicles.Count; vi++)
            {
                VehicleData v = dataCore.activeVehicles[vi];
                if (!_laneNearestToStart.TryGetValue(v.splineId, out float existing)
                    || v.distanceAlongSpline < existing)
                {
                    _laneNearestToStart[v.splineId] = v.distanceAlongSpline;
                }
            }

            foreach (var kvp in _vehiclesByLane)
            {
                int laneId = kvp.Key;
                List<int> vehiclesInLane = kvp.Value;
                if (vehiclesInLane.Count == 0) continue;

                LaneSimData currentLane = LaneRegistry.Instance.vehicleLaneData[laneId];

                vehiclesInLane.Sort(_vehicleSortComparison);

                for (int i = 0; i < vehiclesInLane.Count; i++)
                {
                    int currentIdx = vehiclesInLane[i];
                    VehicleData vehicle = dataCore.activeVehicles[currentIdx];
                    vehicle.justRespawned = false;

                    float vehicleObstacleDist = float.MaxValue;
                    if (i > 0)
                    {
                        int nextIdx = vehiclesInLane[i - 1];
                        vehicleObstacleDist = dataCore.activeVehicles[nextIdx].distanceAlongSpline - vehicle.distanceAlongSpline;
                    }

                    // A merging car straddles two lanes
                    if (_mergingVehicleIdx != -1 && _mergingVehicleIdx != currentIdx)
                    {
                        VehicleData merger = dataCore.activeVehicles[_mergingVehicleIdx];
                        if (merger.isChangingLanes && merger.targetSplineId == laneId)
                        {
                            float mergerGap = merger.targetDistanceAlongSpline - vehicle.distanceAlongSpline;
                            if (mergerGap >= 0f && mergerGap < vehicleObstacleDist)
                                vehicleObstacleDist = mergerGap;
                        }
                    }

                    //cross-spline buffer: check the nearest vehicle at the start of each possible next spline
                    if (currentLane.nextLanes != null)
                    {
                        float distToJunction = currentLane.length - vehicle.distanceAlongSpline;
                        for (int nl = 0; nl < currentLane.nextLanes.Length; nl++)
                        {
                            int nextLaneId = currentLane.nextLanes[nl];
                            if (_laneNearestToStart.TryGetValue(nextLaneId, out float nearestOnNext))
                            {
                                float crossDist = distToJunction + nearestOnNext;
                                if (crossDist < vehicleObstacleDist)
                                    vehicleObstacleDist = crossDist;
                            }
                        }
                    }

                    // keep whichever side is tighter
                    if (vehicle.isChangingLanes)
                    {
                        float forwardNeededToClear = Mathf.Max(0f, LaneChangeClearProgress - vehicle.laneChangeProgress)
                                                     * LaneChangeForwardDistance(vehicle.currentSpeed);
                        if (vehicle.laneChangeProgress >= LaneChangeClearProgress
                            || vehicleObstacleDist - LaneChangeClearMargin >= forwardNeededToClear)
                            vehicleObstacleDist = float.MaxValue;

                        float targetLaneGap = GetGapToNearestAheadInLane(vehicle.targetSplineId, vehicle.targetDistanceAlongSpline, vehicle.vehicleId);
                        if (targetLaneGap < vehicleObstacleDist)
                            vehicleObstacleDist = targetLaneGap;
                    }
                    
                    float environmentObstacleDist = float.MaxValue;
                    
                    //crosswalks
                    if (LaneRegistry.Instance.crosswalks != null)
                    {
                        for (int j = 0; j < LaneRegistry.Instance.crosswalks.Length; j++)
                        {
                            CrosswalkData crosswalk = LaneRegistry.Instance.crosswalks[j];
                            if (crosswalk.vehicleLaneId == laneId && crosswalk.isOccupied)
                            {
                                float crosswalkStopLine = crosswalk.distanceAlongVehicleLane - crosswalk.stopLineOffset;
                                
                                if (vehicle.distanceAlongSpline <= crosswalkStopLine + 1f)
                                {
                                    float distanceToCrosswalk = Mathf.Max(0f, crosswalkStopLine - vehicle.distanceAlongSpline);
                                    if (distanceToCrosswalk < environmentObstacleDist)
                                    {
                                        environmentObstacleDist = distanceToCrosswalk;
                                    }
                                }
                            }
                        }
                    }
                    
                    //traffic lights
                    if (!currentLane.isIntersection)    //don't look ahead if we're currently in an intersection
                    {
                        foreach (var intersection in _intersections)
                        {
                            foreach (var light in intersection.ManagedLights)
                            {
                                if (light.state == TrafficLightState.Red || light.state == TrafficLightState.Yellow)
                                {
                                    bool isCurrentLaneStopLight = light.vehicleLaneId == laneId;
                                    bool isNextLaneStopLight = currentLane.nextLanes != null && System.Array.IndexOf(currentLane.nextLanes, light.vehicleLaneId) != -1;

                                    if (isCurrentLaneStopLight || isNextLaneStopLight)
                                    {
                                        float stopLine = isNextLaneStopLight
                                            ? (currentLane.length - startData.intersectionStopDistance)
                                            : light.stopLineDistance;

                                        if (vehicle.distanceAlongSpline <= stopLine + 1f)
                                        {
                                            float distanceToLight = Mathf.Max(0f, stopLine - vehicle.distanceAlongSpline);
                                            if (distanceToLight < environmentObstacleDist)
                                            {
                                                environmentObstacleDist = distanceToLight;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    float availableSpaceFromVehicles = vehicleObstacleDist == float.MaxValue 
                        ? float.MaxValue 
                        : Mathf.Max(0f, vehicleObstacleDist - startData.bufferDistance);

                    float actualAvailableSpace = Mathf.Min(availableSpaceFromVehicles, environmentObstacleDist);
                    
                    //speed limits
                    float maxAllowedSpeed = currentLane.speedLimit;
                    
                    if (currentLane.nextLanes != null && currentLane.nextLanes.Length > 0)
                    {
                        float distanceToEnd = currentLane.length - vehicle.distanceAlongSpline;
                        if (distanceToEnd < startData.brakingZoneDistance)
                        {
                            float upcomingSpeedLimit = LaneRegistry.Instance.vehicleLaneData[currentLane.nextLanes[0]].speedLimit;
                            
                            //if the next lane is slower, start to slow down smoothly
                            if (upcomingSpeedLimit < maxAllowedSpeed)
                            {
                                float transitionFactor = distanceToEnd / startData.brakingZoneDistance; 
                                maxAllowedSpeed = Mathf.Lerp(upcomingSpeedLimit, currentLane.speedLimit, transitionFactor);
                            }
                        }
                    }
                    
                    float baseTargetSpeed = Mathf.Min(vehicle.desiredSpeed, maxAllowedSpeed);

                    float finalTargetSpeed = baseTargetSpeed;
                    if (actualAvailableSpace < startData.brakingZoneDistance)
                    {
                        float speedFactor = Mathf.Clamp01(actualAvailableSpace / startData.brakingZoneDistance);
                        finalTargetSpeed = baseTargetSpeed * speedFactor;
                    }
                    
                    bool isBraking = finalTargetSpeed < vehicle.currentSpeed;
                    
                    float currentRate;
                    if (isBraking)
                    {
                        currentRate = startData.accelerationRate * 2f; //brake at set rate. Maybe introduce a variable
                    }
                    else
                    {
                        currentRate = startData.accelerationRate * currentLane.accelerationMultiplier;
                        
                        //ease into the gas pedal over the first section of the new lane
                        if (vehicle.distanceAlongSpline < startData.brakingZoneDistance)
                        {
                            float rampFactor = vehicle.distanceAlongSpline / startData.brakingZoneDistance;
                            
                            currentRate *= Mathf.Lerp(0.2f, 1f, rampFactor);
                        }
                    }
                        
                    vehicle.currentSpeed = Mathf.Lerp(vehicle.currentSpeed, finalTargetSpeed, dt * currentRate);
                    vehicle.currentSpeed = Mathf.Min(vehicle.currentSpeed, maxAllowedSpeed);

                    vehicle.isBraking = isBraking;

                    if (vehicle.isBraking
                        && finalTargetSpeed >= vehicle.desiredSpeed
                        && vehicle.currentSpeed - finalTargetSpeed <= BrakeSettleEpsilon)
                    {
                        vehicle.isBraking = false;
                    }

                    float actualMove = Mathf.Min(vehicle.currentSpeed * dt, actualAvailableSpace);
            
                    vehicle.distanceAlongSpline += actualMove;

                    if (vehicle.isChangingLanes)
                    {
                        vehicle.targetDistanceAlongSpline += actualMove;

                        float lcDist = LaneChangeForwardDistance(vehicle.currentSpeed);
                        vehicle.laneChangeProgress += actualMove / Mathf.Max(lcDist, 0.1f);

                        // Snap the last sliver closed if the car comes to rest all but merged.
                        if (vehicle.laneChangeProgress > 0.98f && actualMove <= 0.001f)
                            vehicle.laneChangeProgress = 1f;
                    }
                    
                    // Decide which lane comes next *before* arriving at the junction, so the
                    // blinker logic has something to anticipate. The draw is the same random
                    // pick as before, only taken earlier, so driving behaviour is unchanged.
                    CommitNextLane(ref vehicle, currentLane);

                    if (vehicle.distanceAlongSpline >= currentLane.length)
                    {
                        // The guard in RequestHeroLaneChange reserves laneChangeDistance + bufferDistance
                        // of lane, so a lane change should never still be running here. If one somehow is,
                        // resolve it rather than clearing isChangingLanes and letting the rendered lateral
                        // offset vanish - that discarded up to a lane width in a single frame and is what
                        // the "car jumps the lanes" report was seeing.
                        LaneSimData handoffLane = currentLane;

                        if (vehicle.isChangingLanes)
                        {
                            if (vehicle.laneChangeProgress >= 0.5f
                                && vehicle.targetSplineId >= 0
                                && vehicle.targetSplineId < LaneRegistry.Instance.vehicleLaneData.Length)
                            {
                                // Past halfway the car is visually in the target lane already, so adopt it
                                // and hand off from there. The commitment belonged to the lane we left.
                                LaneSimData targetLane = LaneRegistry.Instance.vehicleLaneData[vehicle.targetSplineId];
                                vehicle.splineId = vehicle.targetSplineId;
                                vehicle.distanceAlongSpline = Mathf.Min(vehicle.targetDistanceAlongSpline, targetLane.length);
                                vehicle.hasCommittedNextLane = false;
                                handoffLane = targetLane;
                            }

                            vehicle.isChangingLanes = false;
                            vehicle.laneChangeProgress = 0f;
                        }

                        if (vehicle.distanceAlongSpline >= handoffLane.length)
                        {
                            if (handoffLane.nextLanes != null && handoffLane.nextLanes.Length > 0)
                            {
                                vehicle.distanceAlongSpline -= handoffLane.length;
                                vehicle.splineId = HasValidCommitment(vehicle)
                                    ? vehicle.committedNextLaneId
                                    : handoffLane.nextLanes[UnityEngine.Random.Range(0, handoffLane.nextLanes.Length)];
                                vehicle.hasCommittedNextLane = false;
                            }
                            else
                            {
                                RespawnVehicle(ref vehicle);
                            }
                        }
                    }
                    
                    UpdateVehicleVisualTransform(ref vehicle);

                    dataCore.activeVehicles[currentIdx] = vehicle;
                }
            }
        }
       
        /// <summary>
        /// Gap from fromDistance to the nearest vehicle ahead on laneId, or float.MaxValue when
        /// the lane ahead is clear.
        /// </summary>
        private float GetGapToNearestAheadInLane(int laneId, float fromDistance, int ignoreVehicleId)
        {
            if (!_vehiclesByLane.TryGetValue(laneId, out List<int> occupants)) return float.MaxValue;

            float best = float.MaxValue;
            for (int i = 0; i < occupants.Count; i++)
            {
                VehicleData other = dataCore.activeVehicles[occupants[i]];
                if (other.vehicleId == ignoreVehicleId) continue;

                float gap = other.distanceAlongSpline - fromDistance;
                if (gap >= 0f && gap < best) best = gap;
            }

            return best;
        }

        /// <summary>
        /// True while the vehicle's stored next-lane pick still belongs to the lane it is on.
        /// Any respawn, redistribution or lane change moves splineId and invalidates it.
        /// </summary>
        private static bool HasValidCommitment(in VehicleData vehicle)
        {
            return vehicle.hasCommittedNextLane && vehicle.committedFromLaneId == vehicle.splineId;
        }

        /// <summary>
        /// Draws the next lane once the vehicle is within the blinker lookahead of the end of
        /// its current lane. Idempotent: the pick is only ever made once per lane.
        /// </summary>
        private void CommitNextLane(ref VehicleData vehicle, in LaneSimData currentLane)
        {
            if (HasValidCommitment(vehicle)) return;
            if (currentLane.nextLanes == null || currentLane.nextLanes.Length == 0) return;

            float leadTime = heroSettings != null ? heroSettings.autoBlinkerLeadTime : 2f;
            float minLead = heroSettings != null ? heroSettings.autoBlinkerMinLeadDistance : 8f;
            float lookahead = Mathf.Max(vehicle.currentSpeed * leadTime, minLead);

            if (currentLane.length - vehicle.distanceAlongSpline > lookahead) return;

            vehicle.committedNextLaneId = currentLane.nextLanes[UnityEngine.Random.Range(0, currentLane.nextLanes.Length)];
            vehicle.committedFromLaneId = vehicle.splineId;
            vehicle.hasCommittedNextLane = true;
        }

        private void UpdateHeroSensors()
        {
            if (heroSensorDataLayer == null || heroSettings == null) return;

            heroSensorDataLayer.detectedVehicles.Clear();
            if (heroSensorDataLayer.detectedPedestrians != null) 
            {
                heroSensorDataLayer.detectedPedestrians.Clear();
            }

            VehicleData hero = default;
            bool heroFound = false;

            for (int i = 0; i < dataCore.activeVehicles.Count; i++)
            {
                if (dataCore.activeVehicles[i].vehicleId == heroSettings.heroVehicleId)
                {
                    hero = dataCore.activeVehicles[i];
                    heroFound = true;
                    break;
                }
            }

            if (!heroFound) return;

            if (heroCarData != null)
            {
                heroCarData.Speed = hero.currentSpeed;
                heroCarData.IsBraking = hero.isBraking;
            }
            
            heroSensorDataLayer.heroWorldPosition = hero.position;
            heroSensorDataLayer.heroWorldRotation = hero.rotation;
            heroSensorDataLayer.heroLaneId = hero.splineId;
            heroSensorDataLayer.heroNextLaneIds = LaneRegistry.Instance.vehicleLaneData[hero.splineId].nextLanes;
            heroSensorDataLayer.heroCommittedNextLaneId = HasValidCommitment(hero) ? hero.committedNextLaneId : -1;

            Quaternion inverseHeroRot = Quaternion.Inverse(hero.rotation);
            float radiusSqr = heroSettings.detectionRadius * heroSettings.detectionRadius;

            //Process Vehicles
            for (int i = 0; i < dataCore.activeVehicles.Count; i++)
            {
                VehicleData targetVehicle = dataCore.activeVehicles[i];
                if (targetVehicle.vehicleId == hero.vehicleId) continue;

                Vector3 offsetToTarget = targetVehicle.position - hero.position;

                if (offsetToTarget.sqrMagnitude <= radiusSqr)
                {
                    Vector3 localPos = inverseHeroRot * offsetToTarget;
                    Quaternion localRot = inverseHeroRot * targetVehicle.rotation;

                    heroSensorDataLayer.detectedVehicles.Add(new RelativeEntityData()
                    {
                        entityId = targetVehicle.vehicleId,
                        vehicleType = targetVehicle.vehicleType,
                        relativePosition = localPos,
                        relativeRotation = localRot,
                        currentSpeed = targetVehicle.currentSpeed,
                        laneId = targetVehicle.splineId
                    });
                }
            }

            //Process Pedestrians
            if (heroSensorDataLayer.detectedPedestrians != null)
            {
                for (int i = 0; i < dataCore.activePedestrians.Count; i++)
                {
                    PedestrianData targetPedestrian = dataCore.activePedestrians[i];
                    Vector3 offsetToTarget = targetPedestrian.position - hero.position;

                    if (offsetToTarget.sqrMagnitude <= radiusSqr)
                    {
                        Vector3 localPos = inverseHeroRot * offsetToTarget;
                        Quaternion localRot = inverseHeroRot * targetPedestrian.rotation;

                        heroSensorDataLayer.detectedPedestrians.Add(new RelativeEntityData()
                        {
                            entityId = targetPedestrian.pedestrianId,
                            relativePosition = localPos,
                            relativeRotation = localRot,
                            currentSpeed = targetPedestrian.currentSpeed,
                            isWaiting = targetPedestrian.isWaiting
                        });
                    }
                }
            }
        }
       
        private void RedistributeVehicles()
        {
            // Find hero position once
            Vector3 heroPos = Vector3.zero;
            for (int i = 0; i < dataCore.activeVehicles.Count; i++)
            {
                if (dataCore.activeVehicles[i].vehicleId == heroSettings.heroVehicleId)
                {
                    heroPos = dataCore.activeVehicles[i].position;
                    break;
                }
            }

            float safeDistSqr = heroSettings.minRespawnDistanceFromHero * heroSettings.minRespawnDistanceFromHero;
            int maxPerSpline  = startData.maxVehiclesPerSpline;

            // Build target lane list: non-intersection lanes with fewer vehicles than the limit,
            // sorted ascending by count so we fill the emptiest lanes first.
            _redistributionTargetLanes.Clear();
            foreach (var kvp in _vehiclesByLane)
            {
                int laneId = kvp.Key;
                if (LaneRegistry.Instance.vehicleLaneData[laneId].isIntersection) continue;
                if (kvp.Value.Count < maxPerSpline)
                    _redistributionTargetLanes.Add(laneId);
            }
            // Also include lanes with no vehicles at all
            for (int laneId = 0; laneId < LaneRegistry.Instance.vehicleLaneData.Length; laneId++)
            {
                if (LaneRegistry.Instance.vehicleLaneData[laneId].isIntersection) continue;
                if (!_vehiclesByLane.ContainsKey(laneId) || _vehiclesByLane[laneId].Count == 0)
                    _redistributionTargetLanes.Add(laneId);
            }

            if (_redistributionTargetLanes.Count == 0) return;

            // Collect indices of vehicles eligible for redistribution:
            // on an overloaded spline AND outside hero detection range.
            _redistributionVehicleQueue.Clear();
            foreach (var kvp in _vehiclesByLane)
            {
                if (kvp.Value.Count <= maxPerSpline) continue;
                List<int> vehiclesInLane = kvp.Value;
                for (int i = 0; i < vehiclesInLane.Count; i++)
                {
                    int idx = vehiclesInLane[i];
                    VehicleData v = dataCore.activeVehicles[idx];
                    if (v.vehicleId == heroSettings.heroVehicleId) continue;
                    Vector3 diff = v.position - heroPos;
                    if (diff.sqrMagnitude >= safeDistSqr)
                        _redistributionVehicleQueue.Add(idx);
                }
            }

            if (_redistributionVehicleQueue.Count == 0) return;

            for (int qi = 0; qi < _redistributionVehicleQueue.Count; qi++)
            {
                int vehicleIdx = _redistributionVehicleQueue[qi];
                VehicleData vehicle = dataCore.activeVehicles[vehicleIdx];

                // Re-check count on vehicle's current lane (may have changed as we redistribute)
                int currentLaneCount = _vehiclesByLane.TryGetValue(vehicle.splineId, out var cl) ? cl.Count : 0;
                if (currentLaneCount <= maxPerSpline) continue;

                // Find the target lane with the fewest vehicles (prefer 0) that has a safe placement spot
                int   bestLane    = -1;
                float bestDist    = -1f;
                int   bestCount   = int.MaxValue;

                for (int ti = 0; ti < _redistributionTargetLanes.Count; ti++)
                {
                    int targetLane = _redistributionTargetLanes[ti];
                    if (targetLane == vehicle.splineId) continue;

                    int targetCount = _vehiclesByLane.TryGetValue(targetLane, out var tl) ? tl.Count : 0;
                    if (targetCount >= maxPerSpline) continue;

                    // Find a safe placement distance on this lane
                    float placementDist = FindSafePlacementDistance(targetLane, startData.bufferDistance * 2f, heroPos, safeDistSqr);
                    if (placementDist < 0f) continue;

                    if (targetCount < bestCount)
                    {
                        bestCount = targetCount;
                        bestLane  = targetLane;
                        bestDist  = placementDist;
                    }
                }

                if (bestLane < 0) continue;

                // Keep _vehiclesByLane counts in sync so subsequent iterations are accurate
                int oldLane = vehicle.splineId;
                if (_vehiclesByLane.TryGetValue(oldLane, out var oldLaneList))
                    oldLaneList.Remove(vehicleIdx);

                // Teleport the vehicle
                vehicle.splineId             = bestLane;
                vehicle.distanceAlongSpline  = bestDist;
                vehicle.isChangingLanes      = false;
                vehicle.laneChangeProgress   = 0f;
                vehicle.position             = LaneRegistry.Instance.GetVehicleWorldPosition(bestLane, bestDist);
                Vector3 dir                  = LaneRegistry.Instance.GetVehicleWorldDirection(bestLane, bestDist);
                vehicle.rotation             = dir != Vector3.zero ? Quaternion.LookRotation(dir) : Quaternion.identity;

                dataCore.activeVehicles[vehicleIdx] = vehicle;

                if (_vehiclesByLane.TryGetValue(bestLane, out var newLaneList))
                    newLaneList.Add(vehicleIdx);
                else
                    _vehiclesByLane[bestLane] = new List<int> { vehicleIdx };
            }
        }

        // Returns a distance along targetLane where the vehicle can be placed with no neighbours
        // within safeGap, and the world position is outside the hero detection sphere.
        // Returns -1 if no safe spot exists.
        private float FindSafePlacementDistance(int targetLane, float safeGap, Vector3 heroPos, float safeDistSqr)
        {
            float laneLength = LaneRegistry.Instance.vehicleLaneData[targetLane].length;

            // Build a sorted list of occupied distances on this lane
            _redistributionLaneSortBuf.Clear();
            if (_vehiclesByLane.TryGetValue(targetLane, out List<int> occupants))
            {
                for (int i = 0; i < occupants.Count; i++)
                    _redistributionLaneSortBuf.Add(dataCore.activeVehicles[occupants[i]].distanceAlongSpline);
                _redistributionLaneSortBuf.Sort();
            }

            // Try the gap before the first vehicle, between consecutive vehicles, and after the last
            // Candidate positions: midpoint of each gap
            float candidate = -1f;

            if (_redistributionLaneSortBuf.Count == 0)
            {
                candidate = laneLength * 0.5f;
            }
            else
            {
                // Gap before first vehicle
                float firstDist = _redistributionLaneSortBuf[0];
                if (firstDist > safeGap * 2f)
                    candidate = firstDist * 0.5f;

                // Gaps between vehicles
                if (candidate < 0f)
                {
                    for (int i = 0; i < _redistributionLaneSortBuf.Count - 1; i++)
                    {
                        float gapStart = _redistributionLaneSortBuf[i];
                        float gapEnd   = _redistributionLaneSortBuf[i + 1];
                        if (gapEnd - gapStart > safeGap * 2f)
                        {
                            candidate = (gapStart + gapEnd) * 0.5f;
                            break;
                        }
                    }
                }

                // Gap after last vehicle
                if (candidate < 0f)
                {
                    float lastDist = _redistributionLaneSortBuf[_redistributionLaneSortBuf.Count - 1];
                    if (laneLength - lastDist > safeGap * 2f)
                        candidate = (lastDist + laneLength) * 0.5f;
                }
            }

            if (candidate < 0f) return -1f;

            // Confirm the placement point itself is outside hero detection range
            Vector3 candidateWorldPos = LaneRegistry.Instance.GetVehicleWorldPosition(targetLane, candidate);
            Vector3 diff = candidateWorldPos - heroPos;
            if (diff.sqrMagnitude < safeDistSqr) return -1f;

            return candidate;
        }

        private void RespawnVehicle(ref VehicleData vehicle)
        {
            Vector3 heroPos = Vector3.zero;
            for (int i = 0; i < dataCore.activeVehicles.Count; i++)
            {
                if (dataCore.activeVehicles[i].vehicleId == heroSettings.heroVehicleId)
                {
                    heroPos = dataCore.activeVehicles[i].position;
                    break;
                }
            }
            
            _validRespawnLanes.Clear();
            for (int i = 0; i < LaneRegistry.Instance.vehicleLaneData.Length; i++)
            {
                if (i == vehicle.splineId) continue;
                
                if (LaneRegistry.Instance.vehicleLaneData[i].isIntersection) continue;

                Vector3 startPos = LaneRegistry.Instance.GetVehicleWorldPosition(i, 0f);
                 
                if (Vector3.Distance(startPos, heroPos) >= heroSettings.minRespawnDistanceFromHero)
                {
                    if (IsLaneGapSafe(i, 0f, startData.bufferDistance * 2f))
                    {
                        _validRespawnLanes.Add(i);
                    }
                }
            }
            
            if (_validRespawnLanes.Count > 0)
            {
                int randomLane = _validRespawnLanes[UnityEngine.Random.Range(0, _validRespawnLanes.Count)];
                vehicle.splineId = randomLane;
                vehicle.distanceAlongSpline = 0f; 
                vehicle.currentSpeed = vehicle.desiredSpeed;
                
                vehicle.isChangingLanes = false;
                vehicle.laneChangeProgress = 0f;
                
                vehicle.position = LaneRegistry.Instance.GetVehicleWorldPosition(randomLane, 0f);
                Vector3 dir = LaneRegistry.Instance.GetVehicleWorldDirection(randomLane, 0f);
                vehicle.rotation = dir != Vector3.zero ? Quaternion.LookRotation(dir) : Quaternion.identity;
                
                vehicle.justRespawned = true;
            }
            else 
            {
                vehicle.distanceAlongSpline = 0f;
                vehicle.isChangingLanes = false;
                vehicle.laneChangeProgress = 0f;
                
                vehicle.position = LaneRegistry.Instance.GetVehicleWorldPosition(vehicle.splineId, 0f);
                Vector3 dir = LaneRegistry.Instance.GetVehicleWorldDirection(vehicle.splineId, 0f);
                vehicle.rotation = dir != Vector3.zero ? Quaternion.LookRotation(dir) : Quaternion.identity;
                
                vehicle.justRespawned = true;
            }
        }

        private bool IsLaneGapSafe(int targetLaneId, float projectedDistance, float safeGap)
        {
            if (!_vehiclesByLane.ContainsKey(targetLaneId)) return true; 

            List<int> carsInTargetLane = _vehiclesByLane[targetLaneId];
            
            for (int i = 0; i < carsInTargetLane.Count; i++)
            {
                VehicleData targetCar = dataCore.activeVehicles[carsInTargetLane[i]];
                float distDiff = Mathf.Abs(targetCar.distanceAlongSpline - projectedDistance);

                if (distDiff < safeGap)
                {
                    return false; 
                }
            }
            return true; 
        }
        
        private void RequestHeroLaneChange(bool toLeft)
        {
            int heroIdx = -1;
            for (int i = 0; i < dataCore.activeVehicles.Count; i++)
            {
                if (dataCore.activeVehicles[i].vehicleId == heroSettings.heroVehicleId)
                {
                    heroIdx = i;
                    break;
                }
            }

            if (heroIdx == -1) return;
    
            VehicleData hero = dataCore.activeVehicles[heroIdx];
            if (hero.isChangingLanes) return;

            // CM-1443: a stationary (or crawling) car cannot merge. Lane-change progress is now
            // distance-based so a stopped car can no longer slide sideways on its own; this gate
            // remains so the request is refused up front instead of stalling mid-manoeuvre.
            if (hero.currentSpeed <= startData.minLaneChangeSpeed)
            {
                OnLaneChangeBlockedWarning?.Invoke(); //too slow to change lanes
                Debug.Log("Lane change blocked: moving too slowly!");
                return;
            }

            LaneSimData heroLaneData = LaneRegistry.Instance.vehicleLaneData[hero.splineId];
            
            // A lane change consumes laneChangeDistance meters of forward travel. Reserve that plus a
            // buffer so the manoeuvre always completes before the junction - the old fixed
            // bufferDistance * 3 covered only a fraction of it, so requests that could never finish
            // were approved and then discarded at the junction, snapping the car back a lane width.
            float requiredLaneAhead = startData.laneChangeDistance + startData.bufferDistance;
            if ((heroLaneData.length - hero.distanceAlongSpline) < requiredLaneAhead)
            {
                OnLaneChangeBlockedWarning?.Invoke(); //not enough lane left to complete the change
                Debug.Log("Lane change blocked: too close to the end of the lane!");
                return;
            }
    
            //grab adjacency directly from the struct array
            int targetId = toLeft ? heroLaneData.adjacentLanes.leftLaneId : heroLaneData.adjacentLanes.rightLaneId;
            
            if (targetId != -1)
            {
                float projectedDist = LaneRegistry.Instance.GetEquivalentDistanceOnSpline(targetId, hero.position);
                
                if (IsLaneGapSafe(targetId, projectedDist, startData.mergeSafetyGap))
                {
                    hero.isChangingLanes = true;
                    hero.targetSplineId = targetId;
                    hero.targetDistanceAlongSpline = projectedDist;
                    hero.laneChangeProgress = 0f;

                    dataCore.activeVehicles[heroIdx] = hero;

                    // Only a lane change that actually starts signals driver intent. The blocked
                    // paths below deliberately leave this alone so the blinker stays off.
                    if (heroSensorDataLayer != null)
                    {
                        heroSensorDataLayer.heroLaneChangeWasToLeft = toLeft;
                        heroSensorDataLayer.heroLaneChangeCount++;
                    }

                    Debug.Log($"Hero changing to lane: {targetId}");
                }
                else
                {
                    OnLaneChangeBlockedWarning?.Invoke(); //blocked by another car
                    Debug.Log("Lane change blocked by traffic!");
                }
            }
            else
            {
                OnLaneChangeBlockedWarning?.Invoke(); //no open lane in that direction
                Debug.Log("Lane change blocked: Edge of the road!");
            }
        }
        
        private void AdjustHeroSpeed(float speedDelta)
        {
            int heroIdx = -1;
            for (int i = 0; i < dataCore.activeVehicles.Count; i++)
            {
                if (dataCore.activeVehicles[i].vehicleId == heroSettings.heroVehicleId)
                {
                    heroIdx = i;
                    break;
                }
            }

            if (heroIdx == -1) return;

            VehicleData hero = dataCore.activeVehicles[heroIdx];
            
            hero.desiredSpeed = Mathf.Clamp(hero.desiredSpeed + speedDelta, 0f, startData.maxSpeed * 1.5f);
            
            dataCore.activeVehicles[heroIdx] = hero;
            
            //Debug.Log($"Hero desired speed set to: {hero.desiredSpeed}");
        }
        
        private void UpdateVehicleVisualTransform(ref VehicleData vehicle)
        {
            Vector3 currentPos = LaneRegistry.Instance.GetVehicleWorldPosition(vehicle.splineId, vehicle.distanceAlongSpline);
            Vector3 currentDir = LaneRegistry.Instance.GetVehicleWorldDirection(vehicle.splineId, vehicle.distanceAlongSpline);

            if (vehicle.isChangingLanes)
            {
                Vector3 targetPos = LaneRegistry.Instance.GetVehicleWorldPosition(vehicle.targetSplineId, vehicle.targetDistanceAlongSpline);
                Vector3 targetDir = LaneRegistry.Instance.GetVehicleWorldDirection(vehicle.targetSplineId, vehicle.targetDistanceAlongSpline);
                
                float t = Mathf.SmoothStep(0f, 1f, vehicle.laneChangeProgress);
                
                vehicle.position = Vector3.Lerp(currentPos, targetPos, t);
                
                Quaternion currentRot = currentDir != Vector3.zero ? Quaternion.LookRotation(currentDir) : Quaternion.identity;
                Quaternion targetRot = targetDir != Vector3.zero ? Quaternion.LookRotation(targetDir) : Quaternion.identity;
                vehicle.rotation = Quaternion.Slerp(currentRot, targetRot, t);
                
                if (vehicle.laneChangeProgress >= 1f)
                {
                    vehicle.splineId = vehicle.targetSplineId;
                    vehicle.distanceAlongSpline = vehicle.targetDistanceAlongSpline;
                    vehicle.isChangingLanes = false;
                    vehicle.laneChangeProgress = 0f;
                }
            }
            else
            {
                vehicle.position = currentPos;
                if (currentDir != Vector3.zero) 
                {
                    vehicle.rotation = Quaternion.LookRotation(currentDir);
                }
            }
        }                                   
    }
}
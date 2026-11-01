using System.Collections.Generic;
using UnityEngine;

namespace HMI_Hub_Template
{
    public class IntersectionController : MonoBehaviour
    {
        [Tooltip("Reference to the global simulation data for stop line distances")]
        public StartingSimulationData simData;

        public List<TrafficPhase> phases = new List<TrafficPhase>();

        private int _currentPhaseIndex = 0;
        private float _phaseTimer = 0f;
        private bool _isYellow = false;

        public List<TrafficLightData> ManagedLights { get; private set; } = new List<TrafficLightData>();

        // Pedestrian lane IDs (indices into LaneRegistry.pedestrianLanes) that
        // currently have the Walk signal.  Updated whenever a phase changes.
        private readonly HashSet<int> _activePedestrianLaneIds = new HashSet<int>();

        /// Returns true when the pedestrian lane with this ID currently has the
        /// Walk signal — meaning its conflicting vehicle traffic is red.
        public bool IsPedestrianLaneOpen(int pedestrianLaneId)
            => _activePedestrianLaneIds.Contains(pedestrianLaneId);

        void Start()
        {
            InitializeLights();

            foreach (var phase in phases)
                foreach (var physicalLight in phase.physicalLights)
                    if (physicalLight != null)
                        physicalLight.SetLightState(TrafficLightState.Red);

            if (phases.Count > 0)
            {
                _currentPhaseIndex = 0;
                _isYellow = false;
                _phaseTimer = phases[0].greenDuration;
                SetPhaseLights(phases[0], TrafficLightState.Green);
            }
        }

        void InitializeLights()
        {
            foreach (var phase in phases)
            {
                foreach (var spline in phase.activeLanes)
                {
                    int laneId = System.Array.IndexOf(LaneRegistry.Instance.vehicleLanes, spline);
                    if (laneId == -1) continue;

                    float length = LaneRegistry.Instance.GetVehicleLaneLength(laneId);
                    float stopOffset = simData != null ? simData.intersectionStopDistance : 10f;

                    ManagedLights.Add(new TrafficLightData
                    {
                        vehicleLaneId    = laneId,
                        stopLineDistance = length - stopOffset,
                        state            = TrafficLightState.Red
                    });
                }
            }
        }

        void Update()
        {
            if (phases.Count == 0) return;

            _phaseTimer -= Time.deltaTime;
            TrafficPhase currentPhase = phases[_currentPhaseIndex];

            if (_phaseTimer <= 0f)
            {
                if (!_isYellow)
                {
                    _isYellow = true;
                    _phaseTimer = currentPhase.yellowDuration;
                    SetPhaseLights(currentPhase, TrafficLightState.Yellow);
                }
                else
                {
                    _isYellow = false;
                    SetPhaseLights(currentPhase, TrafficLightState.Red);

                    _currentPhaseIndex = (_currentPhaseIndex + 1) % phases.Count;
                    currentPhase = phases[_currentPhaseIndex];

                    _phaseTimer = currentPhase.greenDuration;
                    SetPhaseLights(currentPhase, TrafficLightState.Green);
                }
            }
        }

        void SetPhaseLights(TrafficPhase phase, TrafficLightState newState)
        {
            // ── Vehicle lane logical state ────────────────────────────────────
            foreach (var spline in phase.activeLanes)
            {
                int laneId = System.Array.IndexOf(LaneRegistry.Instance.vehicleLanes, spline);
                var light = ManagedLights.Find(l => l.vehicleLaneId == laneId);
                if (light != null) light.state = newState;
            }

            // ── Physical traffic light visuals ────────────────────────────────
            foreach (var physicalLight in phase.physicalLights)
                if (physicalLight != null)
                    physicalLight.SetLightState(newState);

            // ── Pedestrian walk signal ────────────────────────────────────────
            // Green  → open the pedestrian lanes assigned to this phase.
            // Yellow/Red → close them (conflicting vehicle traffic is about to move).
            foreach (var pedSpline in phase.pedestrianCrossings)
            {
                if (pedSpline == null) continue;
                int pedLaneId = System.Array.IndexOf(LaneRegistry.Instance.pedestrianLanes, pedSpline);
                if (pedLaneId == -1) continue;

                if (newState == TrafficLightState.Green)
                    _activePedestrianLaneIds.Add(pedLaneId);
                else
                    _activePedestrianLaneIds.Remove(pedLaneId);
            }
        }
    }
}

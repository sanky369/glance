using UnityEngine;
using UnityEngine.Serialization;

namespace HMI_Hub_Template
{
    public enum VehicleType
    {
        Car,
        Truck
        //Bus,
        //Motorcycle
    }
    
    [System.Serializable]
    public struct VehicleData
    {
        public int vehicleId;
        public VehicleType vehicleType;
        public int splineId;
        public float distanceAlongSpline;
    
        public float currentSpeed;
        public float desiredSpeed;
    
        public Vector3 position;
        public Quaternion rotation;

        [Header("Lane Change Status")]
        public bool isChangingLanes;
        public float laneChangeProgress;
        public int targetSplineId;
        public float targetDistanceAlongSpline;
        
        public bool justRespawned;
        public bool isBraking;

        [Header("Committed Next Lane")]
        // Which lane this vehicle will enter once it reaches the end of the current one.
        // Decided ahead of time (see SimulationSystem.ProcessTraffic) so that blinkers can
        // anticipate a turn instead of learning about it on the frame it happens.
        // Only trust it while committedFromLaneId == splineId — any respawn, redistribution
        // or lane change moves splineId and therefore invalidates the commitment for free.
        public bool hasCommittedNextLane;
        public int committedFromLaneId;
        public int committedNextLaneId;
    }
}

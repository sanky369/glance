using UnityEngine;

namespace HMI_Hub_Template
{
    [CreateAssetMenu(fileName = "StartingData", menuName = "Simulation/Starting Data")]
    public class StartingSimulationData : ScriptableObject
    {
        [Header("Vehicle Spawning")]
        public int vehiclesPerLane = 5;
        public float minSpeed = 10f;
        public float maxSpeed = 15f;
        
        [Header("Pedestrian Spawning")]
        [Tooltip("Number of pedestrians to evenly space along each crosswalk spline (short crossing paths)")]
        public int pedestriansPerCrosswalk = 1;
        [Tooltip("Approximate spacing in meters between pedestrians on long sidewalk paths")]
        public float sidewalkPedSpacing = 8f;
        public float minPedestrianSpeed = 1f;
        public float maxPedestrianSpeed = 2f;
        
        [Header("Traffic Behavior")]
        [Tooltip("Minimum distance in meters between vehicles to prevent collisions")]
        public float bufferDistance = 8f;

        [Tooltip("Distance at which vehicles begin to smooth brake for slower traffic or crosswalks ahead")]
        public float brakingZoneDistance = 15f; 
        
        [Tooltip("How quickly vehicles accelerate and decelerate to reach their target speed")]
        public float accelerationRate = 2f;
        
        [Header("Lane Changing")]
        [Tooltip("Forward distance in meters a lane change consumes. Progress is tied to distance travelled rather than elapsed time, so the manoeuvre always fits inside the end-of-lane guard in RequestHeroLaneChange and cannot advance while the car is stopped.")]
        public float laneChangeDistance = 60f;

        [Tooltip("Target duration of a lane change in seconds")]
        public float laneChangeDuration = 3f;
        
        [Tooltip("Minimum gap required in the target lane before a vehicle will attempt to merge")]
        public float mergeSafetyGap = 12f;

        [Tooltip("Minimum speed the hero must be moving at to start a lane change. At or below this the request is blocked (CM-1443).")]
        public float minLaneChangeSpeed = 2f;
        
        [Tooltip("Distance from the end of an incoming intersection spline to stop the car")]
        public float intersectionStopDistance = 10.0f;

        [Header("Out-of-Sight Redistribution")]
        [Tooltip("Maximum vehicles allowed on a single spline before redistribution kicks in")]
        public int maxVehiclesPerSpline = 6;

        [Tooltip("How often (in seconds) the redistribution check runs. 0 = every frame (not recommended)")]
        public float redistributionInterval = 1f;
    }
}
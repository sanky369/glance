using UnityEngine;

namespace HMI_Hub_Template
{
    public struct LaneSimData
    {
        public float length;       // world-space arc length (used by simulation for speed)
        public float localLength;  // local-space arc length (used by ConvertIndexUnit)
        public float speedLimit;
        public float accelerationMultiplier;
        public int[] nextLanes;
        public AdjacentLanesData adjacentLanes;
        public bool isIntersection;
    }
    
    [System.Serializable]
    public struct AdjacentLanesData
    {
        public int leftLaneId;
        public int rightLaneId;
    }
}




using UnityEngine;
using UnityEngine.Serialization;

namespace HMI_Hub_Template
{
    [System.Serializable]
    public struct PedestrianData
    {
        public int   pedestrianId;
        public int   splineId;
        public float distanceAlongSpline;
        public float currentSpeed;
        /// Walking speed when not waiting — assigned once at spawn and restored
        /// automatically each frame when the crosswalk hold is released.
        public float desiredSpeed;

        /// True while the pedestrian is held at a crosswalk wait line (traffic has right of way).
        /// Consumed by PedestrianView to switch to idle animation.
        public bool isWaiting;

        /// True while the pedestrian is committed to an active road crossing.
        /// Prevents any other crosswalk zone from stopping them mid-road.
        public bool isCrossing;

        //Calculated Transform Data
        public Vector3 position;
        public Quaternion rotation;
    }
}

using System;
using UnityEngine;

namespace HMI_Hub_Template
{
    [Serializable]
    public struct RelativeEntityData
    {
        public int        entityId;
        public VehicleType vehicleType;

        // Values relative to hero
        public Vector3    relativePosition;
        public Quaternion relativeRotation;

        public float currentSpeed;

        // Lane the entity currently occupies (splineId from simulation).
        public int laneId;

        // Pedestrian state — drives idle/walk animation in PedestrianView.
        public bool isWaiting;
    }
}

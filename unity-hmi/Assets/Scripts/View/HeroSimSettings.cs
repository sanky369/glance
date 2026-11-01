using System;
using UnityEngine;

namespace HMI_Hub_Template
{
    [CreateAssetMenu(fileName = "HeroSimSettings", menuName = "HMI/Hero Sim Settings")]
    public class HeroSimSettings : ScriptableObject
    {
        [Header("Targeting")]
        public int heroVehicleId = 1;

        [Header("Camera Tracking")]
        public Vector3 cameraOffset = new Vector3(0f, 4f, -10f);
        public float cameraTilt;
        
        [Header("Dynamic Camera Speeds")]
        public float minimumPositionLerpSpeed = 2f;
        public float maximumPositionLerpSpeed = 10f;
        public float minimumRotationLerpSpeed = 1f;
        public float maximumRotationLerpSpeed = 5f;
        
        [Tooltip("The vehicle speed at which minimum lerp speeds apply.")]
        public float minSpeed = 0f;
        [Tooltip("The vehicle speed at which maximum lerp speeds apply.")]
        public float maxSpeed = 30f;

        [Header("Respawn System")]
        public float minRespawnDistanceFromHero = 150f;
        
        [Header("Sensor System")]
        [Tooltip("Maximum distance the hero car can detect other entities.")]
        public float detectionRadius = 50f;

        [Tooltip("Distance within which a vehicle in the same lane triggers the collision warning highlight.")]
        public float collisionWarningRadius = 25f;

        [Header("Blinkers")]
        [Tooltip("How long a manually triggered blinker stays on, in seconds. Applies to the Q/E keys and to driver-initiated lane changes. Hazards ignore this.")]
        public float manualBlinkerDuration = 5f;

        [Tooltip("How far ahead of a junction the automatic blinker starts, in seconds of travel at the current speed.")]
        public float autoBlinkerLeadTime = 2f;

        [Tooltip("Lower bound for the automatic blinker lookahead, in meters. Keeps the blinker working while the car is stopped or crawling toward a junction.")]
        public float autoBlinkerMinLeadDistance = 8f;

        [Tooltip("Yaw change between the current lane and the next one below which the junction counts as straight, so no automatic blinker. In degrees.")]
        public float autoBlinkerStraightAngle = 15f;

        public event Action<int> HeroVehicleIdChanged; 
        
        private int _oldHeroVehicleId = -1;

        /// <summary>
        /// Calculates the interpolated position and rotation lerp speeds based on the vehicle's current speed.
        /// </summary>
        public void GetCurrentLerpSpeeds(float currentVehicleSpeed, out float currentPosLerp, out float currentRotLerp)
        {
            float t = Mathf.InverseLerp(minSpeed, maxSpeed, currentVehicleSpeed);
            
            currentPosLerp = Mathf.Lerp(minimumPositionLerpSpeed, maximumPositionLerpSpeed, t);
            currentRotLerp = Mathf.Lerp(minimumRotationLerpSpeed, maximumRotationLerpSpeed, t);
        }

        private void OnValidate()
        {
            if (_oldHeroVehicleId != heroVehicleId)
            {
                HeroVehicleIdChanged?.Invoke(heroVehicleId);
                _oldHeroVehicleId = heroVehicleId;
            }
        }
    }
}
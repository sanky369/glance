using System.Collections.Generic;
using UnityEngine;

namespace HMI_Hub_Template
{
    [CreateAssetMenu(fileName = "HeroSensorDataLayer", menuName = "HMI/Hero Sensor Data Layer")]
    public class HeroSensorDataLayer : ScriptableObject
    {
        public Vector3 heroWorldPosition;
        public Quaternion heroWorldRotation;

        // Hero's current lane and the lanes it connects to (for end-of-lane lookahead).
        public int heroLaneId;
        public int[] heroNextLaneIds;

        // The lane the hero has already committed to entering at the end of heroLaneId,
        // or -1 when the commitment has not been made yet. Lets the blinker logic know
        // which way the road turns before the hero gets there.
        public int heroCommittedNextLaneId = -1;

        // Bumped once per lane change the hero actually starts. Blocked requests do not
        // bump it, so a listener can treat every increment as "the driver really moved over".
        public int heroLaneChangeCount;
        public bool heroLaneChangeWasToLeft;

        public List<RelativeEntityData> detectedVehicles = new List<RelativeEntityData>();
        
        public List<RelativeEntityData> detectedPedestrians = new List<RelativeEntityData>();

        private void OnEnable()
        {
            detectedVehicles.Clear();   //prevent ghost data from prior runs or settings on start
        }
    }
}
using System.Collections.Generic;
using RoadSystem;
using UnityEngine;
using UnityEngine.Splines;

namespace HMI_Hub_Template
{
    [System.Serializable]
    public class TrafficPhase
    {
        public string phaseName = "North/South";
        public float greenDuration = 10f;
        public float yellowDuration = 3f;
        public List<SplineContainer> activeLanes = new List<SplineContainer>();
        
        public List<SplineContainer> pedestrianCrossings = new List<SplineContainer>();

        [Header("Visuals")]
        [Tooltip("The physical traffic light prefabs in the scene assigned to this phase")]
        public List<TrafficLightView> physicalLights = new List<TrafficLightView>();
    }
}

using UnityEngine;

namespace HMI_Hub_Template
{
    public class TrafficLightView : MonoBehaviour
    {
        [Header("Light Objects")]
        [Tooltip("The child GameObject representing the red light")]
        public GameObject redLight;
        
        [Tooltip("The child GameObject representing the yellow light")]
        public GameObject yellowLight;
        
        [Tooltip("The child GameObject representing the green light")]
        public GameObject greenLight;
        
        public void SetLightState(TrafficLightState state)
        {
            if (redLight != null) redLight.SetActive(state == TrafficLightState.Red);
            if (yellowLight != null) yellowLight.SetActive(state == TrafficLightState.Yellow);
            if (greenLight != null) greenLight.SetActive(state == TrafficLightState.Green);
        }
    }
}
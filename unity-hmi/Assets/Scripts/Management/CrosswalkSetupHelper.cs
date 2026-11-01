using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics; 

namespace HMI_Hub_Template
{
    [ExecuteInEditMode] 
    public class CrosswalkSetupHelper : MonoBehaviour
    {
        [Header("Crosswalk Settings")]
        public string crosswalkName = "New Crosswalk";
        [Tooltip("How close a pedestrian needs to be to trigger the crosswalk")]
        public float triggerRadius = 2f;
        
        [Tooltip("Distance before the crosswalk center where the vehicle should stop")]
        public float stopLineOffset = 4f;

        [Header("Assign Splines")]
        public SplineContainer vehicleSpline;
        public SplineContainer pedestrianSpline;

        [Header("Calculated Distances (Auto-Updated)")]
        public float vehicleDistance;
        public float pedestrianDistance;

        void Update()
        {
            if (Application.isPlaying) return;

            if (vehicleSpline != null)
            {
                SplineUtility.GetNearestPoint(vehicleSpline.Spline, 
                    vehicleSpline.transform.InverseTransformPoint(transform.position), 
                    out float3 nearestPoint, out float t);
                
                vehicleDistance = t * vehicleSpline.CalculateLength();
            }

            if (pedestrianSpline != null)
            {
                SplineUtility.GetNearestPoint(pedestrianSpline.Spline, 
                    pedestrianSpline.transform.InverseTransformPoint(transform.position), 
                    out float3 nearestPoint, out float t);
                
                pedestrianDistance = t * pedestrianSpline.CalculateLength();
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawSphere(transform.position, triggerRadius); 
        }
    }
}
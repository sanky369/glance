using UnityEngine;

public class RoadSplineTagComponent : MonoBehaviour
{
    //this component exists to tag a GameObject as containing Road Spline Data
    //modified to add data to be used in the simulation to set speed limits for areas
    [Tooltip("Absolute speed limit for this specific section of road.")]
    public float speedLimit = 35f; 
    
    [Tooltip("Multiplier for vehicle acceleration (e.g., 0.5 means vehicles accelerate at half rate here).")]
    public float accelerationMultiplier = 1.0f;

    [Tooltip("Should be true if the spline is in an intersection")]
    public bool isIntersection;
}

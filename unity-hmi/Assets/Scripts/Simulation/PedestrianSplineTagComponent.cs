using UnityEngine;

public class PedestrianSplineTagComponent : MonoBehaviour
{
    [Tooltip("Check this for short road-crossing splines. " +
             "Pedestrians on these lanes stop and idle when traffic has right of way. " +
             "Leave unchecked for long sidewalk/path splines that always walk.")]
    public bool isCrosswalk = false;
}

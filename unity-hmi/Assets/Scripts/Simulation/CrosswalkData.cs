namespace HMI_Hub_Template
{
    [System.Serializable]
    public class CrosswalkData
    {
        public string crosswalkName = "New Crosswalk";
        
        public int vehicleLaneId;
        public float distanceAlongVehicleLane;
        
        public int pedestrianLaneId;
        public float distanceAlongPedestrianLane;
        
        public float triggerRadius = 2f;

        public float stopLineOffset = 4f; 
        
        public bool isOccupied; 
    }
}
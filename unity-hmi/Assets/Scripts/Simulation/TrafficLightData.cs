public enum TrafficLightState { Green, Yellow, Red }

[System.Serializable]
public class TrafficLightData
{
    public int vehicleLaneId;
    public float stopLineDistance;
    public TrafficLightState state = TrafficLightState.Red;
}
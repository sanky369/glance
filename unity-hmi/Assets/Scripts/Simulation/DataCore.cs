using System.Collections.Generic;
using HMI_Hub_Template;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "DataCore", menuName = "Simulation/Data Core")]
public class DataCore : ScriptableObject
{
    // The Simulation System writes to this, View Systems read from it.
    public List<VehicleData> activeVehicles = new List<VehicleData>();

    public List<PedestrianData> activePedestrians = new List<PedestrianData>();

    private void OnDisable()
    {
        activeVehicles.Clear();
        activePedestrians.Clear();
    }
}
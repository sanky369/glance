using System.Collections.Generic;
using UnityEngine;

namespace HMI_Hub_Template
{
    [System.Serializable]
    public struct VehiclePrefabMapping
    {
        public VehicleType type;
        public VehicleView prefab;
    }

    public class ViewSystem : MonoBehaviour
    {
        public DataCore dataCore;
        
        [Header("Prefabs")]
        [Tooltip("Map your VehicleType enums to specific prefabs here")]
        public VehiclePrefabMapping[] vehiclePrefabs;
        public PedestrianView pedestrianPrefab;
        
        private Dictionary<VehicleType, VehicleView> _prefabLookup;
        
        private Dictionary<int, VehicleView> _activeVehicleViews = new Dictionary<int, VehicleView>();
        private Dictionary<int, PedestrianView> _activePedestrianViews = new Dictionary<int, PedestrianView>();

        void Awake()
        {
            _prefabLookup = new Dictionary<VehicleType, VehicleView>();
            foreach (var mapping in vehiclePrefabs)
            {
                if (!_prefabLookup.ContainsKey(mapping.type))
                {
                    _prefabLookup.Add(mapping.type, mapping.prefab);
                }
            }
        }

        void Update()
        {
            SyncVehicles();
            SyncPedestrians();
        }

        private void SyncVehicles()
        {
            HashSet<int> processedIds = new HashSet<int>();

            for (int i = 0; i < dataCore.activeVehicles.Count; i++)
            {
                VehicleData vData = dataCore.activeVehicles[i];
                processedIds.Add(vData.vehicleId);
                
                if (!_activeVehicleViews.ContainsKey(vData.vehicleId))
                {
                    if (_prefabLookup.TryGetValue(vData.vehicleType, out VehicleView prefabToSpawn))
                    {
                        VehicleView newView = Instantiate(prefabToSpawn, vData.position, vData.rotation, transform);
                        newView.gameObject.name = $"{vData.vehicleType}_{vData.vehicleId}";
                        _activeVehicleViews.Add(vData.vehicleId, newView);
                    }
                    else
                    {
                        Debug.LogWarning($"No prefab assigned for VehicleType: {vData.vehicleType}");
                        continue;
                    }
                }
                
                _activeVehicleViews[vData.vehicleId].UpdateData(vData);
            }
            
            List<int> idsToRemove = new List<int>();
            foreach (var kvp in _activeVehicleViews)
            {
                if (!processedIds.Contains(kvp.Key))
                {
                    Destroy(kvp.Value.gameObject);
                    idsToRemove.Add(kvp.Key);
                }
            }

            foreach (int id in idsToRemove)
            {
                _activeVehicleViews.Remove(id);
            }
        }

        private void SyncPedestrians()
        {
            HashSet<int> processedIds = new HashSet<int>();

            for (int i = 0; i < dataCore.activePedestrians.Count; i++)
            {
                PedestrianData pData = dataCore.activePedestrians[i];
                processedIds.Add(pData.pedestrianId);

                if (!_activePedestrianViews.ContainsKey(pData.pedestrianId))
                {
                    if (pedestrianPrefab != null)
                    {
                        PedestrianView newView = Instantiate(pedestrianPrefab, pData.position, pData.rotation, transform);
                        newView.gameObject.name = $"Pedestrian_{pData.pedestrianId}";
                        _activePedestrianViews.Add(pData.pedestrianId, newView);
                    }
                }

                if (_activePedestrianViews.ContainsKey(pData.pedestrianId))
                {
                    _activePedestrianViews[pData.pedestrianId].UpdateData(pData);
                }
            }

            // Cleanup pedestrian views
            List<int> idsToRemove = new List<int>();
            foreach (var kvp in _activePedestrianViews)
            {
                if (!processedIds.Contains(kvp.Key))
                {
                    Destroy(kvp.Value.gameObject);
                    idsToRemove.Add(kvp.Key);
                }
            }

            foreach (int id in idsToRemove)
            {
                _activePedestrianViews.Remove(id);
            }
        }
    }
}
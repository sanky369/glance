using System.Collections.Generic;
using UnityEngine;

namespace HMI_Hub_Template
{
    [System.Serializable]
    public struct VehicleTypeMapping
    {
        public VehicleType type;
        public GameObject prefab;
    }

    public class HeroSensorViewSystem : MonoBehaviour
    {
        [Header("Data Source")]
        public HeroSensorDataLayer sensorData;
        public HeroSimSettings simSettings;
        
        [Header("Hero Settings")]
        public GameObject heroPrefab;
        private GameObject _heroInstance;
        public Transform HeroTransform => _heroInstance != null ? _heroInstance.transform : null;

        [Header("Detected Prefab Mappings")]
        public List<VehicleTypeMapping> vehiclePrefabs;
        
        public GameObject pedestrianPrefab;

        private Dictionary<int, GameObject> _activeVisuals = new Dictionary<int, GameObject>();
        private Dictionary<int, VehicleType> _activeVisualTypes = new Dictionary<int, VehicleType>();
        private Dictionary<VehicleType, Queue<GameObject>> _pools = new Dictionary<VehicleType, Queue<GameObject>>();
        private Dictionary<VehicleType, GameObject> _prefabMap = new Dictionary<VehicleType, GameObject>();

        private Dictionary<int, GameObject> _activePedestrianVisuals = new Dictionary<int, GameObject>();
        private Queue<GameObject> _pedestrianPool = new Queue<GameObject>();

        [Header("Collision Highlight")]
        [Tooltip("Emission color set on the body renderers of vehicles in range and in the hero's lane.")]
        [ColorUsage(true, true)]
        public Color collisionEmissionColor = Color.red;
        [Tooltip("Speed at which the emission color lerps on and off.")]
        public float emissionLerpSpeed = 5f;

        private Dictionary<GameObject, GameObject> _boundingBoxCache = new Dictionary<GameObject, GameObject>();

        // Highlight state — 0 = off, 1 = fully on. All colors derived from this single float.
        private Dictionary<GameObject, float> _highlightProgress = new Dictionary<GameObject, float>();

        // Per-visual renderer split cached on first highlight (avoids GetComponentsInChildren every frame).
        private struct RendererSplit
        {
            public Renderer[] body;
            public Renderer[] tires;
        }
        private Dictionary<GameObject, RendererSplit> _rendererSplitCache = new Dictionary<GameObject, RendererSplit>();

        // Original sharedMaterials per renderer, captured before instancing.
        private Dictionary<Renderer, Material[]> _originalMats = new Dictionary<Renderer, Material[]>();
        // Instanced material arrays per renderer — avoids re-allocating via rend.materials every frame.
        private Dictionary<Renderer, Material[]> _instancedMats = new Dictionary<Renderer, Material[]>();

        private readonly HashSet<int> _detectedVehicleIds = new HashSet<int>();
        private readonly HashSet<int> _detectedPedestrianIds = new HashSet<int>();
        private readonly List<int> _idsToRemove = new List<int>();

        void Awake()
        {
            foreach (var mapping in vehiclePrefabs)
            {
                if (mapping.prefab != null)
                {
                    _prefabMap[mapping.type] = mapping.prefab;
                    _pools[mapping.type] = new Queue<GameObject>();
                }
            }
        }

        void Update()
        {
            if (sensorData == null) return;
            
            if (heroPrefab != null)
            {
                if (_heroInstance == null)
                {
                    _heroInstance = Instantiate(heroPrefab, transform);
                }
                
                _heroInstance.SetActive(true);
                _heroInstance.transform.position = sensorData.heroWorldPosition;
                _heroInstance.transform.rotation = sensorData.heroWorldRotation;
            }
            
            SyncVehicles();
            SyncPedestrians();
        }

        private void SyncVehicles()
        {
            _detectedVehicleIds.Clear();

            float sqrCollisionRadius = simSettings != null ? (simSettings.collisionWarningRadius * simSettings.collisionWarningRadius) : 0f;

            for (int i = 0; i < sensorData.detectedVehicles.Count; i++)
            {
                RelativeEntityData data = sensorData.detectedVehicles[i];
                _detectedVehicleIds.Add(data.entityId);

                GameObject visual;
                
                if (!_activeVisuals.TryGetValue(data.entityId, out visual))
                {
                    if (_pools.ContainsKey(data.vehicleType) && _pools[data.vehicleType].Count > 0)
                    {
                        visual = _pools[data.vehicleType].Dequeue();
                        visual.SetActive(true);
                    }
                    else if (_prefabMap.ContainsKey(data.vehicleType))
                    {
                        visual = Instantiate(_prefabMap[data.vehicleType], transform);
                    }
                    else
                    {
                        Debug.LogWarning($"No prefab mapped for VehicleType: {data.vehicleType}");
                        continue; 
                    }

                    _activeVisuals[data.entityId] = visual;
                    _activeVisualTypes[data.entityId] = data.vehicleType;
                }

                if (!_boundingBoxCache.ContainsKey(visual))
                {
                    EdgeLine edgeLine = visual.GetComponentInChildren<EdgeLine>(true);
                    _boundingBoxCache[visual] = edgeLine != null ? edgeLine.gameObject : null;
                }

                visual.transform.position = sensorData.heroWorldPosition + (sensorData.heroWorldRotation * data.relativePosition);
                visual.transform.rotation = sensorData.heroWorldRotation * data.relativeRotation;

                if (simSettings != null)
                {
                    bool inRange = data.relativePosition.sqrMagnitude <= sqrCollisionRadius;
                    bool inFront = data.relativePosition.z > 0f;
                    bool inLane = data.laneId == sensorData.heroLaneId || IsInHeroNextLane(data.laneId);
                    bool highlight = inRange && inFront && inLane;

                    ApplyHighlight(visual, highlight);
                }
            }

            _idsToRemove.Clear();
            foreach (var kvp in _activeVisuals)
            {
                int id = kvp.Key;
                if (!_detectedVehicleIds.Contains(id))
                {
                    GameObject objToPool = kvp.Value;
                    RestoreImmediate(objToPool);
                    objToPool.SetActive(false);

                    VehicleType type = _activeVisualTypes[id];
                    if (_pools.ContainsKey(type))
                        _pools[type].Enqueue(objToPool);

                    _idsToRemove.Add(id);
                }
            }

            foreach (int id in _idsToRemove)
            {
                _activeVisuals.Remove(id);
                _activeVisualTypes.Remove(id);
            }
        }

        private void SyncPedestrians()
        {
            _detectedPedestrianIds.Clear();
            float sqrCollisionRadius = simSettings != null ? (simSettings.collisionWarningRadius * simSettings.collisionWarningRadius) : 0f;

            for (int i = 0; i < sensorData.detectedPedestrians.Count; i++)
            {
                RelativeEntityData data = sensorData.detectedPedestrians[i];
                _detectedPedestrianIds.Add(data.entityId);

                GameObject visual;

                if (!_activePedestrianVisuals.TryGetValue(data.entityId, out visual))
                {
                    if (_pedestrianPool.Count > 0)
                    {
                        visual = _pedestrianPool.Dequeue();
                        visual.SetActive(true);
                    }
                    else if (pedestrianPrefab != null)
                    {
                        visual = Instantiate(pedestrianPrefab, transform);
                    }
                    else
                    {
                        continue;
                    }

                    _activePedestrianVisuals[data.entityId] = visual;
                }

                if (!_boundingBoxCache.ContainsKey(visual))
                {
                    EdgeLine edgeLine = visual.GetComponentInChildren<EdgeLine>(true);
                    _boundingBoxCache[visual] = edgeLine != null ? edgeLine.gameObject : null;
                }

                visual.transform.position = sensorData.heroWorldPosition + (sensorData.heroWorldRotation * data.relativePosition);
                visual.transform.rotation = sensorData.heroWorldRotation * data.relativeRotation;

                // Drive animation via PedestrianView if present.
                var pedView = visual.GetComponent<PedestrianView>();
                if (pedView != null)
                {
                    pedView.UpdateData(new PedestrianData
                    {
                        pedestrianId = data.entityId,
                        isWaiting    = data.isWaiting,
                        position     = visual.transform.position,
                        rotation     = visual.transform.rotation
                    });
                }

                if (simSettings != null && _boundingBoxCache.TryGetValue(visual, out GameObject boundingBox) && boundingBox != null)
                {
                    bool inRange = data.relativePosition.sqrMagnitude <= sqrCollisionRadius;
                    if (boundingBox.activeSelf != inRange)
                    {
                        boundingBox.SetActive(inRange);
                    }
                }
            }

            _idsToRemove.Clear();
            foreach (var kvp in _activePedestrianVisuals)
            {
                int id = kvp.Key;
                if (!_detectedPedestrianIds.Contains(id))
                {
                    GameObject objToPool = kvp.Value;
                    objToPool.SetActive(false);
                    _pedestrianPool.Enqueue(objToPool);
                    _idsToRemove.Add(id);
                }
            }

            foreach (int id in _idsToRemove)
            {
                _activePedestrianVisuals.Remove(id);
            }
        }

        private void RestoreImmediate(GameObject visual)
        {
            if (_rendererSplitCache.TryGetValue(visual, out RendererSplit split))
            {
                RestoreRenderers(split.body);
                RestoreRenderers(split.tires);
                _rendererSplitCache.Remove(visual);
            }
            _highlightProgress.Remove(visual);
        }

        private void RestoreRenderers(Renderer[] renderers)
        {
            foreach (Renderer rend in renderers)
            {
                // Restore first: destroying a material still assigned to the renderer
                // would leave the slot empty for a frame.
                if (_originalMats.TryGetValue(rend, out Material[] orig))
                    rend.sharedMaterials = orig;

                DestroyInstancedMats(rend);
                _originalMats.Remove(rend);
            }
        }

        // rend.materials handed us private copies that nothing else references once the
        // renderer points back at the shared assets. Unity does not refcount them, so
        // they have to be destroyed explicitly or they are orphaned for the session.
        private void DestroyInstancedMats(Renderer rend)
        {
            if (!_instancedMats.TryGetValue(rend, out Material[] instanced))
                return;

            for (int m = 0; m < instanced.Length; m++)
                if (instanced[m] != null)
                    Destroy(instanced[m]);

            _instancedMats.Remove(rend);
        }

        private bool IsInHeroNextLane(int laneId)
        {
            int[] next = sensorData.heroNextLaneIds;
            if (next == null) return false;
            for (int i = 0; i < next.Length; i++)
                if (next[i] == laneId) return true;
            return false;
        }

        private RendererSplit BuildRendererSplit(GameObject visual)
        {
            Renderer[] all = visual.GetComponentsInChildren<Renderer>(true);
            var body = new System.Collections.Generic.List<Renderer>(all.Length);
            var tires = new System.Collections.Generic.List<Renderer>(4);
            foreach (Renderer rend in all)
            {
                bool isTire = false;
                Material[] shared = rend.sharedMaterials;
                for (int m = 0; m < shared.Length; m++)
                    if (shared[m] != null && shared[m].name.IndexOf("wheel", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        { isTire = true; break; }
                if (isTire) tires.Add(rend); else body.Add(rend);
            }
            return new RendererSplit { body = body.ToArray(), tires = tires.ToArray() };
        }

        private void EnsureHighlightCached(GameObject visual)
        {
            if (_rendererSplitCache.ContainsKey(visual)) return;

            RendererSplit split = BuildRendererSplit(visual);
            _rendererSplitCache[visual] = split;

            // Capture originals and create instances once.
            CacheRenderers(split.body);
            CacheRenderers(split.tires);
        }

        private void CacheRenderers(Renderer[] renderers)
        {
            foreach (Renderer rend in renderers)
            {
                if (_originalMats.ContainsKey(rend)) continue;
                _originalMats[rend] = rend.sharedMaterials;
                _instancedMats[rend] = rend.materials; // creates instances
            }
        }

        private void ApplyHighlight(GameObject visual, bool highlighted)
        {
            float targetT = highlighted ? 1f : 0f;
            if (!_highlightProgress.TryGetValue(visual, out float currentT))
                currentT = 0f;

            // Skip all work if already at rest and not being triggered.
            if (!highlighted && currentT < 0.001f) return;

            EnsureHighlightCached(visual);

            float nextT = Mathf.Lerp(currentT, targetT, Time.deltaTime * emissionLerpSpeed);
            _highlightProgress[visual] = nextT;

            RendererSplit split = _rendererSplitCache[visual];

            Color emissionColor = Color.Lerp(Color.black, collisionEmissionColor, nextT);
            Color bodyColor = new Color(Mathf.Clamp01(emissionColor.r), Mathf.Clamp01(emissionColor.g), Mathf.Clamp01(emissionColor.b), 1f);
            Color tireBaseColor = Color.Lerp(Color.white, bodyColor, nextT);

            foreach (Renderer rend in split.body)
                if (_instancedMats.TryGetValue(rend, out Material[] mats))
                    for (int m = 0; m < mats.Length; m++)
                    {
                        mats[m].SetColor("_emmisson", emissionColor);
                        mats[m].SetColor("_Color", bodyColor);
                    }

            foreach (Renderer rend in split.tires)
                if (_instancedMats.TryGetValue(rend, out Material[] mats))
                    for (int m = 0; m < mats.Length; m++)
                    {
                        mats[m].EnableKeyword("_EMISSION");
                        mats[m].SetColor("_EmissionColor", emissionColor);
                        mats[m].SetColor("_BaseColor", tireBaseColor);
                    }

            if (!highlighted && nextT < 0.001f)
                RestoreImmediate(visual);
        }

        void OnDisable()
        {
            if (_heroInstance != null)
            {
                _heroInstance.SetActive(false);
            }

            foreach (var kvp in _activeVisuals)
            {
                int id = kvp.Key;
                GameObject obj = kvp.Value;
                VehicleType type = _activeVisualTypes[id];

                RestoreImmediate(obj);
                obj.SetActive(false);
                if (_pools.ContainsKey(type))
                {
                    _pools[type].Enqueue(obj);
                }
            }
            _activeVisuals.Clear();
            _activeVisualTypes.Clear();

            foreach (var kvp in _activePedestrianVisuals)
            {
                GameObject obj = kvp.Value;
                obj.SetActive(false);
                _pedestrianPool.Enqueue(obj);
            }
            _activePedestrianVisuals.Clear();

            // Renderers that never went through RestoreImmediate still hold instances,
            // and Clear() on its own would drop the last handle to them.
            foreach (var kvp in _instancedMats)
            {
                Material[] instanced = kvp.Value;
                for (int m = 0; m < instanced.Length; m++)
                    if (instanced[m] != null)
                        Destroy(instanced[m]);
            }

            _boundingBoxCache.Clear();
            _highlightProgress.Clear();
            _rendererSplitCache.Clear();
            _originalMats.Clear();
            _instancedMats.Clear();
        }
    }
}
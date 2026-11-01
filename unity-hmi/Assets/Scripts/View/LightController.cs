using System.Collections.Generic;
using Modules;
using UnityEngine;
using UnityEngine.Serialization;

namespace HMI_Hub_Template
{
    public class LightController : MonoBehaviour
    {
        [Header("Car Data")]
        [SerializeField] private CarData _carData;

        [Header("Blinker Renderers")]
        [Tooltip("Renderer(s) for the left turn-signal mesh(es).")]
        [SerializeField] private Renderer[] _leftBlinkerRenderers;
        [Tooltip("Renderer(s) for the right turn-signal mesh(es).")]
        [SerializeField] private Renderer[] _rightBlinkerRenderers;
        [Tooltip("Material slot index on each blinker renderer that is the lens.")]
        [SerializeField] private int _blinkerMaterialIndex;

        [Header("Brake Light Renderers")]
        [Tooltip("Renderer(s) for the rear brake-light mesh(es).")]
        [SerializeField] private Renderer[] _brakeLightRenderers;
        [Tooltip("Material slot index on each brake renderer that is the lens.")]
        [SerializeField] private int _brakeMaterialIndex;

        [Header("Emission Colors")]
        [Tooltip("Color written to the '_emmision' property on the blinker materials when flashing.")]
        [ColorUsage(true, true)]
        [FormerlySerializedAs("_emissionColor")]
        [SerializeField] private Color _blinkerEmissionColor = new Color(4f, 2f, 0f, 0f);

        [Tooltip("Color written to the '_emmision' property on the brake-light materials while braking.")]
        [ColorUsage(true, true)]
        [SerializeField] private Color _brakeEmissionColor = new Color(4f, 0f, 0f, 0f);

        [Header("Flash Settings")]
        [SerializeField] private float _flashInterval = 0.5f;

        private readonly Dictionary<Renderer, Material> _instancedMaterials = new Dictionary<Renderer, Material>();

        private float _flashTimer;
        private bool _flashOn;
        private CarData.BlinkerState _lastBlinkerState;

        private void Awake()
        {
            InstanceMaterials(_leftBlinkerRenderers, _blinkerMaterialIndex);
            InstanceMaterials(_rightBlinkerRenderers, _blinkerMaterialIndex);
            InstanceMaterials(_brakeLightRenderers, _brakeMaterialIndex);

            // Ensure clean visuals on scene load. The state itself is owned by
            // BlinkerController and SimulationSystem, so this view does not reset it.
            SetEmission(_leftBlinkerRenderers,  false, _blinkerEmissionColor);
            SetEmission(_rightBlinkerRenderers, false, _blinkerEmissionColor);
            SetEmission(_brakeLightRenderers,   false, _brakeEmissionColor);
        }

        private void Update()
        {
            if (_carData == null) return;
            UpdateBlinkers();
            UpdateBrakeLights();
        }

        private void OnDisable()
        {
            SetEmission(_leftBlinkerRenderers, false, _blinkerEmissionColor);
            SetEmission(_rightBlinkerRenderers, false, _blinkerEmissionColor);
            SetEmission(_brakeLightRenderers, false, _brakeEmissionColor);
        }

        private void UpdateBlinkers()
        {
            CarData.BlinkerState state = _carData.CurrentBlinkerState;

            if (state != _lastBlinkerState)
            {
                _flashTimer = 0f;
                _flashOn = state != CarData.BlinkerState.Off;
                _lastBlinkerState = state;
            }

            if (state != CarData.BlinkerState.Off)
            {
                _flashTimer += Time.deltaTime;
                if (_flashTimer >= _flashInterval)
                {
                    _flashTimer -= _flashInterval;
                    _flashOn = !_flashOn;
                }
            }
            else
            {
                _flashOn = false;
            }

            bool leftOn = (state == CarData.BlinkerState.Left || state == CarData.BlinkerState.Hazard) && _flashOn;
            bool rightOn = (state == CarData.BlinkerState.Right || state == CarData.BlinkerState.Hazard) && _flashOn;

            SetEmission(_leftBlinkerRenderers, leftOn, _blinkerEmissionColor);
            SetEmission(_rightBlinkerRenderers, rightOn, _blinkerEmissionColor);
        }

        private void UpdateBrakeLights()
        {
            SetEmission(_brakeLightRenderers, _carData.IsBraking, _brakeEmissionColor);
        }

        private void InstanceMaterials(Renderer[] renderers, int matIndex)
        {
            if (renderers == null) return;
            foreach (var r in renderers)
            {
                if (r == null || _instancedMaterials.ContainsKey(r)) continue;
                var mats = r.materials;
                if (matIndex < 0 || matIndex >= mats.Length) continue;
                _instancedMaterials[r] = mats[matIndex];
                r.materials = mats;
            }
        }

        private void SetEmission(Renderer[] renderers, bool on, Color emissionColor)
        {
            if (renderers == null) return;
            Color color = on ? emissionColor : Color.black;
            foreach (var r in renderers)
            {
                if (r == null || !_instancedMaterials.TryGetValue(r, out Material mat)) continue;
                mat.SetColor("_emmision", color);
            }
        }
    }
}

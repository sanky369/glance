using System;
using UnityEngine;

namespace Modules
{
    /// <summary>
    /// Simulates and computes "dependent" values like battery and consumption,
    /// starting from stats like the current speed.
    /// </summary>
    public class CarDataSimulation : MonoBehaviour
    {
        [Header("Car Data")]
        [SerializeField] private CarData _carData;
        
        private const float regenScale = 80f;              // km/h/s of decel that yields full regen (-1.0)
        private const float batteryDrainCoefficient = 0.05f;
        private const float scaleMultiplier = .7f;

        private float _previousSpeed;

        private void Start()
        {
            SetInitialData();
        }

        private void Update()
        {
            ComputeConsumption();
            UpdateBatteryLevel();
        }

        private void SetInitialData()
        {
            _carData.Speed = 0f;
            _carData.BatteryLevel = 80f;
        }

        private void UpdateBatteryLevel()
        {
            _carData.BatteryLevel -= _carData.Consumption * Time.deltaTime * batteryDrainCoefficient;
        }

        private void ComputeConsumption()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float acceleration = (_carData.Speed - _previousSpeed) / dt; // km/h per second
            float speedFactor = Mathf.Sqrt(Mathf.Clamp01(_carData.Speed / 200f));

            float consumption = acceleration < 0f
                ? Mathf.Max(-1f, acceleration / regenScale)
                : speedFactor;

            _carData.Consumption = consumption * scaleMultiplier;

            _previousSpeed = _carData.Speed;
        }
    }
}
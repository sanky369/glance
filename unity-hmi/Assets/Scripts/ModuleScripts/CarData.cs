using System;
using UnityEditor;
using UnityEngine;

namespace Modules
{
    //[CreateAssetMenu(menuName = "Car Control/Car Data")]
    public class CarData : ScriptableObject
    {
        public const float MaxSpeed = 240f;
        private const int MinTemperature = 15;
        private const int MaxTemperature = 25;
        private const int MaxSeatHeatingLevel = 3;
        private const int MaxAirflowPowerLevel = 10;

        [Header("Engine and Movement")]
        [Range(0f, MaxSpeed), Tooltip("Current speed, in km/h.")] public float Speed;
        [Range(0f, 100f), Tooltip("Battery level, in percentage points.")] public float BatteryLevel;
        // The HMI is automatic-only for now, so the dropdown offers just the selector positions
        // it can display. Second..Sixth stay in the enum for manual shifting later.
        [Tooltip("The currently engaged gear.")]
        [EnumSubset(Gears.Parking, Gears.Reverse, Gears.Neutral, Gears.First_Driving)]
        public Gears CurrentGear;

        [Header("Body")]
        public bool TrunkOpen;
        public bool DriverDoorOpen;
        public bool PassengerDoorOpen;
        
        [Header("Lights")]
        public BlinkerState CurrentBlinkerState;
        [Tooltip("What is currently driving CurrentBlinkerState. Written by BlinkerController; read-only here.")]
        [NonEditable] public BlinkerSource CurrentBlinkerSource;
        [Tooltip("True while the hero car is decelerating (brake lights on).")]
        public bool IsBraking;

        [Header("HVAC")]
        [SerializeField] private bool _airConditioningOn = true;
        [SerializeField, Range(0, MaxAirflowPowerLevel), Tooltip("Fan power level, 0–10 (shared between both sides).")]
        private int _airflowPowerLevel = 5;
        [SerializeField, Range(MinTemperature, MaxTemperature), Tooltip("Driver-side target temperature, in °C.")]
        private int _leftTemperature = 22;
        [SerializeField, Range(MinTemperature, MaxTemperature), Tooltip("Passenger-side target temperature, in °C.")]
        private int _rightTemperature = 22;
        [SerializeField, Range(0, MaxSeatHeatingLevel), Tooltip("Driver-side heated seat level (0 = off).")]
        private int _leftHeatedSeatLevel;
        [SerializeField, Range(0, MaxSeatHeatingLevel), Tooltip("Passenger-side heated seat level (0 = off).")]
        private int _rightHeatedSeatLevel;
        [Tooltip("True while the Auto preset is engaged. Written by AutoButton, which also clears it on any manual HVAC change; read-only here.")]
        [NonEditable, SerializeField] private bool _autoOn;

        [Header("Computed Values")]
        [NonEditable] public float Consumption;

        public event Action AirConditioningChanged;
        public event Action AirflowPowerChanged;
        public event Action TemperaturesChanged;
        public event Action HeatedSeatsChanged;
        public event Action AutoChanged;

        public bool AirConditioningOn
        {
            get => _airConditioningOn;
            set
            {
                if (_airConditioningOn == value) return;
                _airConditioningOn = value;
                AirConditioningChanged?.Invoke();
            }
        }

        public int AirflowPowerLevel
        {
            get => _airflowPowerLevel;
            set
            {
                if (_airflowPowerLevel == value) return;
                _airflowPowerLevel = value;
                AirflowPowerChanged?.Invoke();
            }
        }

        public int LeftTemperature
        {
            get => _leftTemperature;
            set
            {
                if (_leftTemperature == value) return;
                _leftTemperature = value;
                TemperaturesChanged?.Invoke();
            }
        }

        public int RightTemperature
        {
            get => _rightTemperature;
            set
            {
                if (_rightTemperature == value) return;
                _rightTemperature = value;
                TemperaturesChanged?.Invoke();
            }
        }

        public int LeftHeatedSeatLevel
        {
            get => _leftHeatedSeatLevel;
            set
            {
                if (_leftHeatedSeatLevel == value) return;
                _leftHeatedSeatLevel = value;
                HeatedSeatsChanged?.Invoke();
            }
        }

        public int RightHeatedSeatLevel
        {
            get => _rightHeatedSeatLevel;
            set
            {
                if (_rightHeatedSeatLevel == value) return;
                _rightHeatedSeatLevel = value;
                HeatedSeatsChanged?.Invoke();
            }
        }

        public bool AutoOn
        {
            get => _autoOn;
            set
            {
                if (_autoOn == value) return;
                _autoOn = value;
                AutoChanged?.Invoke();
            }
        }

        private bool _lastAirConditioningOn;
        private int _lastAirflowPowerLevel;
        private int _lastLeftTemperature;
        private int _lastRightTemperature;
        private int _lastLeftHeatedSeatLevel;
        private int _lastRightHeatedSeatLevel;

        private void OnEnable()
        {
            _lastAirConditioningOn = _airConditioningOn;
            _lastAirflowPowerLevel = _airflowPowerLevel;
            _lastLeftTemperature = _leftTemperature;
            _lastRightTemperature = _rightTemperature;
            _lastLeftHeatedSeatLevel = _leftHeatedSeatLevel;
            _lastRightHeatedSeatLevel = _rightHeatedSeatLevel;

            // Auto is a runtime mode, not a saved setting. The asset can carry a stale true
            // from a previous session, so clear it on load and after every domain reload.
            _autoOn = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_airConditioningOn != _lastAirConditioningOn)
            {
                _lastAirConditioningOn = _airConditioningOn;
                EditorApplication.delayCall += () => AirConditioningChanged?.Invoke();
            }

            if (_airflowPowerLevel != _lastAirflowPowerLevel)
            {
                _lastAirflowPowerLevel = _airflowPowerLevel;
                AirflowPowerChanged?.Invoke();
            }

            if (_leftTemperature != _lastLeftTemperature || _rightTemperature != _lastRightTemperature)
            {
                _lastLeftTemperature = _leftTemperature;
                _lastRightTemperature = _rightTemperature;
                TemperaturesChanged?.Invoke();
            }

            if (_leftHeatedSeatLevel != _lastLeftHeatedSeatLevel || _rightHeatedSeatLevel != _lastRightHeatedSeatLevel)
            {
                _lastLeftHeatedSeatLevel = _leftHeatedSeatLevel;
                _lastRightHeatedSeatLevel = _rightHeatedSeatLevel;
                HeatedSeatsChanged?.Invoke();
            }
        }
#endif

        public enum BlinkerState { Off, Left, Right, Hazard }

        /// <summary>
        /// Why the blinker is on. Manual and Hazard come from the driver, Auto from the road
        /// geometry ahead. Priority when they disagree is Hazard > Manual > Auto.
        /// </summary>
        public enum BlinkerSource { None, Auto, Manual, Hazard }

        public enum Gears
        {
            Parking = -2,
            Reverse = -1,
            Neutral = 0,
            First_Driving = 1,
            Second = 2,
            Third = 3,
            Fourth = 4,
            Fifth = 5,
            Sixth = 6,
        }
    }
}

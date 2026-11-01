using Modules.Menus.Scripts;
using UnityEngine;
using UnityEngine.UI;

namespace Modules.HVAC.Scripts
{
    public class AutoButton : ThemeResponsive
    {
        [Header("Auto Behavior")]
        [SerializeField] private CarData _carData;
        [Tooltip("Label colour while Auto is disengaged.")]
        [SerializeField] private Color _inactiveColor = new(0.34f, 0.36f, 0.4f);

        [Header("UI Parts")]
        [SerializeField] private TemperatureSelectorWidget _leftTemperatureSelector;
        [SerializeField] private TemperatureSelectorWidget _rightTemperatureSelector;
        [SerializeField] private AirflowPowerControl _airflowPowerControl;
        [SerializeField] private HeatedSeat _heatedSeatLeft;
        [SerializeField] private HeatedSeat _heatedSeatRight;

        private Image _image;

        // True only for the duration of ToggleAuto's preset writes, so the change events they
        // raise are not mistaken for the driver reaching for a dial.
        private bool _applyingPreset;

        private void Awake()
        {
            _image = GetComponent<Image>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_carData == null) return;

            _carData.AutoChanged += ApplyState;
            _carData.TemperaturesChanged += HandleHvacChanged;
            _carData.AirflowPowerChanged += HandleHvacChanged;
            _carData.HeatedSeatsChanged += HandleHvacChanged;
            ApplyState();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_carData == null) return;

            _carData.AutoChanged -= ApplyState;
            _carData.TemperaturesChanged -= HandleHvacChanged;
            _carData.AirflowPowerChanged -= HandleHvacChanged;
            _carData.HeatedSeatsChanged -= HandleHvacChanged;
        }

        protected override void ApplyTheme() => ApplyState();

        public void ToggleAuto()
        {
            if (_carData == null) return;

            if (_carData.AutoOn)
            {
                // Turning Auto off only drops the mode; the values it set stay where they are.
                _carData.AutoOn = false;
                return;
            }

            // animate: false keeps all five writes on this stack so _applyingPreset covers them.
            // With the selector's snap animation the temperature writes land ~15 frames later,
            // outside the guard, and would immediately clear the flag set below.
            _applyingPreset = true;
            _leftTemperatureSelector.SetTemperature(21, animate: false);
            _rightTemperatureSelector.SetTemperature(21, animate: false);
            _airflowPowerControl.SetPowerLevel(7);
            _heatedSeatLeft.SetHeatingLevel(0);
            _heatedSeatRight.SetHeatingLevel(0);
            _applyingPreset = false;

            _carData.AutoOn = true;
        }

        // Any HVAC change that is not ours means the driver took over. A/C on/off is
        // deliberately not treated as an override.
        private void HandleHvacChanged()
        {
            if (_applyingPreset) return;
            _carData.AutoOn = false;
        }

        private void ApplyState()
        {
            if (_image == null) return;

            Color activeColor = _themeData != null ? _themeData.CurrentScheme.uiColor : Color.white;
            _image.color = _carData != null && _carData.AutoOn ? activeColor : _inactiveColor;
        }
    }
}

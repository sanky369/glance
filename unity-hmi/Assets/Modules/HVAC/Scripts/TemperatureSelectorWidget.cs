using UnityEngine;

namespace Modules.Menus.Scripts
{
    public class TemperatureSelectorWidget : SelectorWidget
    {
        [SerializeField] private AirflowVisualsController.Sides _side;
        [SerializeField] private CarData _carData;

        [Header("Choices")]
        [SerializeField] private ValueChoice _choicePrefab;
        [SerializeField] private int _minValue;
        [SerializeField] private int _maxValue;
        [SerializeField] private Gradient _gradient; // Used to color the text labels

        protected override void Awake()
        {
            base.Awake();
            _carData.TemperaturesChanged += HandleTemperaturesChanged;
        }

        private void OnDestroy()
        {
            if (_carData != null)
                _carData.TemperaturesChanged -= HandleTemperaturesChanged;
        }

        protected override void PopulateChoices(RectTransform container)
        {
            int count = _maxValue - _minValue + 1;
            for (int i = 0; i < count; i++)
            {
                ValueChoice choice = Instantiate(_choicePrefab, container);
                float t = GetPositionOnGradient(count, i);
                Color c = _gradient.Evaluate(t);
                choice.Initialize(_minValue + i, "F0", c);
            }

            OnSelectionConfirmed -= HandleSelectionConfirmed;
            OnSelectionConfirmed += HandleSelectionConfirmed;
        }

        protected override void OnInitialized()
        {
            int stored = GetBoundTemperature();
            int index = Mathf.Clamp(stored - _minValue, 0, _maxValue - _minValue);
            SelectChoice(index, animate: false);
        }

        /// <summary>
        /// Jumps the selector to a temperature. Pass animate: false to commit on this stack -
        /// the snap animation defers the CarData write until it finishes.
        /// </summary>
        public void SetTemperature(int value, bool animate = true)
        {
            int index = Mathf.Clamp(value - _minValue, 0, _maxValue - _minValue);
            SelectChoice(index, animate);
        }

        private static float GetPositionOnGradient(int count, int i)
        {
            float t = count > 1 ? i / (float)(count - 1) : 0f;
            return t;
        }

        private void HandleSelectionConfirmed(SelectorChoice choice)
        {
            if (choice is ValueChoice valueChoice)
                SetBoundTemperature(valueChoice.Value);
        }

        private void HandleTemperaturesChanged()
        {
            int stored = GetBoundTemperature();
            int index = Mathf.Clamp(stored - _minValue, 0, _maxValue - _minValue);
            if (index != SelectedIndex)
                SelectChoice(index);
        }

        private int GetBoundTemperature() => _side == AirflowVisualsController.Sides.Left
            ? _carData.LeftTemperature
            : _carData.RightTemperature;

        private void SetBoundTemperature(int value)
        {
            if (_side == AirflowVisualsController.Sides.Left) _carData.LeftTemperature = value;
            else _carData.RightTemperature = value;
        }
    }
}

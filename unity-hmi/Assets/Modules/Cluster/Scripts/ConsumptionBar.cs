using System;
using UnityEngine;
using UnityEngine.UI;

namespace Modules.Cluster
{
    public class ConsumptionBar : ThemeResponsive
    {
        [Header("Car Data")]
        [SerializeField] private CarData _carData;
        
        [Header("Refs")]
        [SerializeField] private Image _line;
        [SerializeField] private Image _glow;
        
        private const float lerpSpeed = 2f;

        private float _lastAmount;
        private bool _lastPositiveConsumption = true;
        private Color _originalColor;
        private readonly Color _grayColor = Color.gray6;
        private Color _grayGlowColor;
        private Color _originalGlowColor;

        private void Start()
        {
            _originalColor = _themeData.CurrentScheme.uiColor;
            
            float glowElementAlpha = _glow.color.a;
            _originalGlowColor = _originalColor.ChangeAlpha(glowElementAlpha);
            _grayGlowColor = _grayColor.ChangeAlpha(glowElementAlpha);
        }

        private void Update() => UpdateChildren();

        private void UpdateChildren(bool force = false)
        {
            float target = _carData.Consumption;
            float newAmount = Mathf.Lerp(_lastAmount, target, Time.smoothDeltaTime * lerpSpeed);

            if (Mathf.Approximately(newAmount, _lastAmount) && !force) return;
            _lastAmount = newAmount;

            float fillAmount = Mathf.Abs(newAmount);
            _line.fillAmount = fillAmount;
            _glow.fillAmount = fillAmount;

            bool positiveConsumption = newAmount >= 0f;
            _line.fillClockwise = positiveConsumption;
            _glow.fillClockwise = positiveConsumption;

            if (force ||
                positiveConsumption != _lastPositiveConsumption)
            {
                _lastPositiveConsumption = positiveConsumption;
                _line.color = positiveConsumption ? _originalColor : _grayColor;
                _glow.color = positiveConsumption ? _originalGlowColor : _grayGlowColor;
            }
        }

        protected override void ApplyTheme()
        {
            _originalColor = _themeData.CurrentScheme.uiColor;
            UpdateChildren(true);
        }
    }
}

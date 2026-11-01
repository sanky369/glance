using TMPro;
using UnityEngine;

namespace Modules.Menus.Scripts
{
    public class ValueChoice : SelectorChoice
    {
        private const float DimmedAlpha = .2f;

        [SerializeField] private int _value;
        [SerializeField] private string _format = "F1";

        private TMP_Text _label;
        private Color? _color;

        public int Value => _value;

        public void Initialize(int value, string format = null, Color? color = null)
        {
            _value = value;
            if (format != null) _format = format;
            _color = color;
            UpdateVisual();
        }

        private void Awake() => _label = GetComponentInChildren<TMP_Text>();

        private void Start()
        {
            UpdateVisual();
        }

        public override void SetDimAmount(float amount)
        {
            if (_label != null)
                _label.alpha = Mathf.Lerp(1f, DimmedAlpha, amount);
        }

        private void UpdateVisual()
        {
            if (_label == null) return;
            _label.text = _value.ToString(_format);
            if (_color.HasValue)
                _label.color = _color.Value;
        }
    }
}

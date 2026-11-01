using Modules;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Modules
{
    [RequireComponent(typeof(Image))]
    public class ToggleIconButton : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("Theme data used to tint the icon when toggled on.")]
        [SerializeField] private ThemeData _themeData;

        [Tooltip("Optional icon shown while toggled ON. Leave empty to keep the original sprite and only change tint.")]
        [SerializeField] private Sprite _onSprite;

        [Tooltip("Whether this button starts in the ON state.")]
        [SerializeField] private bool _startsOn;

        private Image _icon;
        private Sprite _offSprite;
        private bool _isOn;
        private Color _initialColor;

        public bool IsOn => _isOn;

        private void Awake()
        {
            _icon = GetComponent<Image>();
            _offSprite = _icon.sprite;
            _isOn = _startsOn;
            _initialColor = _icon.color;
        }

        private void OnEnable()
        {
            if (_themeData != null) _themeData.ThemeChanged += Apply;
            Apply();
        }

        private void OnDisable()
        {
            if (_themeData != null) _themeData.ThemeChanged -= Apply;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _isOn = !_isOn;
            Apply();
        }

        /// Sets the toggle state explicitly (e.g. from other systems).
        public void SetOn(bool on)
        {
            _isOn = on;
            Apply();
        }

        private void Apply()
        {
            if (_icon == null) return;

            _icon.sprite = _isOn && _onSprite != null ? _onSprite : _offSprite;

            _icon.color = (_isOn && _themeData != null)
                ? _themeData.CurrentScheme.uiColor
                : _initialColor;
        }
    }
}

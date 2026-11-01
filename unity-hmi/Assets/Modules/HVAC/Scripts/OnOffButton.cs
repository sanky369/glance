using UnityEngine;
using UnityEngine.UI;

namespace Modules.HVAC.Scripts
{
    public class OnOffButton : ThemeResponsive
    {
        [Header("On Off Behavior")]
        [SerializeField] private CarData _carData;

        [Header("UI Parts")]
        [SerializeField] private Button[] _interactiveButtons;
        [SerializeField] private Image[] _interactiveImages;
        [SerializeField] private CanvasGroup _bottomBarCanvasGroup;

        private Image _image;

        private void Awake()
        {
            _image = GetComponent<Image>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _carData.AirConditioningChanged += ApplyState;
            ApplyState();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _carData.AirConditioningChanged -= ApplyState;
        }

        public void ToggleOnOff()
        {
            _carData.AirConditioningOn = !_carData.AirConditioningOn;
        }

        private void ApplyState()
        {
            bool isOn = _carData.AirConditioningOn;
            _bottomBarCanvasGroup.alpha = isOn ? 1f : 0.5f;
            _bottomBarCanvasGroup.interactable = isOn;
            foreach (Button interactiveItem in _interactiveButtons) interactiveItem.interactable = isOn;
            foreach (Image interactiveImage in _interactiveImages) interactiveImage.raycastTarget = isOn;
        }

        protected override void ApplyTheme()
        {
            _image.color = _themeData.CurrentScheme.uiColor;
        }
    }
}

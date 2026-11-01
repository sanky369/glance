using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Modules.Menus.Scripts
{
    public class BatteryIndicator : ThemeResponsive
    {
        [Header("UI Elements")]
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _batteryText;

        [Header("Graphics")] [SerializeField] private Sprite[] _batteryIcons;
    
        [Header("Car Data")]
        [SerializeField] private CarData _carData;
    
        protected override void ApplyTheme()
        {
            _icon.color = _themeData.CurrentScheme.uiColor;
        }

        private void Update()
        {
            float level = _carData.BatteryLevel;
            _batteryText.text = $"{level:N0}%";

            int iconIndex = level switch
            {
                < 33f => 0,
                < 66f => 1,
                _ => 2,
            };
            _icon.sprite = _batteryIcons[iconIndex];
        }
    }
}

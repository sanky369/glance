using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Modules.BodyControl.Scripts
{
    public class RegularButton : ThemeResponsive
    {
        [Header("UI Parts")]
        [SerializeField] protected TextMeshProUGUI _label;
        [SerializeField] protected Image _icon;

        protected override void ApplyTheme()
        {
            Color tint = _themeData.CurrentScheme.uiColor;

            _label.color = tint;
            _icon.color = tint;
        }
    }
}

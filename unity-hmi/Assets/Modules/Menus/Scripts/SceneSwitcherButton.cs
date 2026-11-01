using Modules.BodyControl.Scripts;
using UnityEngine;
using UnityEngine.UI;

namespace Modules.Menus.Scripts
{
    public class SceneSwitcherButton : RegularButton
    {
        [SerializeField] protected Image _glow;

        [Header("Scene Functionality")]
        [SerializeField] private Scenes _scene;

        private bool _active;
        private readonly Color _disabledColor = new(0.52f, 0.54f, 0.58f);

        public void SetActive(bool active)
        {
            _active = active;
            _glow.enabled = active;
            ApplyTheme();
        }

        public Scenes Scene => _scene;

        protected override void ApplyTheme()
        {
            Color tint = _themeData.CurrentScheme.uiColor;
            Color fadedColor = tint.ChangeAlpha(_glow.color.a);

            _label.color = _active ? tint : _disabledColor;
            _icon.color = _active ? tint : _disabledColor;
            _glow.color = fadedColor;
        }
    }
}

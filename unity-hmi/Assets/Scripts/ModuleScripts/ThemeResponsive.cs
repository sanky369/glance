using UnityEngine;

namespace Modules
{
    public abstract class ThemeResponsive : MonoBehaviour
    {
        [Header("Theme Data")]
        [SerializeField] protected ThemeData _themeData;

        protected abstract void ApplyTheme();

        protected virtual void OnEnable()
        {
            if (_themeData == null)
                return;

            _themeData.ThemeChanged += ApplyTheme;
            ApplyTheme();
        }

        protected virtual void OnDisable()
        {
            if (_themeData == null)
                return;

            _themeData.ThemeChanged -= ApplyTheme;
        }
    }
}

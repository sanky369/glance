using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Modules.Cluster
{
    /// <summary>
    /// A theme-responsive component used to apply tinting to a bunch of Image components in one go.
    /// It applies the RGB values but keeps the original alpha.
    /// </summary>
    public class ThemeTintUI : ThemeResponsive
    {
        [SerializeField] private List<Image> _images;

        protected override void ApplyTheme() => UpdateElements();

        private void OnValidate() => UpdateElements();

        private void UpdateElements()
        {
            Color tint = _themeData.CurrentScheme.uiColor;
            foreach (Image image in _images) image.color = image.color.ChangeIntoColorKeepAlpha(tint);
        }
    }
}

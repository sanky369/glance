using UnityEngine;

namespace Modules
{
    public class ADASTheming : ThemeResponsive
    {
        [SerializeField] private Material _stripsMaterial;
        [SerializeField] private Material _cityBuildingMaterial;

        protected override void ApplyTheme()
        {
            Color color = _themeData.CurrentScheme.adasLinesColor;

            if (_stripsMaterial != null)
            {
                _stripsMaterial.SetColor("_EmissionColor", color);
                Color baseColor = new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), color.a);
                _stripsMaterial.SetColor("_BaseColor", baseColor);
                _stripsMaterial.SetColor("_Color", baseColor);
            }

            if (_cityBuildingMaterial != null)
                _cityBuildingMaterial.SetColor("_ground_level_glow_color", color);
        }
    }
}

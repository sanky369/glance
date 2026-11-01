using UnityEngine;

namespace Modules
{
    /// <summary>
    /// Used to ensure the car changes color (i.e. Material) according to the current theme.
    /// </summary>
    public class CarTheming : ThemeResponsive
    {
        [SerializeField] private MeshRenderer[] _meshRenderers;

        protected override void ApplyTheme()
        {
            foreach (MeshRenderer meshRenderer in _meshRenderers)
            {
                meshRenderer.sharedMaterial = _themeData.CurrentScheme.carMaterial;
            }
        }
    }
}

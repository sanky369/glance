using Modules;
using UnityEngine;
using UnityEngine.UI;

namespace Modules.Menus.Scripts
{
    public class ColorChoice : SelectorChoice
    {
        private const float DimmedAlpha = 0.2f;

        private ColorScheme _scheme;
        private int _schemeIndex;

        private Image _image;

        public ColorScheme Scheme => _scheme;
        public int SchemeIndex => _schemeIndex;

        public void Initialize(ColorScheme scheme, int schemeIndex)
        {
            _scheme = scheme;
            _schemeIndex = schemeIndex;
            UpdateVisual();
        }

        private void Awake() => _image = GetComponent<Image>();

        private void Start()
        {
            UpdateVisual();
        }

        public override void SetDimAmount(float amount)
        {
            if (_image != null)
                _image.color = _scheme.uiColor.ChangeAlpha(Mathf.Lerp(1f, DimmedAlpha, amount));
        }

        private void UpdateVisual()
        {
            if (_image != null)
                _image.color = _image.color.ChangeIntoColorKeepAlpha(_scheme.uiColor);
        }
    }
}

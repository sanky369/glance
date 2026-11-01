using Modules;
using UnityEngine;
using UnityEngine.UI;

public class ThemedImage : ThemeResponsive
{
    private Image _image;
    private float _originalAlpha;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _originalAlpha = _image.color.a;
    }

    protected override void ApplyTheme()
    {
        _image.color = _themeData.CurrentScheme.uiColor.ChangeAlpha(_originalAlpha);
    }
}

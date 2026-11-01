using HMI_Hub_Template;
using Modules;
using UnityEngine;
using UnityEngine.UI;

public class PlayButton : ThemeResponsive
{
    [Header("Theme")]
    public Image background;

    [Header("Icon Swap")]
    [SerializeField] private Image _icon;
    [SerializeField] private Sprite _playSprite;
    [SerializeField] private Sprite _pauseSprite;
    [SerializeField] private MusicPlayerWidget _musicPlayer;

    protected override void ApplyTheme()
    {
        background.color = _themeData.CurrentScheme.uiColor;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (_musicPlayer == null) return;

        _musicPlayer.PlaybackStateChanged += OnPlaybackStateChanged;
        OnPlaybackStateChanged(_musicPlayer.IsPlaying);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (_musicPlayer == null) return;

        _musicPlayer.PlaybackStateChanged -= OnPlaybackStateChanged;
    }

    private void OnPlaybackStateChanged(bool isPlaying)
    {
        _icon.sprite = isPlaying ? _pauseSprite : _playSprite;
    }
}

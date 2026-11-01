using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HMI_Hub_Template
{
    /// <summary>
    /// Drives the dashboard music player: grays out the cover art, sweeps the progress bar
    /// over a fixed track length, and on each loop advances to the next "track" — an easter-egg
    /// list of games made with Unity.
    /// </summary>
    public class MusicPlayerWidget : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The album cover Image (grayed out at startup).")]
        [SerializeField] private Image _coverArt;

        [Tooltip("The progress bar Fill RectTransform. Its horizontal anchor sweeps 0 -> 1.")]
        [SerializeField] private RectTransform _progressFill;

        [Tooltip("Song title text (cycled through the easter-egg list).")]
        [SerializeField] private TMP_Text _titleText;

        [Tooltip("Artist text (optional).")]
        [SerializeField] private TMP_Text _artistText;

        [Header("Playback")]
        [Tooltip("Seconds for the progress bar to fill before the track restarts.")]
        [SerializeField] private float _trackLength = 60f;

        [Header("Album Covers (per team member)")]
        [Tooltip("BALANCE 002 — Sheraz (bald).")]
        [SerializeField] private Sprite _coverSheraz;
        [Tooltip("FOUNDATION 003 — John (beard, shirt).")]
        [SerializeField] private Sprite _coverJohn;
        [Tooltip("Fallback cover for anyone without dedicated art.")]
        [SerializeField] private Sprite _coverDefault;

        // Easter egg: the Industry Sample & Demo team as a "now playing" tracklist.
        // Every title is a real, specific thing said/done in #industry-sample-and-demo-team
        // while building the Industry HMI Template — no filler.
        // cover key: "sheraz" | "john" | "" (default)
        private static readonly (string title, string artist, string cover)[] _tracks =
        {
            ("I Will Run To You", "Sheraz Asghar", "sheraz"),
            ("If You Say Pi, I Say Po", "John Futscher", "john"),
        };

        private int _trackIndex;
        private float _elapsed;
        private bool _isPlaying = true;

        public bool IsPlaying => _isPlaying;
        public event Action<bool> PlaybackStateChanged;

        private void Awake()
        {
            // Covers are full-art album sleeves, shown at full color (no gray tint).
            if (_coverArt != null) _coverArt.color = Color.white;
        }

        private void OnEnable()
        {
            _trackIndex = 0;
            _elapsed = 0f;
            _isPlaying = true;
            ApplyTrack();
            PlaybackStateChanged?.Invoke(_isPlaying);
        }

        private void Update()
        {
            if (!_isPlaying) return;
            if (_trackLength <= 0f) return;

            _elapsed += Time.deltaTime;
            if (_elapsed >= _trackLength)
            {
                // Track finished — advance to the next game and restart.
                NextTrack();
            }

            SetProgress(Mathf.Clamp01(_elapsed / _trackLength));
        }

        public void TogglePlayPause()
        {
            _isPlaying = !_isPlaying;
            PlaybackStateChanged?.Invoke(_isPlaying);
        }

        /// Skip to the next track (wired to the Next ▶▶ button).
        public void NextTrack()
        {
            _trackIndex = (_trackIndex + 1) % _tracks.Length;
            _elapsed = 0f;
            ApplyTrack();
        }

        /// Skip to the previous track (wired to the Back ◀◀ button).
        public void PreviousTrack()
        {
            _trackIndex = (_trackIndex - 1 + _tracks.Length) % _tracks.Length;
            _elapsed = 0f;
            ApplyTrack();
        }

        private void ApplyTrack()
        {
            var track = _tracks[_trackIndex];
            if (_titleText != null) _titleText.text = track.title;
            if (_artistText != null) _artistText.text = track.artist;

            if (_coverArt != null)
            {
                Sprite cover = track.cover switch
                {
                    "sheraz" => _coverSheraz,
                    "john"   => _coverJohn,
                    _        => _coverDefault,
                };
                if (cover == null) cover = _coverDefault;
                if (cover != null) _coverArt.sprite = cover;
            }

            SetProgress(0f);
        }

        // Sweeps the fill RectTransform horizontally from empty (0) to full (1) using its anchors,
        // which works regardless of the Image draw mode (the fill sprite is Sliced, not Filled).
        private void SetProgress(float t)
        {
            if (_progressFill == null) return;
            _progressFill.anchorMin = new Vector2(0f, _progressFill.anchorMin.y);
            _progressFill.anchorMax = new Vector2(t, _progressFill.anchorMax.y);
            _progressFill.offsetMin = Vector2.zero;
            _progressFill.offsetMax = Vector2.zero;
        }
    }
}

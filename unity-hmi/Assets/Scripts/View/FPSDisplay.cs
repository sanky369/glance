using TMPro;
using UnityEngine;
using UnityEngine.Profiling;

namespace HMI_Hub_Template
{
    public class FPSDisplay : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private bool showOverlay = true;
        [SerializeField] private UnityEngine.InputSystem.Key toggleKey = UnityEngine.InputSystem.Key.F1;
        [SerializeField] private TextMeshProUGUI _label;

        [Header("Thresholds (FPS)")]
        [SerializeField] private float warningFPS = 45f;
        [SerializeField] private float criticalFPS = 30f;

        [Header("Colors")]
        [SerializeField] private Color normalColor   = new Color(0f, 1f, 0f, 0.9f);
        [SerializeField] private Color warningColor  = new Color(1f, 0.6f, 0f, 0.9f);
        [SerializeField] private Color criticalColor = new Color(1f, 0.2f, 0.2f, 0.9f);

        private float _accumTime;
        private int   _accumFrames;
        private float _rollingFPS;
        private float _worstFrameMs;
        private long  _gcAllocBytes;

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
            {
                showOverlay = !showOverlay;
                if (_label != null) _label.enabled = showOverlay;
            }

            if (!showOverlay || _label == null) return;

            float dt = Time.unscaledDeltaTime;
            _accumTime   += dt;
            _accumFrames++;

            float frameMs = dt * 1000f;
            if (frameMs > _worstFrameMs) _worstFrameMs = frameMs;

            _gcAllocBytes = Profiler.GetMonoUsedSizeLong();

            if (_accumTime >= 1f)
            {
                _rollingFPS = _accumFrames / _accumTime;

                _label.color = _rollingFPS >= warningFPS  ? normalColor
                             : _rollingFPS >= criticalFPS ? warningColor
                             : criticalColor;

                _label.text = $"FPS  {_rollingFPS:F0}   ({1000f / _rollingFPS:F1} ms avg)\n" +
                              $"Worst  {_worstFrameMs:F1} ms\n" +
                              $"Heap  {_gcAllocBytes / 1024:N0} KB";

                _accumTime   = 0f;
                _accumFrames = 0;
                _worstFrameMs = 0f;
            }
        }
    }
}

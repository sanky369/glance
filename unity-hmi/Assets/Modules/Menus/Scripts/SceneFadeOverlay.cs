using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Modules.Menus.Scripts
{
    // Attach to a full-screen Image (stretch anchors, raycast target off) inside the
    // persistent CanvasOverlay. Set the Image color to black (or any tint) with alpha 0
    // in the editor — this script drives the alpha at runtime.
    [RequireComponent(typeof(Image))]
    public class SceneFadeOverlay : MonoBehaviour
    {
        [Tooltip("Duration of each fade direction in seconds")]
        [SerializeField] private float _fadeDuration = 0.5f;

        private Image _image;
        private Coroutine _activeCoroutine;

        private void Awake()
        {
            _image = GetComponent<Image>();
            SetAlpha(0f);
        }

        public float FadeDuration => _fadeDuration;

        // Fades the overlay IN (screen goes black). Awaitable via StartCoroutine.
        public IEnumerator FadeOut()
        {
            if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
            _activeCoroutine = StartCoroutine(TweenAlpha(0f, 1f, _fadeDuration));
            yield return _activeCoroutine;
        }

        // Fades the overlay OUT (screen becomes clear). Awaitable via StartCoroutine.
        public IEnumerator FadeIn()
        {
            if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
            _activeCoroutine = StartCoroutine(TweenAlpha(1f, 0f, _fadeDuration));
            yield return _activeCoroutine;
        }

        private IEnumerator TweenAlpha(float from, float to, float duration)
        {
            float elapsed = 0f;
            SetAlpha(from);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, to, elapsed / duration));
                yield return null;
            }
            SetAlpha(to);
        }

        private void SetAlpha(float alpha)
        {
            Color c = _image.color;
            c.a = alpha;
            _image.color = c;
        }
    }
}

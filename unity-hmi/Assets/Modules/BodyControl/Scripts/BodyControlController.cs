using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace Modules.BodyControl.Scripts
{
    public class BodyControlController : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera _ADASCamera;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _fadeDuration = 0.5f;

        private InterestPoint[] _interestPoints;
        private Coroutine _fadeRoutine;

        private void Awake()
        {
            FindAllInterestPoints();
        }

        /// <summary>
        /// Invoked by the Main scene, from UI elements that allow transitioning to ADAS.
        /// </summary>
        public void TransitionToADASScene()
        {
            _ADASCamera.gameObject.SetActive(true);
            CloseAllParts();
            FadeOutUI();
        }

        /// <summary>
        /// Invoked by the Close All button in the bottom UI.
        /// </summary>
        public void CloseAllParts()
        {
            foreach (InterestPoint interestPoint in _interestPoints) interestPoint.ForceClose();
        }

        public void FadeOutUI() => FadeUI(0f);
        public void FadeInUI() => FadeUI(1f);

        private void FadeUI(float targetAlpha)
        {
            if (_fadeRoutine != null)
                StopCoroutine(_fadeRoutine);

            _fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha));
        }

        private IEnumerator FadeRoutine(float targetAlpha)
        {
            float startAlpha = _canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / _fadeDuration);
                yield return null;
            }

            _canvasGroup.alpha = targetAlpha;
            _fadeRoutine = null;
        }

        private void FindAllInterestPoints()
        {
#if UNITY_6000_4_OR_NEWER
            _interestPoints = FindObjectsByType<InterestPoint>();
#else
            _interestPoints = FindObjectsByType<InterestPoint>(FindObjectsSortMode.None);
#endif
        }
    }
}

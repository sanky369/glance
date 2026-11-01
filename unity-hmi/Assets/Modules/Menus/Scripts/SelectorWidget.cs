using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Modules.Menus.Scripts
{
    public abstract class SelectorWidget : MonoBehaviour
    {
        [Header("Object References")]
        [SerializeField] private SelectorHandle _handle;
        [SerializeField] private Image _handleOnTop;
        [SerializeField] private RectTransform _maskElement;
        [SerializeField] private RectTransform _choicesContainer;
        [SerializeField] private InputActionReference _cameraOrbitX;
        [SerializeField] private InputActionReference _cameraOrbitY;
        
        [Header("Parameters")]
        [SerializeField] private float _maxDragDistance = 30f;
        [SerializeField] private float _animationDuration = 0.15f;
        [SerializeField] private float _scrollSpeed = 300f;
        [SerializeField] private float _snapDuration = 0.25f;

        public event Action<SelectorChoice> OnSelectionConfirmed;

        private RectTransform _handleRect;
        private RectTransform _handleOnTopRect;
        private RectTransform _parentRect;
        private RectTransform _maskRect;
        private Image _handleImage;

        private float _startAnchoredX;
        private Coroutine _pixelsPerUnitCoroutine;
        private Coroutine _snapBackCoroutine;
        private Coroutine _containerSnapCoroutine;
        private Coroutine _dimCoroutine;
        private float _dimAmount = 1f;

        private float[] _snapPoints;
        private float _containerMinX;
        private float _containerMaxX;
        private SelectorChoice[] _choices;

        private bool _isDragging;
        private float _handleDisplacement;
        private bool _orbitDisabled;

        public int SelectedIndex { get; private set; }

        public SelectorChoice SelectedChoice =>
            _choices != null && SelectedIndex >= 0 && SelectedIndex < _choices.Length
                ? _choices[SelectedIndex]
                : null;

        protected virtual void Awake()
        {
            _handleRect = _handle.transform as RectTransform;
            _handleOnTopRect = _handleOnTop.transform as RectTransform;
            _parentRect = _handleRect!.parent as RectTransform;
            _maskRect = _maskElement;
            _handleImage = _handle.GetComponent<Image>();
        }

        private IEnumerator Start()
        {
            for (int i = _choicesContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = _choicesContainer.GetChild(i);
                child.SetParent(null);
                Destroy(child.gameObject);
            }

            PopulateChoices(_choicesContainer);

            // Wait one frame for HorizontalLayoutGroup to compute positions
            yield return null;
            ComputeSnapPoints();
            ApplyDim(1f);
            OnInitialized();
        }

        protected abstract void PopulateChoices(RectTransform container);

        protected virtual void OnInitialized() { }

        private void OnEnable()
        {
            _handle.OnDragEvent += HandleOnDragEvent;
            _handle.OnBeginDragEvent += HandleOnBeginDragEvent;
            _handle.OnEndDragEvent += HandleOnEndDragEvent;
            _handle.OnPointerDownEvent += HandleOnPointerDownEvent;
            _handle.OnPointerUpEvent += HandleOnPointerUpEvent;
        }

        private void OnDisable()
        {
            _handle.OnDragEvent -= HandleOnDragEvent;
            _handle.OnBeginDragEvent -= HandleOnBeginDragEvent;
            _handle.OnEndDragEvent -= HandleOnEndDragEvent;
            _handle.OnPointerDownEvent -= HandleOnPointerDownEvent;
            _handle.OnPointerUpEvent -= HandleOnPointerUpEvent;

            if (_orbitDisabled) SetCameraOrbit(true);
        }

        private void Update()
        {
            if (!_isDragging || Mathf.Approximately(_handleDisplacement, 0f))
                return;

            float normalized = _handleDisplacement / _maxDragDistance;
            float velocity = normalized * _scrollSpeed;
            float newX = _choicesContainer.anchoredPosition.x - velocity * Time.deltaTime;
            newX = Mathf.Clamp(newX, _containerMinX, _containerMaxX);
            SetContainerX(newX);

            int nearest = FindNearestSnapIndex(newX);
            if (nearest != SelectedIndex)
                CommitSelection(nearest);
        }

        // --- Handle drag ---

        private void HandleOnDragEvent(PointerEventData obj)
        {
            StopSnapBack();
            StopContainerSnap();

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parentRect, obj.position, obj.pressEventCamera, out Vector2 localPoint))
                return;

            float clampedX = Mathf.Clamp(localPoint.x, _startAnchoredX - _maxDragDistance,
                _startAnchoredX + _maxDragDistance);

            SetHandleX(clampedX);
            _handleDisplacement = clampedX - _startAnchoredX;
        }

        private void HandleOnBeginDragEvent(PointerEventData obj)
        {
            _startAnchoredX = _handleRect.anchoredPosition.x;
            _isDragging = true;
            StopContainerSnap();
        }

        private void HandleOnEndDragEvent(PointerEventData obj)
        {
            _isDragging = false;
            _handleDisplacement = 0f;

            StopSnapBack();
            _snapBackCoroutine = StartCoroutine(AnimateSnapBack());

            StopContainerSnap();
            int nearestIndex = FindNearestSnapIndex(_choicesContainer.anchoredPosition.x);
            _containerSnapCoroutine = StartCoroutine(AnimateContainerSnap(nearestIndex));
        }

        // --- Pointer down/up (visual feedback) ---

        private void HandleOnPointerDownEvent(PointerEventData obj)
        {
            SetCameraOrbit(false);
            AnimatePixelsPerUnit(2f);
            AnimateDim(0f);
        }

        private void HandleOnPointerUpEvent(PointerEventData obj)
        {
            SetCameraOrbit(true);
            AnimatePixelsPerUnit(1f);
            AnimateDim(1f);
        }

        protected void SelectChoice(int index, bool animate = true)
        {
            if (_snapPoints == null || index < 0 || index >= _snapPoints.Length)
                return;

            StopContainerSnap();

            if (animate)
            {
                _containerSnapCoroutine = StartCoroutine(AnimateContainerSnap(index));
            }
            else
            {
                SetContainerX(_snapPoints[index]);
                CommitSelection(index);
            }
        }

        private void SetCameraOrbit(bool active)
        {
            if (active)
            {
                _cameraOrbitX.action.Enable();
                _cameraOrbitY.action.Enable();
            }
            else
            {
                _cameraOrbitX.action.Disable();
                _cameraOrbitY.action.Disable();
            }

            _orbitDisabled = !active;
        }

        // --- Snap points ---

        private void ComputeSnapPoints()
        {
            int childCount = _choicesContainer.childCount;
            _choices = new SelectorChoice[childCount];
            _snapPoints = new float[childCount];

            float currentContainerX = _choicesContainer.anchoredPosition.x;
            Vector3 maskCenter = _maskRect.TransformPoint(Vector3.zero);

            for (int i = 0; i < childCount; i++)
            {
                RectTransform childRect = _choicesContainer.GetChild(i) as RectTransform;
                _choices[i] = childRect!.GetComponent<SelectorChoice>();

                Vector3 childCenter = childRect.TransformPoint(Vector3.zero);
                float worldOffset = childCenter.x - maskCenter.x;
                float localOffset = worldOffset / _choicesContainer.lossyScale.x;
                _snapPoints[i] = currentContainerX - localOffset;
            }

            if (childCount > 0)
            {
                _containerMinX = Mathf.Min(_snapPoints[0], _snapPoints[childCount - 1]);
                _containerMaxX = Mathf.Max(_snapPoints[0], _snapPoints[childCount - 1]);
            }

            if (childCount == 0)
                return;

            SelectedIndex = FindNearestSnapIndex(currentContainerX);
            SetContainerX(_snapPoints[SelectedIndex]);
        }

        private int FindNearestSnapIndex(float containerX)
        {
            int nearest = 0;
            float minDist = float.MaxValue;
            for (int i = 0; i < _snapPoints.Length; i++)
            {
                float dist = Mathf.Abs(_snapPoints[i] - containerX);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = i;
                }
            }

            return nearest;
        }

        // --- Setters ---

        private void SetHandleX(float x)
        {
            Vector2 pos = _handleRect.anchoredPosition;
            pos.x = x;
            _handleRect.anchoredPosition = pos;
            _handleOnTopRect.anchoredPosition = pos;
        }

        private void SetContainerX(float x)
        {
            Vector2 pos = _choicesContainer.anchoredPosition;
            pos.x = x;
            _choicesContainer.anchoredPosition = pos;
        }

        private void SetPixelsPerUnit(float value)
        {
            _handleImage.pixelsPerUnitMultiplier =
                _handleOnTop.pixelsPerUnitMultiplier = value;
        }

        private void CommitSelection(int index)
        {
            SelectedIndex = index;
            ApplyDim(_dimAmount);
            OnSelectionConfirmed?.Invoke(_choices[SelectedIndex]);
        }

        private void ApplyDim(float amount)
        {
            if (_choices == null) return;
            for (int i = 0; i < _choices.Length; i++)
            {
                if (_choices[i] == null) continue;
                _choices[i].SetDimAmount(i == SelectedIndex ? 0f : amount);
            }
        }

        // --- Coroutine management ---

        private void AnimatePixelsPerUnit(float target)
        {
            if (_pixelsPerUnitCoroutine != null)
                StopCoroutine(_pixelsPerUnitCoroutine);
            _pixelsPerUnitCoroutine = StartCoroutine(LerpPixelsPerUnit(target));
        }

        private void AnimateDim(float target)
        {
            if (_dimCoroutine != null)
                StopCoroutine(_dimCoroutine);
            _dimCoroutine = StartCoroutine(LerpDim(target));
        }

        private void StopSnapBack()
        {
            if (_snapBackCoroutine != null)
            {
                StopCoroutine(_snapBackCoroutine);
                _snapBackCoroutine = null;
            }
        }

        private void StopContainerSnap()
        {
            if (_containerSnapCoroutine != null)
            {
                StopCoroutine(_containerSnapCoroutine);
                _containerSnapCoroutine = null;
            }
        }

        // --- Animations ---

        private IEnumerator LerpPixelsPerUnit(float target)
        {
            float start = _handleImage.pixelsPerUnitMultiplier;
            float elapsed = 0f;

            while (elapsed < _animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _animationDuration;
                t = t * t * (3f - 2f * t);
                SetPixelsPerUnit(Mathf.Lerp(start, target, t));
                yield return null;
            }

            SetPixelsPerUnit(target);
            _pixelsPerUnitCoroutine = null;
        }

        private IEnumerator LerpDim(float target)
        {
            float start = _dimAmount;
            float elapsed = 0f;

            while (elapsed < _animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _animationDuration;
                t = t * t * (3f - 2f * t);
                _dimAmount = Mathf.Lerp(start, target, t);
                ApplyDim(_dimAmount);
                yield return null;
            }

            _dimAmount = target;
            ApplyDim(_dimAmount);
            _dimCoroutine = null;
        }

        private IEnumerator AnimateSnapBack()
        {
            float startX = _handleRect.anchoredPosition.x;
            float elapsed = 0f;

            while (elapsed < _animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _animationDuration;
                t = t * t * (3f - 2f * t);
                SetHandleX(Mathf.Lerp(startX, _startAnchoredX, t));
                yield return null;
            }

            SetHandleX(_startAnchoredX);
            _snapBackCoroutine = null;
        }

        private IEnumerator AnimateContainerSnap(int targetIndex)
        {
            float startX = _choicesContainer.anchoredPosition.x;
            float targetX = _snapPoints[targetIndex];
            float elapsed = 0f;

            while (elapsed < _snapDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _snapDuration;
                t = t * t * (3f - 2f * t);
                SetContainerX(Mathf.Lerp(startX, targetX, t));
                yield return null;
            }

            SetContainerX(targetX);
            _containerSnapCoroutine = null;
            CommitSelection(targetIndex);
        }
    }
}

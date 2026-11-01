using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Modules.BodyControl.Scripts
{
    public enum BodyPart
    {
        Hood,
        Trunk,
        ChargingPort,
        DriverDoor,
        PassengerDoor,
        RearLeftDoor,
        RearRightDoor,
    }

    public class InterestPoint : ThemeResponsive
    {
        [SerializeField] private InputActionReference _holdAction;
        [SerializeField] private Transform _followTarget;
        [SerializeField] private Collider _occluder;
        
        [Header("UI parts")]
        [SerializeField] private Button _button;
        [SerializeField] private RectTransform _ring;
        [SerializeField] private Image _ringImage;
        [SerializeField] private Image _circleImage;
        [SerializeField] private Image _outlineImage;
        [SerializeField] private Image _lineImage;
        [SerializeField] private RectTransform _infoGroup;
        [SerializeField] private RectTransform _textPanel;
        [SerializeField] private TextMeshProUGUI _statusLabel;
        
        [Header("Car part")]
        [SerializeField] private CarData _carData;
        [SerializeField] private BodyPart _boundPart;
        [SerializeField] private Transform _partToRotate;
        [SerializeField] private Transform _restPosition;
        [SerializeField] private Transform _openedPosition;
        [SerializeField] private float _animationDuration = 1f;
        [SerializeField] private AnimationCurve _animationCurve;
        [SerializeField] private string _closedLabel = "CLOSED";
        [SerializeField] private string _openedLabel = "OPEN";

        private static int s_holdActionUsers;

        private CanvasGroup _thisCanvasGroup;
        private CanvasGroup _infoGroupCanvasGroup;
        private RectTransform _rectTransform;
        private Camera _camera;
        private Color _ringColor;
        
        private float _ringAnimationProgress;
        private Coroutine _partAnimation;
        private bool _isOpened;
        private CinemachineOrbitalFollow _orbitalFollow;
        private float _lastHAxis;
        private float _lastVAxis;
        private bool _lastLeftSide;
        private Ray _occluderRay;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_holdActionUsers = 0;

        private void Awake()
        {
            _camera = Object.FindFirstObjectByType<Camera>();
            _orbitalFollow = Object.FindFirstObjectByType<CinemachineOrbitalFollow>();
            if (_orbitalFollow != null)
            {
                _lastHAxis = _orbitalFollow.HorizontalAxis.Value;
                _lastVAxis = _orbitalFollow.VerticalAxis.Value;
            }
            _thisCanvasGroup = GetComponent<CanvasGroup>();
            _rectTransform = GetComponent<RectTransform>();
            _infoGroupCanvasGroup = _infoGroup.GetComponent<CanvasGroup>();
            _ringColor = _ringImage.color;

            _isOpened = GetBoundState();
            Transform startPose = _isOpened ? _openedPosition : _restPosition;
            _partToRotate.SetPositionAndRotation(startPose.position, startPose.rotation);
            if (_statusLabel != null) _statusLabel.text = _isOpened ? _openedLabel : _closedLabel;
        }

        private void Start()
        {
            UpdateOcclusionAlpha();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (s_holdActionUsers++ == 0) _holdAction.action.Enable();
            _holdAction.action.performed += OnCameraRotation;
            _holdAction.action.canceled += OnCameraStoppedRotating;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (--s_holdActionUsers == 0) _holdAction.action.Disable();
            _holdAction.action.performed -= OnCameraRotation;
            _holdAction.action.canceled -= OnCameraStoppedRotating;
        }

        protected override void ApplyTheme()
        {
            if (_themeData == null) return;
            Color tint = _themeData.CurrentScheme.uiColor;

            if (_ringImage != null)
            {
                _ringImage.color = tint;
                _ringColor = tint;
            }
            if (_circleImage != null) _circleImage.color = tint;
            if (_outlineImage != null) _outlineImage.color = tint;
            if (_statusLabel != null) _statusLabel.color = tint;
            if (_lineImage != null)
                _lineImage.color = _lineImage.color.ChangeIntoColorKeepAlpha(tint);
        }

        private void OnCameraRotation(InputAction.CallbackContext obj) => _infoGroupCanvasGroup.alpha = 0f;

        private void OnCameraStoppedRotating(InputAction.CallbackContext obj) => _infoGroupCanvasGroup.alpha = 1f;

        /// <summary>
        /// Invoked by the Button script on the sub-object Circle
        /// </summary>
        public void OnButtonPressed()
        {
            if (_partAnimation != null) return;
            _isOpened = !_isOpened;
            SetBoundState(_isOpened);
            if (_statusLabel != null) _statusLabel.text = _isOpened ? _openedLabel : _closedLabel;
            if (_button != null) _button.interactable = false;
            _partAnimation = StartCoroutine(AnimatePart(_isOpened));
        }

        public void ForceClose()
        {
            if (!_isOpened) return;
            OnButtonPressed();
        }

        private bool GetBoundState() => _boundPart switch
        {
            BodyPart.Trunk => _carData.TrunkOpen,
            BodyPart.DriverDoor => _carData.DriverDoorOpen,
            BodyPart.PassengerDoor => _carData.PassengerDoorOpen,
            _ => false,
        };

        private void SetBoundState(bool value)
        {
            switch (_boundPart)
            {
                case BodyPart.Trunk: _carData.TrunkOpen = value; break;
                case BodyPart.DriverDoor: _carData.DriverDoorOpen = value; break;
                case BodyPart.PassengerDoor: _carData.PassengerDoorOpen = value; break;
            }
        }

        private IEnumerator AnimatePart(bool opening)
        {
            Transform from = opening ? _restPosition : _openedPosition;
            Transform to = opening ? _openedPosition : _restPosition;
            Vector3 fromPos = from.position;
            Vector3 toPos = to.position;
            Quaternion fromRot = from.rotation;
            Quaternion toRot = to.rotation;

            float elapsed = 0f;
            while (elapsed < _animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = _animationCurve.Evaluate(Mathf.Clamp01(elapsed / _animationDuration));
                _partToRotate.SetPositionAndRotation(
                    Vector3.LerpUnclamped(fromPos, toPos, t),
                    Quaternion.LerpUnclamped(fromRot, toRot, t));
                yield return null;
            }

            _partToRotate.SetPositionAndRotation(toPos, toRot);
            _partAnimation = null;
            if (_button != null) _button.interactable = true;
        }

        private void UpdateOcclusionAlpha()
        {
            if (_occluder == null || _camera == null) return;

            Vector3 origin = _camera.transform.position;
            Vector3 toTarget = _followTarget.position - origin;
            _occluderRay.origin = origin;
            _occluderRay.direction = toTarget.normalized;

            bool occluded = _occluder.Raycast(_occluderRay, out _, toTarget.magnitude);
            _thisCanvasGroup.alpha = occluded ? 0.2f : 1f;
        }

        private void Update()
        {
            // Movement in 2D space
            Vector3 screenPos = _camera.WorldToScreenPoint(_followTarget.position);
            bool behindCamera = screenPos.z < 0f;
            if (!behindCamera) _rectTransform.position = screenPos;

            AnimateRing();
            UpdateSide();
            SyncFromCarData();
        }

        private void LateUpdate()
        {
            bool cameraMoved = false;
            if (_orbitalFollow != null)
            {
                float h = _orbitalFollow.HorizontalAxis.Value;
                float v = _orbitalFollow.VerticalAxis.Value;
                cameraMoved = !Mathf.Approximately(h, _lastHAxis) || !Mathf.Approximately(v, _lastVAxis);
                _lastHAxis = h;
                _lastVAxis = v;
            }

            if (cameraMoved || _partAnimation != null) UpdateOcclusionAlpha();
        }

        private void SyncFromCarData()
        {
            bool soState = GetBoundState();
            if (soState == _isOpened) return;

            _isOpened = soState;
            if (_statusLabel != null) _statusLabel.text = _isOpened ? _openedLabel : _closedLabel;
            if (_partAnimation != null) StopCoroutine(_partAnimation);
            if (_button != null) _button.interactable = false;
            _partAnimation = StartCoroutine(AnimatePart(_isOpened));
        }

        private void UpdateSide()
        {
            bool leftSide = _rectTransform.anchoredPosition.x < 0f;
            if (leftSide == _lastLeftSide) return;
            _lastLeftSide = leftSide;
            Vector3 elementsScale = new(leftSide ? 1f : -1f, 1f, 1f);
            _infoGroup.localScale = elementsScale;
            _textPanel.localScale = elementsScale;
        }

        private void AnimateRing()
        {
            const float ringAnimationSpeed = .8f;
            _ringAnimationProgress = Mathf.Repeat(_ringAnimationProgress + Time.deltaTime * ringAnimationSpeed, 1f);

            _ring.localScale = Vector3.one * (1f + _ringAnimationProgress * .75f);
            // SetAlpha on the CanvasRenderer avoids SetVerticesDirty and the canvas mesh rebuild it triggers.
            float alpha = Mathf.Clamp01(1f - (_ringAnimationProgress - .1f));
            _ringImage.canvasRenderer.SetAlpha(alpha);
        }
    }
}

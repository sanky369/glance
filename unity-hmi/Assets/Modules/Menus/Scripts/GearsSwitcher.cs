using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Modules.Menus.Scripts
{
    public class GearsSwitcher : ThemeResponsive
    {
        [SerializeField] private GearLetter[] _letters;
        [SerializeField] private Image _underline;
        [SerializeField] private Color _disabledLetterColor = new(0.34f, 0.36f, 0.4f);
        [SerializeField] private float _underlineTweenDuration = 0.2f;
        [SerializeField] private AnimationCurve _underlineTweenCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private SceneSwitcherUI _sceneSwitcherUI;
        private int _currentlySelectedGearIndex;
        private Coroutine _underlineTween;

        protected override void OnEnable()
        {
            base.OnEnable();
            SetupButtons();
            _sceneSwitcherUI = FindAnyObjectByType<SceneSwitcherUI>();
            if (_sceneSwitcherUI != null)
            {
                _sceneSwitcherUI.SceneChanged += OnSceneChanged;
                if (_sceneSwitcherUI.LoadedScene.HasValue)
                    OnSceneChanged(_sceneSwitcherUI.LoadedScene.Value);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            RemoveButtonListeners();
            if (_sceneSwitcherUI != null)
                _sceneSwitcherUI.SceneChanged -= OnSceneChanged;
        }

        private void SetupButtons()
        {
            for (int i = 0; i < _letters.Length; i++)
            {
                int buttonIndex = i;
                _letters[i].GetComponent<Button>().onClick.AddListener(() => ChangeGear(_letters[buttonIndex].gear));
            }
        }

        private void RemoveButtonListeners()
        {
            foreach (GearLetter t in _letters) t.GetComponent<Button>().onClick.RemoveAllListeners();
        }

        protected override void ApplyTheme()
        {
            Color tint = _themeData.CurrentScheme.uiColor;

            for (int i = 0; i < _letters.Length; i++)
            {
                TextMeshProUGUI tmp = _letters[i].GetComponent<TextMeshProUGUI>();
                tmp.color = i == _currentlySelectedGearIndex ? tint : _disabledLetterColor;
            }

            _underline.color = tint;
        }

        private int MapGearToIndex(CarData.Gears gear)
        {
            return gear switch
            {
                CarData.Gears.Parking => 0,
                CarData.Gears.Reverse => 1,
                CarData.Gears.Neutral => 2,
                CarData.Gears.First_Driving => 3,
                _ => -1
            };
        }

        private void ChangeGear(CarData.Gears newGear)
        {
            SelectGear(newGear);

            switch (newGear)
            {
                case CarData.Gears.Parking:
                    _sceneSwitcherUI.LoadScreenInMainViewport(Scenes.BodyControl);
                    break;
                case CarData.Gears.Reverse:
                case CarData.Gears.Neutral:
                case CarData.Gears.First_Driving:
                    _sceneSwitcherUI.LoadScreenInMainViewport(Scenes.ADAS);
                    break;
            }
        }

        private void SelectGear(CarData.Gears gear)
        {
            int newIndex = MapGearToIndex(gear);
            if (newIndex < 0) return;
            if (newIndex == _currentlySelectedGearIndex) return;

            _currentlySelectedGearIndex = newIndex;

            ApplyTheme();

            RectTransform letterRT = _letters[newIndex].transform as RectTransform;
            RectTransform underlineRT = _underline.transform as RectTransform;
            if (letterRT != null && underlineRT != null)
            {
                if (_underlineTween != null) StopCoroutine(_underlineTween);
                _underlineTween = StartCoroutine(TweenUnderlineX(underlineRT, letterRT.position.x));
            }
        }

        private void OnSceneChanged(Scenes scene)
        {
            switch (scene)
            {
                case Scenes.BodyControl:
                    SelectGear(CarData.Gears.Parking);
                    break;
                case Scenes.ADAS:
                    SelectGear(CarData.Gears.First_Driving);
                    break;
            }
        }

        private IEnumerator TweenUnderlineX(RectTransform underlineRT, float targetX)
        {
            Vector3 startPos = underlineRT.position;
            Vector3 endPos = new(targetX, startPos.y, startPos.z);

            float elapsed = 0f;
            while (elapsed < _underlineTweenDuration)
            {
                elapsed += Time.deltaTime;
                float t = _underlineTweenCurve.Evaluate(Mathf.Clamp01(elapsed / _underlineTweenDuration));
                underlineRT.position = Vector3.LerpUnclamped(startPos, endPos, t);
                yield return null;
            }

            underlineRT.position = endPos;
            _underlineTween = null;
        }
    }
}
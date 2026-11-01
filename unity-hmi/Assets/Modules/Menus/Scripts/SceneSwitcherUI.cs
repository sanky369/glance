using System;
using System.Collections;
using Modules.BodyControl.Scripts;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Modules.Menus.Scripts
{
    public enum Scenes
    {
        Main,
        BodyControl,
        ADAS,
        Cluster,
        HVAC,
    }

    public class SceneSwitcherUI : MonoBehaviour
    {
        [SerializeField] private SceneSwitcherButton[] _buttons;
        [SerializeField] private float _bodyControlToAdasDelay = 1f;
        [SerializeField] private SceneFadeOverlay _fadeOverlay;

        private Scenes? _loadedScene;
        private int _activeButtonIndex;
        private bool _isTransitioning;

        public event Action<Scenes> SceneChanged;
        public Scenes? LoadedScene => _loadedScene;

        private void OnEnable()
        {
            SetupButtons();
            LoadScreenInMainViewport(Scenes.BodyControl);
        }

        private void OnDisable()
        {
            RemoveButtonListeners();
        }

        private void SetupButtons()
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                int buttonIndex = i;
                _buttons[i].GetComponent<Button>().onClick.AddListener(() => OnMainButtonClicked(buttonIndex));
            }
        }

        private void RemoveButtonListeners()
        {
            foreach (SceneSwitcherButton t in _buttons) t.GetComponent<Button>().onClick.RemoveAllListeners();
        }

        private void SetButtonsInteractable(bool interactable)
        {
            foreach (SceneSwitcherButton b in _buttons)
                b.GetComponent<Button>().interactable = interactable;
        }

        private void OnMainButtonClicked(int clickedButtonIndex)
        {
            LoadScreenInMainViewport(_buttons[clickedButtonIndex].Scene);
        }

        private void UpdateActiveButtonForScene(Scenes scene)
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                bool isActive = _buttons[i].Scene == scene;
                _buttons[i].SetActive(isActive);
                if (isActive) _activeButtonIndex = i;
            }
        }

        public void LoadScreenInMainViewport(Scenes scene)
        {
            if (_isTransitioning) return;
            if (_loadedScene == scene) return;

            if (_loadedScene == Scenes.BodyControl && scene == Scenes.ADAS)
            {
                StartCoroutine(TransitionFromBodyControlToAdas());
                return;
            }

            StartCoroutine(SwapSceneWithFade(scene));
        }

        private IEnumerator TransitionFromBodyControlToAdas()
        {
            _isTransitioning = true;
            SetButtonsInteractable(false);

            BodyControlController bodyControl = FindAnyObjectByType<BodyControlController>();
            if (bodyControl != null)
            {
                bodyControl.TransitionToADASScene();
                yield return new WaitForSeconds(_bodyControlToAdasDelay);
            }

            yield return StartCoroutine(SwapSceneWithFade(Scenes.ADAS));

            _isTransitioning = false;
            SetButtonsInteractable(true);
        }

        private IEnumerator SwapSceneWithFade(Scenes scene)
        {
            _isTransitioning = true;
            SetButtonsInteractable(false);

            // The first load runs automatically at startup against an empty screen,
            // so fading it to black and back only adds dead time. Fade user-initiated
            // scene switches only.
            bool fade = _fadeOverlay != null && _loadedScene.HasValue;

            // Fade to black
            if (fade)
                yield return StartCoroutine(_fadeOverlay.FadeOut());

            // Unload the current scene
            if (_loadedScene.HasValue)
                yield return SceneManager.UnloadSceneAsync(_loadedScene.Value.ToString());

            // Load the new scene
            _loadedScene = scene;
            UpdateActiveButtonForScene(scene);
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(scene.ToString(), LoadSceneMode.Additive);
            yield return loadOp;

            SceneManager.SetActiveScene(SceneManager.GetSceneByName(scene.ToString()));

            // Fade back in
            if (fade)
                yield return StartCoroutine(_fadeOverlay.FadeIn());

            _isTransitioning = false;
            SetButtonsInteractable(true);

            SceneChanged?.Invoke(scene);
        }
    }
}

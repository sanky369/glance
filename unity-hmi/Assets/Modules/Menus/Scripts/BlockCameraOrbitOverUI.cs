using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Modules.Menus.Scripts
{
    public class BlockCameraOrbitOverUI : MonoBehaviour
    {
        [SerializeField] private InputActionReference _cameraOrbitX;
        [SerializeField] private InputActionReference _cameraOrbitY;

        private bool _suppressed;

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame
                && EventSystem.current != null
                && EventSystem.current.IsPointerOverGameObject())
            {
                SetCameraOrbit(false);
                _suppressed = true;
            }
            else if (_suppressed && mouse.leftButton.wasReleasedThisFrame)
            {
                SetCameraOrbit(true);
                _suppressed = false;
            }
        }

        private void OnDisable()
        {
            if (!_suppressed) return;

            SetCameraOrbit(true);
            _suppressed = false;
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
        }
    }
}

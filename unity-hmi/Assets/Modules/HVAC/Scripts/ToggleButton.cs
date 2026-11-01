using UnityEngine;
using UnityEngine.UI;

namespace Modules.HVAC.Scripts
{
    public class ToggleButton : MonoBehaviour
    {
        [SerializeField] private Color _onColor = Color.softRed;
        [SerializeField] private Color _offColor = Color.gray7;
    
        private bool _isOn;
        private Image _image;

        private void Awake()
        {
            _image = GetComponent<Image>();
        }

        private void Start()
        {
            Toggle();
        }

        public void Toggle()
        {
            _isOn = !_isOn;
            _image.color = _isOn ? _onColor : _offColor;
        }
    }
}

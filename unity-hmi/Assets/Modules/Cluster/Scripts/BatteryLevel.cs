using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Modules.Cluster
{
    [ExecuteInEditMode]
    public class BatteryLevel : MonoBehaviour
    {
        [Header("Car Data")]
        [SerializeField] private CarData _carData;
        
        [Header("Refs")]
        [SerializeField] private TextMeshProUGUI _batteryText;
        [SerializeField] private Image _batteryImage;

        private void Update() => UpdateChildren();

        private void UpdateChildren()
        {
            _batteryText.text = $"{(_carData.BatteryLevel):N0}%";
            _batteryImage.fillAmount = _carData.BatteryLevel * .01f;
        }
    }
}
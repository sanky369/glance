using Modules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AirflowPowerControl : MonoBehaviour
{
    [SerializeField] private CarData _carData;

    [SerializeField] private int minLevel = 0;
    [SerializeField] private int maxLevel = 10;

    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private Button _leftButton;
    [SerializeField] private Button _rightButton;

    private void OnEnable()
    {
        _carData.AirflowPowerChanged += ApplyState;
        ApplyState();
    }

    private void OnDisable()
    {
        _carData.AirflowPowerChanged -= ApplyState;
    }

    public void ChangePowerLevel(int increment)
    {
        SetPowerLevel(_carData.AirflowPowerLevel + increment);
    }

    public void SetPowerLevel(int level)
    {
        _carData.AirflowPowerLevel = Mathf.Clamp(level, minLevel, maxLevel);
    }

    private void ApplyState()
    {
        int level = _carData.AirflowPowerLevel;
        _label.text = level.ToString();
        _rightButton.interactable = level != maxLevel;
        _leftButton.interactable = level != minLevel;
    }
}

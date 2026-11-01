using Modules;
using UnityEngine;

public class HeatedSeat : MonoBehaviour
{
    [SerializeField] private CarData _carData;
    [SerializeField] private AirflowVisualsController.Sides _side;
    [SerializeField] private GameObject[] _heatLines;

    private void Awake()
    {
        ApplyState();
    }

    private void OnEnable()
    {
        _carData.HeatedSeatsChanged += ApplyState;
    }

    private void OnDisable()
    {
        _carData.HeatedSeatsChanged -= ApplyState;
    }

    public void CycleHeating()
    {
        int next = GetBoundLevel() + 1;
        if (next == _heatLines.Length + 1) next = 0;
        SetHeatingLevel(next);
    }

    public void SetHeatingLevel(int level)
    {
        int clamped = Mathf.Clamp(level, 0, _heatLines.Length);
        SetBoundLevel(clamped);
    }

    private void ApplyState()
    {
        int level = Mathf.Clamp(GetBoundLevel(), 0, _heatLines.Length);
        for (int i = 0; i < _heatLines.Length; i++)
            _heatLines[i].SetActive(i < level);
    }

    private int GetBoundLevel() => _side == AirflowVisualsController.Sides.Left
        ? _carData.LeftHeatedSeatLevel
        : _carData.RightHeatedSeatLevel;

    private void SetBoundLevel(int value)
    {
        if (_side == AirflowVisualsController.Sides.Left) _carData.LeftHeatedSeatLevel = value;
        else _carData.RightHeatedSeatLevel = value;
    }
}

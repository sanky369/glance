using System;
using System.Collections;
using Modules;
using UnityEngine;

public class AirflowVisualsController : MonoBehaviour
{
    [SerializeField] private CarData _carData;
    [SerializeField] private int _minTemperature = 18;
    [SerializeField] private int _maxTemperature = 30;

    [SerializeField] private Material _leftAirflowMaterial;
    [SerializeField] private Material _rightAirflowMaterial;
    [SerializeField] private Gradient _airColors; // Used to tint the airflow meshes (in the shader)
    
    private const float TweenDuration = 0.4f;

    private static readonly int TintId = Shader.PropertyToID("_Tint");
    private static readonly int CurrentTimeId = Shader.PropertyToID("_CurrentTime");
    private static readonly int ACPowerId = Shader.PropertyToID("_ACPower");
    
    private float _currentTime; // This is based on time * aircon speed

    public enum Sides
    {
        Left,
        Right
    }

    private readonly Coroutine[] _tintTweens = new Coroutine[2];
    private readonly Coroutine[] _speedTweens = new Coroutine[2];
    private float _currentSpeed;

    private void OnEnable()
    {
        if (_carData == null) return;
        _carData.AirConditioningChanged += HandleAirflowChanged;
        _carData.AirflowPowerChanged += HandleAirflowChanged;
        _carData.TemperaturesChanged += HandleTemperaturesChanged;
    }

    private void OnDisable()
    {
        if (_carData == null) return;
        _carData.AirConditioningChanged -= HandleAirflowChanged;
        _carData.AirflowPowerChanged -= HandleAirflowChanged;
        _carData.TemperaturesChanged -= HandleTemperaturesChanged;
    }

    private void Start()
    {
        HandleAirflowChanged();
        HandleTemperaturesChanged();
    }

    private void Update()
    {
        _currentTime += Time.deltaTime * _currentSpeed;
        _currentTime = _currentTime % (Mathf.PI * 2f);

        GetMaterial(Sides.Left).SetFloat(CurrentTimeId, _currentTime);
        GetMaterial(Sides.Right).SetFloat(CurrentTimeId, _currentTime);
    }

    private void HandleAirflowChanged()
    {
        int speed = _carData.AirConditioningOn ? _carData.AirflowPowerLevel : 0;
        SetAirflowSpeed(Sides.Left, speed);
        SetAirflowSpeed(Sides.Right, speed);
    }

    private void HandleTemperaturesChanged()
    {
        SetAirflowTemperature(Sides.Left, NormalizeTemperature(_carData.LeftTemperature));
        SetAirflowTemperature(Sides.Right, NormalizeTemperature(_carData.RightTemperature));
    }

    private float NormalizeTemperature(int value) =>
        Mathf.InverseLerp(_minTemperature, _maxTemperature, value);

    public void SetAirflowTemperature(Sides side, float t)
    {
        Color target = _airColors.Evaluate(t);
        int id = (int)side;
        if (_tintTweens[id] != null) StopCoroutine(_tintTweens[id]);
        _tintTweens[id] = StartCoroutine(TweenColor(GetMaterial(side), TintId, target));
    }

    public void SetAirflowSpeed(Sides side, int speed)
    {
        int id = (int)side;
        if (_speedTweens[id] != null) StopCoroutine(_speedTweens[id]);
        _speedTweens[id] = StartCoroutine(TweenSpeed(GetMaterial(side), speed));
    }

    private static IEnumerator TweenColor(Material mat, int propertyId, Color target)
    {
        Color start = mat.GetColor(propertyId);
        float elapsed = 0f;
        while (elapsed < TweenDuration)
        {
            elapsed += Time.deltaTime;
            mat.SetColor(propertyId, Color.Lerp(start, target, elapsed / TweenDuration));
            yield return null;
        }
        
        // Tween done
        mat.SetColor(propertyId, target);
    }

    private IEnumerator TweenSpeed(Material mat, float targetSpeed)
    {
        float start = _currentSpeed;
        float elapsed = 0f;
        while (elapsed < TweenDuration)
        {
            elapsed += Time.deltaTime;
            _currentSpeed = Mathf.Lerp(start, targetSpeed, elapsed / TweenDuration);
            mat.SetFloat(ACPowerId, _currentSpeed * 0.1f);
            yield return null;
        }
        
        // Tween done
        _currentSpeed = targetSpeed;
        mat.SetFloat(ACPowerId, _currentSpeed * 0.1f);
    }

    private Material GetMaterial(Sides side) =>
        side == Sides.Left ? _leftAirflowMaterial : _rightAirflowMaterial;
}

using System;
using UnityEngine;

namespace Modules
{
    public class AnimatedDemo : MonoBehaviour
    {
        [Header("Car Data")]
        [SerializeField] private CarData _carData;
    
        [Header("Animation")]
        [SerializeField] private float _animationDuration;
        [SerializeField] private AnimationCurve _speedCurve;

        private void Update()
        {
            if (_carData == null || _speedCurve == null || _animationDuration <= 0f) return;

            float currentProgress = Mathf.Repeat(Time.time / _animationDuration, 1f);
            _carData.Speed = _speedCurve.Evaluate(currentProgress) * CarData.MaxSpeed;
        }
    }
}

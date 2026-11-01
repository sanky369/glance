using UnityEngine;

namespace HMI_Hub_Template
{
    public class PedestrianView : MonoBehaviour
    {
        [Header("Movement")]
        public float smoothSpeed = 15f;

        [Header("Animation")]
        [Tooltip("Animator bool parameter name toggled to true while walking, false while idle/waiting. " +
                 "Leave empty to skip animation control.")]
        public string walkBoolParameter = "IsWalking";

        private PedestrianData _targetData;
        private bool _isInitialized = false;
        
        private Animator _animator;
        private int _walkHash;
        private bool _animatorResolved;

        public void UpdateData(PedestrianData data)
        {
            _targetData = data;

            if (!_isInitialized)
            {
                transform.position = data.position;
                transform.rotation = data.rotation;
                _isInitialized = true;
            }
            
            if (!_animatorResolved) //lazy find animator
            {
                _animator = GetComponentInChildren<Animator>();
                if (_animator != null && !string.IsNullOrEmpty(walkBoolParameter))
                    _walkHash = Animator.StringToHash(walkBoolParameter);
                _animatorResolved = true;
            }

            if (_animator != null && !string.IsNullOrEmpty(walkBoolParameter))
                _animator.SetBool(_walkHash, !data.isWaiting);
                
        }

        void Update()
        {
            if (!_isInitialized) return;
            
            transform.position = Vector3.Lerp(
                transform.position, _targetData.position, Time.deltaTime * smoothSpeed);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, _targetData.rotation, Time.deltaTime * smoothSpeed);
        }
    }
}

using UnityEngine;

namespace HMI_Hub_Template
{
    public class WheelRotation : MonoBehaviour
    {
        public Transform[] wheels;

        public float wheelRadius = 0.35f;
        public bool invertRotation;

        private Vector3 _lastPosition;
        private bool _hasLastPosition;

        void LateUpdate()
        {
            if (!_hasLastPosition)
            {
                _lastPosition = transform.position;
                _hasLastPosition = true;
                return;
            }

            Vector3 movement = transform.position - _lastPosition;
            float distance = Vector3.Dot(movement, transform.forward);

            if (Mathf.Abs(distance) > 0.0001f && wheelRadius > 0f)
            {
                float circumference = 2f * Mathf.PI * wheelRadius;
                float rotationDegrees = distance / circumference * 360f;

                if (invertRotation)
                    rotationDegrees *= -1f;

                for (int i = 0; i < wheels.Length; i++)
                {
                    if (wheels[i] != null)
                        wheels[i].Rotate(rotationDegrees, 0f, 0f, Space.Self);
                }
            }

            _lastPosition = transform.position;
        }
    }
}
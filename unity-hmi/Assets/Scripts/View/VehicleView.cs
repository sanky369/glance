using UnityEngine;

namespace HMI_Hub_Template
{
    public class VehicleView : MonoBehaviour
    {
        [Tooltip("How fast the visual model catches up to the simulation data")]
        public float smoothSpeed = 20f;
        
        private VehicleData _targetData;
        private bool _isInitialized = false;

        public void UpdateData(VehicleData data)
        {
            _targetData = data;
            
            // Snap to position on the very first frame to prevent models flying in from (0,0,0)
            if (!_isInitialized)
            {
                transform.position = data.position;
                transform.rotation = data.rotation;
                _isInitialized = true;
            }
        }

        void Update()
        {
            if (!_isInitialized) return;

            transform.position = Vector3.Lerp(transform.position, _targetData.position, Time.deltaTime * smoothSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetData.rotation, Time.deltaTime * smoothSpeed);
        }
    }
}
using System.Collections;
using Modules;
using Unity.Cinemachine;
using UnityEngine;

namespace HMI_Hub_Template
{
    public class HeroCameraController : MonoBehaviour
    {
        public DataCore dataCore;
        public HeroSimSettings heroSettings;
        public HeroSensorViewSystem heroSensorViewSystem;

        [Header("Cinemachine")]
        [Tooltip("When true, Cinemachine drives the camera. When false, this script moves the camera transform directly with SmoothDamp.")]
        [SerializeField] private bool _useCinemachine = true;

        [Tooltip("The main follow camera used during normal simulation. Its Follow/LookAt are assigned at runtime to the spawned hero transform.")]
        [SerializeField] private CinemachineCamera _heroCarCamera;

        [Tooltip("Static intro camera placed anywhere in the scene. Give it a higher Priority than the follow cam. Leave null to skip the intro.")]
        [SerializeField] private CinemachineCamera _introCam;

        [Tooltip("Seconds to hold on the intro cam before the blend to the follow cam begins.")]
        [SerializeField] private float _introDuration = 2.5f;

        private Transform _heroTransform;
        private Vector3 _posVelocity;

        private void Awake()
        {
            if (_useCinemachine && _heroCarCamera != null) _heroCarCamera.gameObject.SetActive(false);
            GetComponent<Camera>().enabled = false;
        }

        private void OnEnable() => heroSettings.HeroVehicleIdChanged += HeroIdChanged;
        private void OnDisable() => heroSettings.HeroVehicleIdChanged -= HeroIdChanged;

        private void Start()
        {
            StartCoroutine(PlayIntro());
        }

        private IEnumerator PlayIntro()
        {
            yield return null; //possibly replace the wait by an initialization event

            _heroTransform = heroSensorViewSystem.HeroTransform; 

            if (_useCinemachine && _heroCarCamera != null && _heroTransform != null)
            {
                _heroCarCamera.Follow = _heroCarCamera.LookAt = _heroTransform;
                _heroCarCamera.gameObject.SetActive(true);
                GetComponent<Camera>().enabled = true;
            }

            if (_useCinemachine && _introCam != null && _introDuration > 0)
            {
                _introCam.gameObject.SetActive(true);
                SimulationSystem.IsPaused = true;
                yield return null;
                _introCam.gameObject.SetActive(false);
                yield return new WaitForSeconds(_introDuration);
            }

            SimulationSystem.IsPaused = false;
        }

        private void LateUpdate()
        {
            if (_useCinemachine || _heroTransform == null || heroSettings == null) return;

            TryFindHeroCarData(out VehicleData hero);

            Vector3 targetPos = _heroTransform.position + (_heroTransform.rotation * heroSettings.cameraOffset);
            Quaternion targetRot = _heroTransform.rotation * Quaternion.Euler(heroSettings.cameraTilt, 0f, 0f);

            heroSettings.GetCurrentLerpSpeeds(hero.currentSpeed, out float posLerp, out float rotLerp);

            float posSmoothTime = 1f / Mathf.Max(posLerp, 0.01f);
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _posVelocity, posSmoothTime);

            float rotT = 1f - Mathf.Exp(-rotLerp * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotT);
        }

        private bool TryFindHeroCarData(out VehicleData heroCar)
        {
            for (int i = 0; i < dataCore.activeVehicles.Count; i++)
            {
                if (dataCore.activeVehicles[i].vehicleId == heroSettings.heroVehicleId)
                {
                    heroCar = dataCore.activeVehicles[i];
                    return true;
                }
            }
            heroCar = default;
            return false;
        }

        private void HeroIdChanged(int newHeroId)
        {
            _heroTransform = heroSensorViewSystem.HeroTransform;

            if (_useCinemachine && _heroCarCamera != null && _heroTransform != null)
            {
                _heroCarCamera.Follow = _heroCarCamera.LookAt = _heroTransform;
                _heroCarCamera.OnTargetObjectWarped(_heroTransform, Vector3.zero);
            }
            _posVelocity = Vector3.zero;
        }
    }
}

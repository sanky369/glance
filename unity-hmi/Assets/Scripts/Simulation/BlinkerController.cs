using Modules;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HMI_Hub_Template
{
    /// <summary>
    /// The only writer of <see cref="CarData.CurrentBlinkerState"/>.
    ///
    /// Three sources feed it, in descending priority:
    ///   Hazard  — the hazard key. No timer, only the hazard key clears it, beats everything.
    ///   Manual  — the turn-signal keys, or a lane change the driver actually started.
    ///             Runs for HeroSimSettings.manualBlinkerDuration, then re-evaluates Auto.
    ///   Auto    — the road turns at the junction ahead. No timer; clears itself once the
    ///             hero is settled on a straight lane again.
    ///
    /// <see cref="LightController"/> is the view for this state and does not write to it.
    /// </summary>
    public class BlinkerController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private CarData _carData;
        [SerializeField] private HeroSensorDataLayer _heroSensorData;
        [SerializeField] private HeroSimSettings _heroSettings;

        [Header("Keyboard Shortcuts")]
        [SerializeField] private Key _leftKey = Key.Q;
        [SerializeField] private Key _rightKey = Key.E;
        [SerializeField] private Key _hazardKey = Key.H;

        [Header("Lane Changes")]
        [Tooltip("Treat a driver-initiated lane change as a manual blinker on that side. Blocked lane changes never trigger it.")]
        [SerializeField] private bool _laneChangesTriggerBlinker = true;

        // Manual state. _manualExpiry is a Time.time stamp; manual is live while it is in the future.
        private CarData.BlinkerState _manualSide = CarData.BlinkerState.Off;
        private float _manualExpiry;

        private bool _hazardOn;

        // Lets us spot a new lane change without needing a C# event across the scene boundary.
        private int _lastSeenLaneChangeCount;

        private float ManualDuration => _heroSettings != null ? _heroSettings.manualBlinkerDuration : 5f;
        private float StraightAngle => _heroSettings != null ? _heroSettings.autoBlinkerStraightAngle : 15f;
        private bool ManualIsLive => _manualSide != CarData.BlinkerState.Off && Time.time < _manualExpiry;

        private void OnEnable()
        {
            _manualSide = CarData.BlinkerState.Off;
            _manualExpiry = 0f;
            _hazardOn = false;

            if (_heroSensorData != null)
                _lastSeenLaneChangeCount = _heroSensorData.heroLaneChangeCount;

            if (_carData != null)
            {
                _carData.CurrentBlinkerState = CarData.BlinkerState.Off;
                _carData.CurrentBlinkerSource = CarData.BlinkerSource.None;
            }
        }

        private void Update()
        {
            if (_carData == null) return;

            ReadKeyboard();
            ReadLaneChanges();
            Arbitrate();
        }

        private void ReadKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[_hazardKey].wasPressedThisFrame)
            {
                ToggleHazard();
                return;
            }

            if (keyboard[_leftKey].wasPressedThisFrame) TriggerManualBlinker(CarData.BlinkerState.Left);
            if (keyboard[_rightKey].wasPressedThisFrame) TriggerManualBlinker(CarData.BlinkerState.Right);
        }

        /// <summary>
        /// Public entry point for HMI UI (touch stalk buttons) and tests. Same behaviour as the
        /// turn-signal keys: same side restarts the timer, the other side switches over.
        /// </summary>
        public void TriggerManualBlinker(CarData.BlinkerState side)
        {
            if (side != CarData.BlinkerState.Left && side != CarData.BlinkerState.Right) return;
            TriggerManual(side);
        }

        /// <summary>
        /// Public entry point for HMI UI and tests. Hazards toggle, have no timer,
        /// and outrank everything else.
        /// </summary>
        public void ToggleHazard()
        {
            _hazardOn = !_hazardOn;
        }

        private void ReadLaneChanges()
        {
            if (!_laneChangesTriggerBlinker || _heroSensorData == null) return;

            int count = _heroSensorData.heroLaneChangeCount;
            if (count == _lastSeenLaneChangeCount) return;

            _lastSeenLaneChangeCount = count;
            TriggerManual(_heroSensorData.heroLaneChangeWasToLeft
                ? CarData.BlinkerState.Left
                : CarData.BlinkerState.Right);
        }

        /// <summary>
        /// Starts, extends or switches the manual blinker. Same side restarts the timer rather
        /// than cancelling, the other side switches over on a fresh timer.
        /// </summary>
        private void TriggerManual(CarData.BlinkerState side)
        {
            _manualSide = side;
            _manualExpiry = Time.time + ManualDuration;
        }

        private void Arbitrate()
        {
            CarData.BlinkerState state;
            CarData.BlinkerSource source;

            if (_hazardOn)
            {
                state = CarData.BlinkerState.Hazard;
                source = CarData.BlinkerSource.Hazard;
            }
            else if (ManualIsLive)
            {
                state = _manualSide;
                source = CarData.BlinkerSource.Manual;
            }
            else
            {
                // Manual has lapsed. Auto picks up whatever the road is still doing, which
                // covers "timer expired mid-turn" without any extra bookkeeping.
                _manualSide = CarData.BlinkerState.Off;
                state = EvaluateAuto();
                source = state == CarData.BlinkerState.Off
                    ? CarData.BlinkerSource.None
                    : CarData.BlinkerSource.Auto;
            }

            _carData.CurrentBlinkerState = state;
            _carData.CurrentBlinkerSource = source;
        }

        /// <summary>
        /// Recomputed from scratch every frame so it stays a pure function of road position.
        /// Returns Left/Right while the hero is approaching or driving through a turn the road
        /// makes, Off once it is straight on an ordinary lane. Lane changes are never Auto.
        /// </summary>
        private CarData.BlinkerState EvaluateAuto()
        {
            if (_heroSensorData == null) return CarData.BlinkerState.Off;

            LaneRegistry registry = LaneRegistry.Instance;
            if (registry == null || registry.vehicleLaneData == null) return CarData.BlinkerState.Off;

            int laneId = _heroSensorData.heroLaneId;
            if (!IsLaneInRange(registry, laneId)) return CarData.BlinkerState.Off;

            // The road here is authored with smooth seams: the yaw change happens *inside* the
            // intersection lane, not at the hand-off between lanes. So the turn side always
            // comes from an intersection lane's own entry-vs-exit tangents.

            // Already inside the junction — keeps the blinker lit for the rest of the turn.
            if (registry.vehicleLaneData[laneId].isIntersection)
                return SideOfLaneCurvature(registry, laneId);

            // Approaching one: the committed next lane knows which way it will bend.
            // Junctions only (rule 6) — a committed ordinary lane never blinks.
            int nextLaneId = _heroSensorData.heroCommittedNextLaneId;
            if (IsLaneInRange(registry, nextLaneId) && registry.vehicleLaneData[nextLaneId].isIntersection)
                return SideOfLaneCurvature(registry, nextLaneId);

            return CarData.BlinkerState.Off;
        }

        private static bool IsLaneInRange(LaneRegistry registry, int laneId)
        {
            return laneId >= 0 && laneId < registry.vehicleLaneData.Length;
        }

        private CarData.BlinkerState SideOfLaneCurvature(LaneRegistry registry, int laneId)
        {
            float length = registry.vehicleLaneData[laneId].length;
            Vector3 entryDir = registry.GetVehicleWorldDirection(laneId, 0f);
            Vector3 exitDir = registry.GetVehicleWorldDirection(laneId, length);
            return SideOf(entryDir, exitDir);
        }

        /// <summary>
        /// Signed yaw between two tangents, flattened to the ground plane. Anything gentler
        /// than the straight-angle threshold counts as going straight.
        /// </summary>
        private CarData.BlinkerState SideOf(Vector3 fromDir, Vector3 toDir)
        {
            fromDir.y = 0f;
            toDir.y = 0f;

            if (fromDir.sqrMagnitude < 1e-6f || toDir.sqrMagnitude < 1e-6f)
                return CarData.BlinkerState.Off;

            float yaw = Vector3.SignedAngle(fromDir, toDir, Vector3.up);

            if (Mathf.Abs(yaw) < StraightAngle) return CarData.BlinkerState.Off;

            return yaw > 0f ? CarData.BlinkerState.Right : CarData.BlinkerState.Left;
        }
    }
}

using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace Modules.Cluster
{
    /// <summary>
    /// Spawns tick marks and number labels around an arc for a speed dial.
    /// </summary>
    [RequireComponent(typeof(RectTransform)), ExecuteAlways]
    public class SpeedDialController : ThemeResponsive
    {
        [Header("Car Data")]
        [SerializeField] private CarData _carData;
        
        [Header("Prefabs")]
        public GameObject tickPrefab;
        public GameObject numberPrefab;

        [Header("Geometry")]
        [Tooltip("Design resolution. Radii are authored at this size; layout scales proportionally.")]
        public float referenceSize = 1000f;
        public float tickRadius = 200f;
        public float numberRadius = 155f;
        public float startAngle = 225f;
        public float sweepAngle = 270f;
        public bool rotateTicks = true;

        [Header("Speed Range")]
        public float maxSpeed = 200f;
        public float tickStep = 5f; // finest tick mark interval
        public float labelBaseInterval = 5f; // finest label interval (must be a multiple of tickStep)
        [Min(2)] public int levelMultiplier = 2;

        [Header("Fade Thresholds")]
        [Range(0f, 1f)] public float tickFadeStart = 0.08f; // fully visible above this
        [Range(0f, 1f)] public float tickFadeEnd = 0.03f; // fully invisible below this
        [Range(0f, 1f)] public float labelFadeStart = 0.15f; // labels fade earlier than ticks
        [Range(0f, 1f)] public float labelFadeEnd = 0.07f;
        [Range(0f, 0.5f)] public float labelFadeInZone = 0.15f; // labels start fading in this fraction beyond displayMax

        [Header("Display")]
        public float minDisplayRange = 40f;
        [Range(0.1f, 1f)] public float headroom = 0.7f;
        [Range(0f, 1f)] public float zoomLevel = 0.2f;

        [Header("Arm")]
        [SerializeField] private RectTransform _arm;
        
        [Header("Text")]
        [SerializeField] private TextMeshProUGUI _speedText;

        private float DisplayMax => Mathf.Max(minDisplayRange, maxSpeed * zoomLevel);

        private class Entry
        {
            public RectTransform tickRT;
            public Graphic tickGraphic; // cached for alpha
            public RectTransform numberRT; // null when this tick has no label
            public TextMeshProUGUI numberTmp; // null when this tick has no label
            public float speedValue;
            public float tickWeight; // index-based — drives tick mark fade
            public float labelWeight; // value-based — drives number label fade; 0 = never shown
        }

        [SerializeField] [HideInInspector] private List<RectTransform> spawnedTicks = new();
        [SerializeField] [HideInInspector] private List<RectTransform> spawnedNumbers = new();

        [SerializeField] [HideInInspector] private GameObject lastTickPrefab;
        [SerializeField] [HideInInspector] private GameObject lastNumberPrefab;
        [SerializeField] [HideInInspector] private float lastMaxSpeed = -1f;
        [SerializeField] [HideInInspector] private float lastTickStep = -1f;
        [SerializeField] [HideInInspector] private float lastLabelBaseInterval = -1f;

        private readonly List<Entry> entries = new();
        private float _currentSpeed;
        private string[] _speedStrings;
        private int _lastSpeedIdx = -1;

        protected override void ApplyTheme()
        {
            if (_themeData == null) return;
            Color tint = _themeData.CurrentScheme.uiColor;

            foreach (RectTransform numberRT in spawnedNumbers)
            {
                if (numberRT == null) continue;
                TextMeshProUGUI tmp = numberRT.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null)
                    tmp.color = tmp.color.ChangeIntoColorKeepAlpha(tint);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            CleanNulls(spawnedTicks);
            CleanNulls(spawnedNumbers);

            if (spawnedTicks.Count == 0 && tickPrefab != null)
            {
                Rebuild();
            }
            else
            {
                RebuildEntries();
                Reposition();
            }
        }

        private void OnValidate()
        {
            if (!enabled) return;
            
#if UNITY_EDITOR
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                Apply();
            };
#endif
        }

        private void Apply()
        {
            bool structural = tickPrefab != lastTickPrefab
                              || numberPrefab != lastNumberPrefab
                              || !Mathf.Approximately(maxSpeed, lastMaxSpeed)
                              || !Mathf.Approximately(tickStep, lastTickStep)
                              || !Mathf.Approximately(labelBaseInterval, lastLabelBaseInterval);

            if (structural) Rebuild();
            else Reposition();
        }

        public void Rebuild()
        {
            DestroyList(spawnedTicks);
            DestroyList(spawnedNumbers);
            spawnedTicks.Clear();
            spawnedNumbers.Clear();
            entries.Clear();

            lastTickPrefab = tickPrefab;
            lastNumberPrefab = numberPrefab;
            lastMaxSpeed = maxSpeed;
            lastTickStep = tickStep;
            lastLabelBaseInterval = labelBaseInterval;

            if (tickPrefab == null || tickStep <= 0f || maxSpeed <= 0f) return;

            RectTransform parent = (RectTransform)transform;
            int count = Mathf.RoundToInt(maxSpeed / tickStep);

            for (int i = 0; i <= count; i++)
            {
                float v = i * tickStep;

                RectTransform tickRT = SpawnPrefab(tickPrefab, parent);
                spawnedTicks.Add(tickRT);
                tickRT.name = $"Tick_{v}";

                // Only spawn a number label for clean multiples of labelBaseInterval
                float lw = ComputeLabelWeight(v);
                RectTransform numberRT = null;
                TextMeshProUGUI numberTmp = null;
                if (lw > 0f && numberPrefab != null)
                {
                    numberRT = SpawnPrefab(numberPrefab, parent);
                    spawnedNumbers.Add(numberRT);
                    numberTmp = numberRT.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (numberTmp != null) numberTmp.text = Mathf.RoundToInt(v).ToString();
                    numberRT.name = $"Label_{v}";
                }

                entries.Add(new Entry
                {
                    tickRT = tickRT,
                    tickGraphic = tickRT.GetComponentInChildren<Graphic>(),
                    numberRT = numberRT,
                    numberTmp = numberTmp,
                    speedValue = v,
                    tickWeight = ComputeTickWeight(i),
                    labelWeight = lw
                });
            }

            BuildSpeedStrings();
            Reposition();
            ApplyTheme();
        }

        private void BuildSpeedStrings()
        {
            int max = Mathf.RoundToInt(maxSpeed);
            _speedStrings = new string[max + 1];
            for (int i = 0; i <= max; i++)
                _speedStrings[i] = i.ToString();
        }

        // Reconstructs the Entry list from already-spawned objects (e.g. after domain reload)
        private void RebuildEntries()
        {
            entries.Clear();
            int count = Mathf.RoundToInt(maxSpeed / tickStep);

            if (spawnedTicks.Count != count + 1)
            {
                Rebuild();
                return;
            }

            int labelIdx = 0;
            for (int i = 0; i <= count; i++)
            {
                float v = i * tickStep;
                float lw = ComputeLabelWeight(v);

                RectTransform numberRT = null;
                TextMeshProUGUI numberTmp = null;
                if (lw > 0f && numberPrefab != null && labelIdx < spawnedNumbers.Count)
                {
                    numberRT = spawnedNumbers[labelIdx++];
                    numberTmp = numberRT != null ? numberRT.GetComponentInChildren<TextMeshProUGUI>(true) : null;
                    if (numberTmp != null) numberTmp.text = Mathf.RoundToInt(v).ToString();
                }

                entries.Add(new Entry
                {
                    tickRT = spawnedTicks[i],
                    tickGraphic = spawnedTicks[i].GetComponentInChildren<Graphic>(),
                    numberRT = numberRT,
                    numberTmp = numberTmp,
                    speedValue = v,
                    tickWeight = ComputeTickWeight(i),
                    labelWeight = lw
                });
            }

            if (_speedStrings == null) BuildSpeedStrings();
        }

        private void Reposition()
        {
            if (entries.Count == 0 || tickPrefab == null) return;

            Vector2 tickHalfNorm = PrefabHalfNorm(tickPrefab, referenceSize);
            Vector2 numberHalfNorm = numberPrefab != null ? PrefabHalfNorm(numberPrefab, referenceSize) : Vector2.zero;

            float displayMax = DisplayMax;
            float fadeInRange = displayMax * labelFadeInZone;

            UpdateArm(displayMax);

            foreach (Entry e in entries)
            {
                bool inRange = e.speedValue <= displayMax + 0.001f;

                float tickAlpha, labelAlpha;
                if (inRange)
                {
                    tickAlpha = FadeAlpha(e.tickWeight, displayMax, tickFadeStart, tickFadeEnd);
                    labelAlpha = FadeAlpha(e.labelWeight, displayMax, labelFadeStart, labelFadeEnd);
                }
                else if (fadeInRange > 0f && e.speedValue <= displayMax + fadeInRange)
                {
                    // Just beyond the visible range — fade in at the arc edge.
                    float proximity = Mathf.SmoothStep(0f, 1f, 1f - (e.speedValue - displayMax) / fadeInRange);
                    tickAlpha = FadeAlpha(e.tickWeight, displayMax, tickFadeStart, tickFadeEnd) * proximity;
                    labelAlpha = FadeAlpha(e.labelWeight, displayMax, labelFadeStart, labelFadeEnd) * proximity;
                }
                else
                {
                    tickAlpha = 0f;
                    labelAlpha = 0f;
                }

                float arcT = e.speedValue / displayMax;
                float angleDeg = startAngle - sweepAngle * arcT;

                if (e.tickRT != null)
                {
                    PlaceElement(e.tickRT, angleDeg, tickRadius, tickHalfNorm, referenceSize);
                    if (rotateTicks)
                        e.tickRT.localRotation = Quaternion.Euler(0f, 0f, angleDeg + 90f);
                    if (e.tickGraphic != null)
                        SetAlpha(e.tickGraphic, tickAlpha);
                }

                if (e.numberRT != null)
                {
                    PlaceElement(e.numberRT, angleDeg, numberRadius, numberHalfNorm, referenceSize);
                    if (e.numberTmp != null)
                        SetAlpha(e.numberTmp, labelAlpha);
                }
            }
        }

        /// <summary>
        /// Sets zoomLevel so the displayed range tracks the current speed
        /// (at least minDisplayRange) and calls Reposition.
        /// </summary>
        public void SetSpeed(float speed)
        {
            _currentSpeed = speed;
            float target = Mathf.Min(maxSpeed, Mathf.Lerp(minDisplayRange, maxSpeed / headroom, speed / maxSpeed));
            zoomLevel = target / maxSpeed;
            int speedIdx = Mathf.Clamp(Mathf.RoundToInt(speed), 0, _speedStrings.Length - 1);
            if (speedIdx != _lastSpeedIdx)
            {
                _lastSpeedIdx = speedIdx;
                _speedText.text = _speedStrings[speedIdx];
            }
            Reposition();
        }

        private void UpdateArm(float displayMax)
        {
            if (_arm == null) return;
            float t = displayMax > 0f ? Mathf.Clamp01(_currentSpeed / displayMax) : 0f;
            float angleDeg = startAngle - sweepAngle * t;
            _arm.localRotation = Quaternion.Euler(0f, 0f, angleDeg + 90f);
        }

        /// <summary>
        /// Tick weight — index-based.
        /// tickStep × levelMultiplier^k, where k = how many times levelMultiplier divides i.
        /// Tick at i=0 gets maxSpeed so it is always fully visible.
        /// </summary>
        private float ComputeTickWeight(int i)
        {
            if (i == 0) return maxSpeed;
            int k = 0, n = i;
            while (n % levelMultiplier == 0)
            {
                n /= levelMultiplier;
                k++;
            }

            return tickStep * Mathf.Pow(levelMultiplier, k);
        }

        /// <summary>
        /// Label weight — value-based.
        /// Returns the largest labelBaseInterval × levelMultiplier^k that divides v,
        /// or 0 if v is not a multiple of labelBaseInterval at all (never show a label).
        /// Speed 0 gets maxSpeed weight so it is always labeled.
        /// </summary>
        private float ComputeLabelWeight(float v)
        {
            if (v < 0.001f) return maxSpeed;

            // Reject values that are not multiples of the base interval.
            float eps = labelBaseInterval * 0.001f;
            if (v % labelBaseInterval > eps) return 0f;

            float w = labelBaseInterval;
            float next = w * levelMultiplier;
            while (v % next <= next * 0.001f)
            {
                w = next;
                next *= levelMultiplier;
            }

            return w;
        }

        /// Converts a weight into a 0–1 alpha using the supplied fade thresholds.
        private float FadeAlpha(float weight, float displayMax, float fadeStart, float fadeEnd)
        {
            if (weight <= 0f) return 0f;
            float arcFraction = weight / displayMax;
            float t = Mathf.InverseLerp(fadeEnd, fadeStart, arcFraction);
            return Mathf.SmoothStep(0f, 1f, t);
        }

        private void PlaceElement(RectTransform rt, float angleDeg, float radius, Vector2 halfNorm, float refSize)
        {
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector2 dir = new(-Mathf.Cos(angleRad), -Mathf.Sin(angleRad));
            Vector2 center = new Vector2(0.5f, 0.5f) + dir * (radius / refSize);
            Vector2 min = center - halfNorm;
            Vector2 max = center + halfNorm;

            // Skip the RectTransform write entirely when nothing moved: setting
            // anchorMin/anchorMax even to an unchanged value still marks the
            // rect dirty and forces TMP to rebuild its mesh next frame.
            if (rt.anchorMin != min)
            {
                rt.anchorMin = min;
                rt.offsetMin = Vector2.zero;
            }
            if (rt.anchorMax != max)
            {
                rt.anchorMax = max;
                rt.offsetMax = Vector2.zero;
            }
        }

        private static Vector2 PrefabHalfNorm(GameObject prefab, float refSize)
        {
            Vector2 s = ((RectTransform)prefab.transform).sizeDelta;
            return s * 0.5f / refSize;
        }

        private static void SetAlpha(Graphic g, float alpha)
        {
            g.canvasRenderer.SetAlpha(alpha);
        }

        private RectTransform SpawnPrefab(GameObject prefab, RectTransform parent)
        {
            GameObject go;
#if UNITY_EDITOR
            go = Application.isPlaying
                ? Instantiate(prefab, parent)
                : (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
#else
            go = Instantiate(prefab, parent);
#endif
            return go.GetComponent<RectTransform>();
        }

        private void DestroyList(List<RectTransform> list)
        {
            foreach (RectTransform rt in list)
            {
                if (rt == null) continue;
                if (Application.isPlaying) Destroy(rt.gameObject);
                else DestroyImmediate(rt.gameObject);
            }
        }

        private static void CleanNulls(List<RectTransform> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i] == null)
                    list.RemoveAt(i);
        }

        private void Update()
        {
            if (IsPrefabStage()) return;

            if(Math.Abs(_carData.Speed - _currentSpeed) > .1f)
                SetSpeed(_carData.Speed);
        }

        /// <summary>
        /// Guard to prevent the Prefab from destroying itself due to ExecuteAlways
        /// </summary>
        /// <returns></returns>
        private static bool IsPrefabStage()
        {
#if UNITY_EDITOR
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) return true;
#endif
            return false;
        }
    }
}
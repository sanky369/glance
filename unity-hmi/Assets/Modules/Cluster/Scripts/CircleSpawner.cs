using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Modules.Cluster
{
    [RequireComponent(typeof(RectTransform))]
    public class CircleSpawner : ThemeResponsive
    {
        public GameObject prefab;
        [Tooltip("Design resolution. Radius is authored at this size; layout scales proportionally.")]
        public float referenceSize = 1000f;
        public float radius = 200f;
        public float startAngle;
        public float sweepAngle = 270f;
        public bool includeEndpoint = true;
        public bool rotatePrefab = true;
        public bool applyTheme = true;

        [Header("Distribution")]
        public DistributionType distributionType = DistributionType.Simple;
        
        [Range(1, 360)] public int count = 5; // Count doesn't apply to DistributionType.IndividualLetters
        
        public int startNumber;
        public int endNumber = 100;

        public string textToDistribute = "Example";

        [SerializeField, HideInInspector] private List<RectTransform> spawned = new();
        [SerializeField, HideInInspector] private GameObject lastPrefab;
        [SerializeField, HideInInspector] private int lastCount = -1;

        public enum DistributionType
        {
            Simple,
            NumbersRange,
            IndividualLetters,
        }
        
        /// <summary>
        /// Gives the final count of the elements to spawn.
        /// When the distribution type is IndividualLetters, the count variable is overridden by the length of the text.
        /// </summary>
        private int FinalCount => distributionType == DistributionType.IndividualLetters ? textToDistribute.Length : count;

        protected override void OnEnable()
        {
            base.OnEnable();
            
            for (int i = spawned.Count - 1; i >= 0; i--)
                if (spawned[i] == null)
                    spawned.RemoveAt(i);

            if (spawned.Count == 0 && prefab != null && FinalCount > 0)
                Rebuild();
            else
                Reposition();
        }

        private void OnValidate()
        {
            if(!enabled) return;

#if UNITY_EDITOR
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                Apply();
            };
#else
            Apply();
#endif
        }

        private void Apply()
        {
            bool structuralChange = prefab != lastPrefab || FinalCount != lastCount;

            if (structuralChange)
                Rebuild();
            else
                Reposition();
        }

        public void Rebuild()
        {
            for (int i = spawned.Count - 1; i >= 0; i--)
                if (spawned[i] != null)
                {
                    if (Application.isPlaying)
                        Destroy(spawned[i].gameObject);
                    else
                        DestroyImmediate(spawned[i].gameObject);
                }

            spawned.Clear();

            lastPrefab = prefab;
            lastCount = FinalCount;

            if (prefab == null || FinalCount == 0) return;

            RectTransform parent = (RectTransform)transform;

            for (int i = 0; i < FinalCount; i++)
            {
                GameObject go;
#if UNITY_EDITOR
                if (Application.isPlaying)
                    go = Instantiate(prefab, parent);
                else
                    go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
#else
                go = Instantiate(prefab, parent);
#endif
                
                RectTransform rt = go.GetComponent<RectTransform>();
                spawned.Add(rt);
            }

            Reposition();
            if(applyTheme) ApplyTheme();
        }

        private void Reposition()
        {
            if (spawned.Count == 0 || prefab == null) return;

            Vector2 tickSize = ((RectTransform)prefab.transform).sizeDelta;
            Vector2 halfNormalized = tickSize * 0.5f / referenceSize;

            int divisions = includeEndpoint && spawned.Count > 1 ? spawned.Count - 1 : spawned.Count;

            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] == null) continue;

                float t = divisions == 0 ? 0f : (float)i / divisions;
                float angleDeg = startAngle + -sweepAngle * t;
                float angleRad = angleDeg * Mathf.Deg2Rad;

                Vector2 dir = new(-Mathf.Cos(angleRad), -Mathf.Sin(angleRad));

                Vector2 center = new Vector2(0.5f, 0.5f) + dir * (radius / referenceSize);

                spawned[i].anchorMin = center - halfNormalized;
                spawned[i].anchorMax = center + halfNormalized;
                spawned[i].offsetMin = Vector2.zero;
                spawned[i].offsetMax = Vector2.zero;

                if (rotatePrefab)
                    spawned[i].localRotation = Quaternion.Euler(0f, 0f, angleDeg + 90f);
                else
                    spawned[i].localRotation = Quaternion.identity;

                TextMeshProUGUI tmp;
                switch (distributionType)
                {
                    case DistributionType.NumbersRange:
                        tmp = spawned[i].GetComponentInChildren<TextMeshProUGUI>(true);
                        if (tmp == null)
                        {
                            Debug.LogWarning("Wrong Prefab type?");
                            return;
                        }
                        string number = Mathf.RoundToInt(Mathf.Lerp(startNumber, endNumber, t)).ToString();
                        tmp.text = number;
                        spawned[i].name = number;
                        break;
                    
                    case DistributionType.IndividualLetters:
                        tmp = spawned[i].GetComponentInChildren<TextMeshProUGUI>(true);
                        if (tmp == null)
                        {
                            Debug.LogWarning("Wrong Prefab type?");
                            return;
                        }
                        string letter = textToDistribute[i].ToString();
                        tmp.text = letter;
                        spawned[i].name = letter;
                        break;
                }
            }
        }

        protected override void ApplyTheme()
        {
            if (!applyTheme) return;
            
            foreach (RectTransform rectTransform in spawned)
            {
                switch (distributionType)
                {
                    case DistributionType.Simple:
                        Image image = rectTransform.GetComponent<Image>();
                        image.color = image.color.ChangeIntoColorKeepAlpha(_themeData.CurrentScheme.uiColor);
                        break;
                    
                    case DistributionType.NumbersRange:
                    case DistributionType.IndividualLetters:
                        TextMeshProUGUI textComponent = rectTransform.GetComponentInChildren<TextMeshProUGUI>(true);
                        textComponent.color = textComponent.color.ChangeIntoColorKeepAlpha(_themeData.CurrentScheme.uiColor);
                        break;
                }
            }
        }
    }
}
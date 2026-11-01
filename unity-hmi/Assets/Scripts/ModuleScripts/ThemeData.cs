using System;
using UnityEngine;

namespace Modules
{
    [Serializable]
    public struct ColorScheme
    {
        public Color uiColor;
        [ColorUsage(true, true)] public Color adasLinesColor;
        public Material carMaterial;
    }

    //[CreateAssetMenu(menuName = "Car Control/Theme Data")]
    public class ThemeData : ScriptableObject
    {
        private const string PrefsKey = "ThemeData.SelectedSchemeIndex";

        [SerializeField] private int _selectedSchemeIndex;
        [SerializeField] public ColorScheme[] availableSchemes;

        public event Action ThemeChanged;

        public int SelectedSchemeIndex => _selectedSchemeIndex;

        public ColorScheme CurrentScheme =>
            availableSchemes != null && availableSchemes.Length > 0
                ? availableSchemes[Mathf.Clamp(_selectedSchemeIndex, 0, availableSchemes.Length - 1)]
                : default;

        private void OnEnable()
        {
#if !UNITY_EDITOR
            if (Application.isPlaying && PlayerPrefs.HasKey(PrefsKey) && availableSchemes != null && availableSchemes.Length > 0)
                _selectedSchemeIndex = Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey), 0, availableSchemes.Length - 1);
#endif
        }

        public void SetSelectedSchemeIndex(int index)
        {
            if (availableSchemes == null || availableSchemes.Length == 0)
                return;

            int clamped = Mathf.Clamp(index, 0, availableSchemes.Length - 1);
            if (clamped == _selectedSchemeIndex)
                return;

            _selectedSchemeIndex = clamped;

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#else
            PlayerPrefs.SetInt(PrefsKey, _selectedSchemeIndex);
#endif

            ThemeChanged?.Invoke();
        }
    }
}

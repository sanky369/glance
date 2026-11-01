using Modules;
using UnityEngine;

namespace Modules.Menus.Scripts
{
    public class ColorSelectorWidget : SelectorWidget
    {
        [Header("Choices")]
        [SerializeField] private ColorChoice _choicePrefab;
        [SerializeField] private ThemeData _themeData;

        protected override void PopulateChoices(RectTransform container)
        {
            for (int i = 0; i < _themeData.availableSchemes.Length; i++)
            {
                ColorChoice choice = Instantiate(_choicePrefab, container);
                choice.Initialize(_themeData.availableSchemes[i], i);
            }

            OnSelectionConfirmed -= HandleSelectionConfirmed;
            OnSelectionConfirmed += HandleSelectionConfirmed;
        }

        protected override void OnInitialized()
        {
            SelectChoice(_themeData.SelectedSchemeIndex, animate: false);
        }

        private void HandleSelectionConfirmed(SelectorChoice choice)
        {
            if (choice is ColorChoice colorChoice)
                _themeData.SetSelectedSchemeIndex(colorChoice.SchemeIndex);
        }
    }
}

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Modules.Cluster.Editor
{
    [CustomEditor(typeof(CircleSpawner))]
    public class CircularSpawnerEditor : UnityEditor.Editor
    {
        private VisualElement _distributionFields;

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement inspector = new();
            
            inspector.Add(new PropertyField(serializedObject.FindProperty("_themeData")));
            inspector.Add(new PropertyField(serializedObject.FindProperty(nameof(CircleSpawner.prefab))));
            inspector.Add(new PropertyField(serializedObject.FindProperty(nameof(CircleSpawner.radius))));
            inspector.Add(new PropertyField(serializedObject.FindProperty(nameof(CircleSpawner.startAngle))));
            inspector.Add(new PropertyField(serializedObject.FindProperty(nameof(CircleSpawner.sweepAngle))));
            inspector.Add(new PropertyField(serializedObject.FindProperty(nameof(CircleSpawner.includeEndpoint))));
            inspector.Add(new PropertyField(serializedObject.FindProperty(nameof(CircleSpawner.rotatePrefab))));
            inspector.Add(new PropertyField(serializedObject.FindProperty(nameof(CircleSpawner.applyTheme))));
            
            SerializedProperty distributionTypeProp = serializedObject.FindProperty(nameof(CircleSpawner.distributionType));
            PropertyField distributionTypePropField = new(distributionTypeProp);
            inspector.Add(distributionTypePropField);
            
            _distributionFields = new VisualElement(){ name = "DistributionFields" };
            inspector.Add(_distributionFields);
            
            distributionTypePropField.RegisterValueChangeCallback(DistributionTypeChanged);
            
            Button rebuildButton = new Button() { text = "Rebuild" };
            rebuildButton.clicked += OnRebuild;
            inspector.Add(rebuildButton);

            return inspector;

            void OnRebuild() => ((CircleSpawner)target).Rebuild();
        }

        private void DistributionTypeChanged(SerializedPropertyChangeEvent evt)
        {
            _distributionFields.Clear();
            PopulateDistributionFields(evt.changedProperty);
        }

        private void PopulateDistributionFields(SerializedProperty prop)
        {
            switch ((CircleSpawner.DistributionType)prop.enumValueIndex)
            {
                case CircleSpawner.DistributionType.Simple:
                    PropertyField countProp = new(serializedObject.FindProperty(nameof(CircleSpawner.count)));
                    countProp.Bind(serializedObject);
                    _distributionFields.Add(countProp);
                    break;
                
                case CircleSpawner.DistributionType.NumbersRange:
                    PropertyField countProp1 = new(serializedObject.FindProperty(nameof(CircleSpawner.count)));
                    PropertyField startNProp = new(serializedObject.FindProperty(nameof(CircleSpawner.startNumber)));
                    PropertyField endNProp = new(serializedObject.FindProperty(nameof(CircleSpawner.endNumber)));
                    countProp1.Bind(serializedObject);
                    startNProp.Bind(serializedObject);
                    endNProp.Bind(serializedObject);
                    _distributionFields.Add(countProp1);
                    _distributionFields.Add(startNProp);
                    _distributionFields.Add(endNProp);
                    break;
                
                case CircleSpawner.DistributionType.IndividualLetters:
                    PropertyField textProp = new(serializedObject.FindProperty(nameof(CircleSpawner.textToDistribute)));
                    textProp.Bind(serializedObject);
                    _distributionFields.Add(textProp);
                    break;
            }
        }
    }
}
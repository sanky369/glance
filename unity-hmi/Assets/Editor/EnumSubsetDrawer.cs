using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnumSubsetAttribute))]
public class EnumSubsetDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.Enum)
        {
            EditorGUI.PropertyField(position, property, label, true);
            return;
        }

        // enumValueIndex is an index into the member list, not the value. CarData.Gears starts
        // at -2, so only intValue is meaningful here.
        int current = property.intValue;

        List<int> values = new(((EnumSubsetAttribute)attribute).Values);
        List<GUIContent> names = new();
        foreach (int value in values) names.Add(new GUIContent(DisplayName(value)));

        // Keep an out-of-subset value selectable rather than snapping the field to something
        // else, so merely inspecting an asset cannot change it.
        if (!values.Contains(current))
        {
            values.Insert(0, current);
            names.Insert(0, new GUIContent($"{DisplayName(current)} (not supported)"));
        }

        label = EditorGUI.BeginProperty(position, label, property);
        EditorGUI.BeginChangeCheck();
        int picked = EditorGUI.IntPopup(position, label, current, names.ToArray(), values.ToArray());
        if (EditorGUI.EndChangeCheck()) property.intValue = picked;
        EditorGUI.EndProperty();
    }

    private string DisplayName(int value)
    {
        string name = fieldInfo != null ? Enum.GetName(fieldInfo.FieldType, value) : null;
        return name != null ? ObjectNames.NicifyVariableName(name) : value.ToString();
    }
}

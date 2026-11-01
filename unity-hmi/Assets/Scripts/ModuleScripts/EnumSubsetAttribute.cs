using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Limits an enum field's Inspector dropdown to the listed values. The enum itself keeps every
/// member, so code and serialized data are untouched - this only hides options the project does
/// not support yet. A value outside the subset stays visible if a field already holds one, so
/// opening the Inspector never silently rewrites data.
/// </summary>
public class EnumSubsetAttribute : PropertyAttribute
{
    public readonly int[] Values;

    // Convert rather than cast: (int)someBoxedEnum throws InvalidCastException.
    public EnumSubsetAttribute(params object[] values)
        => Values = values.Select(Convert.ToInt32).ToArray();
}

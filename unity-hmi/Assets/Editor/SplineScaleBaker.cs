using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public static class SplineScaleBaker
{
    // This adds a new right-click option to the SplineContainer component
    [MenuItem("CONTEXT/SplineContainer/Bake Transform Scale into Spline")]
    public static void BakeScale(MenuCommand command)
    {
        SplineContainer container = (SplineContainer)command.context;
        
        // Allows you to CTRL+Z / CMD+Z if you make a mistake
        Undo.RecordObject(container, "Bake Spline Scale");
        Undo.RecordObject(container.transform, "Bake Transform Scale");

        Vector3 scale = container.transform.localScale;
        float3 scale3 = new float3(scale.x, scale.y, scale.z);

        // Loop through all splines in this container
        foreach (Spline spline in container.Splines)
        {
            // Loop through every point (knot) on the spline
            for (int i = 0; i < spline.Count; i++)
            {
                BezierKnot knot = spline[i];
                
                // Multiply the position and the curve handles by the old scale
                knot.Position *= scale3;
                knot.TangentIn *= scale3;
                knot.TangentOut *= scale3;
                
                // Save the new knot back into the spline
                spline[i] = knot;
            }
        }

        // Reset the Transform scale back to 1, 1, 1 safely
        container.transform.localScale = Vector3.one;
        EditorUtility.SetDirty(container);
        
        Debug.Log($"Successfully baked scale of {scale} into the spline knots for '{container.gameObject.name}'!");
    }
}
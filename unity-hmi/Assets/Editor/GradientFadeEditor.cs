using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Modules;

[CustomEditor(typeof(GradientFade))]
public class GradientFadeEditor : Editor
{
    // ── Serialized props ──────────────────────────────────────────────────
    SerializedProperty _shapeMode;
    SerializedProperty _paddingPixels;
    SerializedProperty _cornerRadius;
    SerializedProperty _circleRadius;
    SerializedProperty _polygonPoints;
    SerializedProperty _polygonEdgeSoftness;
    SerializedProperty _gradH, _borderH;
    SerializedProperty _gradV, _borderV;
    SerializedProperty _texRes;

    // ── Foldout state ─────────────────────────────────────────────────────
    bool _showShape    = true;
    bool _showGradient = true;
    bool _showBake     = false;

    // ── Styles (lazy) ─────────────────────────────────────────────────────
    GUIStyle _headerStyle;
    GUIStyle _boxStyle;

    static readonly Color _accentColor  = new Color(0.35f, 0.75f, 0.95f);
    static readonly Color _sectionColor = new Color(0.18f, 0.18f, 0.18f);

    void OnEnable()
    {
        _shapeMode          = serializedObject.FindProperty("_shapeMode");
        _paddingPixels      = serializedObject.FindProperty("_paddingPixels");
        _cornerRadius       = serializedObject.FindProperty("_cornerRadiusPixels");
        _circleRadius       = serializedObject.FindProperty("_circleRadiusFraction");
        _polygonPoints      = serializedObject.FindProperty("_polygonPoints");
        _polygonEdgeSoftness= serializedObject.FindProperty("_polygonEdgeSoftness");
        _gradH              = serializedObject.FindProperty("_gradientHorizontal");
        _borderH            = serializedObject.FindProperty("_borderWidthHPixels");
        _gradV              = serializedObject.FindProperty("_gradientVertical");
        _borderV            = serializedObject.FindProperty("_borderWidthVPixels");
        _texRes             = serializedObject.FindProperty("_textureResolution");
    }

    void InitStyles()
    {
        if (_headerStyle != null) return;

        _headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 11,
            alignment = TextAnchor.MiddleLeft,
        };
        _headerStyle.normal.textColor = _accentColor;

        _boxStyle = new GUIStyle("box")
        {
            padding = new RectOffset(10, 10, 6, 6),
            margin  = new RectOffset(0, 0, 2, 2),
        };
    }

    public override void OnInspectorGUI()
    {
        InitStyles();
        serializedObject.Update();

        var fade = (GradientFade)target;

        // ── Shape section ────────────────────────────────────────────────
        _showShape = DrawSection("SHAPE", _showShape, () =>
        {
            EditorGUILayout.PropertyField(_shapeMode, new GUIContent("Mode"));
            EditorGUILayout.Space(4);

            var mode = (GradientFadeShape)_shapeMode.enumValueIndex;

            switch (mode)
            {
                case GradientFadeShape.RoundedRect:
                    DrawRoundedRectFields();
                    break;
                case GradientFadeShape.Circle:
                    DrawCircleFields();
                    break;
                case GradientFadeShape.Polygon:
                    DrawPolygonFields(fade);
                    break;
            }
        });

        EditorGUILayout.Space(4);

        // ── Gradient section ─────────────────────────────────────────────
        _showGradient = DrawSection("GRADIENT", _showGradient, () =>
        {
            var mode = (GradientFadeShape)_shapeMode.enumValueIndex;

            if (mode == GradientFadeShape.Circle || mode == GradientFadeShape.Polygon)
            {
                EditorGUILayout.PropertyField(_gradH, new GUIContent("Gradient"));
                EditorGUILayout.PropertyField(_borderH, new GUIContent("Border Width (px)"));
            }
            else
            {
                EditorGUILayout.LabelField("Left / Right", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_gradH,   new GUIContent("Gradient"));
                EditorGUILayout.PropertyField(_borderH, new GUIContent("Border Width (px)"));
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(4);

                EditorGUILayout.LabelField("Top / Bottom", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_gradV,   new GUIContent("Gradient"));
                EditorGUILayout.PropertyField(_borderV, new GUIContent("Border Width (px)"));
                EditorGUI.indentLevel--;
            }
        });

        EditorGUILayout.Space(4);

        // ── Bake section ─────────────────────────────────────────────────
        _showBake = DrawSection("BAKE", _showBake, () =>
        {
            EditorGUILayout.PropertyField(_texRes, new GUIContent("Texture Resolution"));
            EditorGUILayout.Space(4);
            if (GUILayout.Button("Rebuild Material"))
                fade.RebuildMaterial();
            EditorGUILayout.Space(4);
            EditorGUI.BeginDisabledGroup(Application.isPlaying);
            if (GUILayout.Button("Bake to PNG (swap to static sprite)"))
                BakeToSprite(fade);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.HelpBox("Renders the current shape to a PNG, saves it next to this prefab, and replaces the GradientFade component with a plain Image. Irreversible — use Undo if needed.", MessageType.Info);
        });

        if (serializedObject.ApplyModifiedProperties())
            fade.SendMessage("OnValidate", SendMessageOptions.DontRequireReceiver);
    }

    // ── Shape sub-drawers ─────────────────────────────────────────────────

    void DrawRoundedRectFields()
    {
        var p = _paddingPixels.vector4Value;

        EditorGUILayout.LabelField("Padding (px)", EditorStyles.miniBoldLabel);
        EditorGUI.indentLevel++;

        float newL = EditorGUILayout.FloatField("Left",   p.x);
        float newR = EditorGUILayout.FloatField("Right",  p.y);
        float newT = EditorGUILayout.FloatField("Top",    p.z);
        float newB = EditorGUILayout.FloatField("Bottom", p.w);

        if (!Mathf.Approximately(newL, p.x) || !Mathf.Approximately(newR, p.y) ||
            !Mathf.Approximately(newT, p.z) || !Mathf.Approximately(newB, p.w))
            _paddingPixels.vector4Value = new Vector4(newL, newR, newT, newB);

        EditorGUI.indentLevel--;

        EditorGUILayout.Space(4);
        EditorGUILayout.PropertyField(_cornerRadius, new GUIContent("Corner Radius (px)"));
    }

    void DrawCircleFields()
    {
        EditorGUILayout.PropertyField(_circleRadius, new GUIContent("Radius (fraction of shorter side)"));
    }

    void DrawPolygonFields(GradientFade fade)
    {
        EditorGUILayout.PropertyField(_polygonEdgeSoftness, new GUIContent("Edge Softness (height units)"));
        EditorGUILayout.Space(6);

        // Quick-add preset shapes
        EditorGUILayout.LabelField("Preset Shapes", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Triangle", EditorStyles.miniButton))  SetPolygon(fade, 3, 0.45f);
        if (GUILayout.Button("Diamond",  EditorStyles.miniButton))  SetPolygon(fade, 4, 0.45f, -45f);
        if (GUILayout.Button("Pentagon", EditorStyles.miniButton))  SetPolygon(fade, 5, 0.45f);
        if (GUILayout.Button("Hexagon",  EditorStyles.miniButton))  SetPolygon(fade, 6, 0.45f);
        if (GUILayout.Button("Octagon",  EditorStyles.miniButton))  SetPolygon(fade, 8, 0.45f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Points (angle °, radius 0–1)", EditorStyles.miniBoldLabel);

        // Draw each point with a delete button
        int deleteIdx = -1;
        for (int i = 0; i < _polygonPoints.arraySize; i++)
        {
            var elem  = _polygonPoints.GetArrayElementAtIndex(i);
            var val   = elem.vector2Value;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Point {i}", GUILayout.Width(54));
            float angle  = EditorGUILayout.FloatField(val.x, GUILayout.Width(60));
            EditorGUILayout.LabelField("°", GUILayout.Width(14));
            float radius = EditorGUILayout.Slider(val.y, 0f, 1f);
            if (!Mathf.Approximately(angle, val.x) || !Mathf.Approximately(radius, val.y))
                elem.vector2Value = new Vector2(angle, radius);

            if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22)))
                deleteIdx = i;
            EditorGUILayout.EndHorizontal();
        }

        if (deleteIdx >= 0 && _polygonPoints.arraySize > 3)
            _polygonPoints.DeleteArrayElementAtIndex(deleteIdx);

        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+ Add Point", EditorStyles.miniButton, GUILayout.Width(90)))
        {
            if (_polygonPoints.arraySize < 8)
            {
                _polygonPoints.InsertArrayElementAtIndex(_polygonPoints.arraySize);
                var last = _polygonPoints.GetArrayElementAtIndex(_polygonPoints.arraySize - 1);
                last.vector2Value = new Vector2(0f, 0.45f);
            }
            else
            {
                Debug.LogWarning("GradientFade: max 8 polygon points.");
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        DrawPolygonPreview(fade);
    }

    // ── Polygon preview ───────────────────────────────────────────────────

    void DrawPolygonPreview(GradientFade fade)
    {
        float size   = EditorGUIUtility.currentViewWidth - 32f;
        float height = Mathf.Min(size * 0.5f, 160f);
        Rect rect    = GUILayoutUtility.GetRect(size, height);

        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));

        if (Event.current.type != EventType.Repaint) return;

        // Sort by angle to match the CCW winding the shader expects.
        var rawPts = new List<Vector2>();
        for (int i = 0; i < _polygonPoints.arraySize; i++)
            rawPts.Add(_polygonPoints.GetArrayElementAtIndex(i).vector2Value);
        rawPts.Sort((a, b) => a.x.CompareTo(b.x));

        var pts = new List<Vector2>();
        foreach (var v in rawPts)
        {
            float a = v.x * Mathf.Deg2Rad;
            float r = v.y;
            pts.Add(new Vector2(
                rect.center.x + Mathf.Cos(a) * r * (rect.width  * 0.45f),
                rect.center.y - Mathf.Sin(a) * r * (rect.height * 0.45f)
            ));
        }

        if (pts.Count < 2) return;

        Handles.color = _accentColor;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 a3 = new Vector3(pts[i].x,                 pts[i].y,                 0f);
            Vector3 b3 = new Vector3(pts[(i + 1) % pts.Count].x, pts[(i + 1) % pts.Count].y, 0f);
            Handles.DrawLine(a3, b3);
        }

        Handles.color = Color.white;
        foreach (var p in pts)
            Handles.DrawSolidDisc(new Vector3(p.x, p.y, 0f), Vector3.forward, 4f);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    void SetPolygon(GradientFade fade, int sides, float radius, float startAngle = 90f)
    {
        Undo.RecordObject(fade, "Set Polygon Shape");
        fade.PolygonPoints.Clear();
        for (int i = 0; i < sides; i++)
            fade.PolygonPoints.Add(new Vector2(startAngle + i * (360f / sides), radius));
        EditorUtility.SetDirty(fade);
        serializedObject.Update();
    }

    // ── Bake to PNG ───────────────────────────────────────────────────────

    void BakeToSprite(GradientFade fade)
    {
        var rectTransform = fade.GetComponent<RectTransform>();
        if (rectTransform == null) { Debug.LogError("GradientFade: no RectTransform found."); return; }

        Vector2 rectSize = rectTransform.rect.size;
        int w = rectSize.x > 0 ? (int)rectSize.x : fade.TextureResolution;
        int h = rectSize.y > 0 ? (int)rectSize.y : fade.TextureResolution;

        float scale = Mathf.Min(1f, 2048f / Mathf.Max(w, h));
        w = Mathf.Max(1, Mathf.RoundToInt(w * scale));
        h = Mathf.Max(1, Mathf.RoundToInt(h * scale));

        float aspect = (float)w / h;

        // Build gradient lookup arrays (same resolution the component uses)
        int res = Mathf.Max(fade.TextureResolution, 64);
        var lutH = new Color[res];
        var lutV = new Color[res];
        for (int i = 0; i < res; i++)
        {
            float t = (float)i / (res - 1);
            lutH[i] = fade.GradientHorizontal.Evaluate(t);
            lutV[i] = fade.GradientVertical.Evaluate(t);
        }

        // Pre-compute polygon points in aspect-corrected height-unit space
        Vector2[] polyPts = null;
        if (fade.ShapeMode == GradientFadeShape.Polygon)
        {
            var sorted = new List<Vector2>(fade.PolygonPoints);
            sorted.Sort((a, b) => a.x.CompareTo(b.x));
            int cnt = Mathf.Min(sorted.Count, 8);
            polyPts = new Vector2[cnt];
            for (int i = 0; i < cnt; i++)
            {
                float ang = sorted[i].x * Mathf.Deg2Rad;
                float r   = sorted[i].y;
                polyPts[i] = new Vector2(
                    0.5f * aspect + Mathf.Cos(ang) * r * aspect,
                    0.5f          - Mathf.Sin(ang) * r);
            }
        }

        float bwH = Mathf.Max(fade.BorderWidthHPixels / h, 0.001f);
        float bwV = Mathf.Max(fade.BorderWidthVPixels / h, 0.001f);

        var pixels = new Color[w * h];

        for (int py = 0; py < h; py++)
        {
            float uvY = (float)py / (h - 1);
            for (int px = 0; px < w; px++)
            {
                float uvX = (float)px / (w - 1);
                float apX = uvX * aspect;
                float apY = uvY;

                float d  = 0f;
                float tH = 0f, tV = 0f;
                float wH = 0.5f, wV = 0.5f;

                if (fade.ShapeMode == GradientFadeShape.RoundedRect)
                {
                    float padL = Mathf.Clamp01(fade.PaddingPixels.x / (w / scale));
                    float padR = Mathf.Clamp01(fade.PaddingPixels.y / (w / scale));
                    float padT = Mathf.Clamp01(fade.PaddingPixels.z / (h / scale));
                    float padB = Mathf.Clamp01(fade.PaddingPixels.w / (h / scale));
                    float rad  = Mathf.Clamp(fade.CornerRadiusPixels / (h / scale), 0f, 0.5f);

                    float minX = padL * aspect, maxX = (1f - padR) * aspect;
                    float minY = padB,          maxY = 1f - padT;
                    float cx = (minX + maxX) * 0.5f, cy = (minY + maxY) * 0.5f;
                    float hx = (maxX - minX) * 0.5f, hy = (maxY - minY) * 0.5f;

                    float qx = Mathf.Abs(apX - cx) - (hx - rad);
                    float qy = Mathf.Abs(apY - cy) - (hy - rad);
                    d = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f))
                        + Mathf.Min(Mathf.Max(qx, qy), 0f) - rad;

                    float dx  = Mathf.Max(minX - apX, apX - maxX);
                    float dy  = Mathf.Max(minY - apY, apY - maxY);
                    float dxp = Mathf.Max(dx, 0f), dyp = Mathf.Max(dy, 0f);
                    float tot = dxp + dyp;
                    wH = tot > 0.0001f ? dxp / tot : 0.5f;
                    wV = 1f - wH;

                    tH = Mathf.Clamp01(d / bwH);
                    tV = Mathf.Clamp01(d / bwV);
                }
                else if (fade.ShapeMode == GradientFadeShape.Circle)
                {
                    float shorter  = Mathf.Min(w, h);
                    float radiusHU = fade.CircleRadiusFraction * shorter / (h / scale);
                    float cx = 0.5f * aspect, cy = 0.5f;
                    d  = Mathf.Sqrt((apX - cx) * (apX - cx) + (apY - cy) * (apY - cy)) - radiusHU;
                    tH = Mathf.Clamp01(d / bwH);
                    tV = tH;
                }
                else if (polyPts != null) // Polygon
                {
                    float sdf = -1e9f;
                    for (int i = 0; i < polyPts.Length; i++)
                    {
                        int    j   = (i + 1) % polyPts.Length;
                        float  ex  = polyPts[j].x - polyPts[i].x;
                        float  ey  = polyPts[j].y - polyPts[i].y;
                        float  len = Mathf.Sqrt(ex * ex + ey * ey);
                        if (len < 0.0001f) continue;
                        float nx = ey / len, ny = -ex / len;
                        sdf = Mathf.Max(sdf, (apX - polyPts[i].x) * nx + (apY - polyPts[i].y) * ny);
                    }
                    d  = sdf - fade.PolygonEdgeSoftness;
                    tH = Mathf.Clamp01(d / bwH);
                    tV = tH;
                }

                Color gH = SampleLut(lutH, tH);
                Color gV = SampleLut(lutV, tV);
                Color g  = gH * wH + gV * wV;

                pixels[py * w + px] = new Color(0f, 0f, 0f, g.a);
            }
        }

        var baked = new Texture2D(w, h, TextureFormat.RGBA32, false);
        baked.SetPixels(pixels);
        baked.Apply();

        string prefabPath = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(fade.gameObject));
        string dir        = string.IsNullOrEmpty(prefabPath)
                            ? "Assets/Textures"
                            : System.IO.Path.GetDirectoryName(prefabPath).Replace("\\", "/");
        System.IO.Directory.CreateDirectory(dir);

        string pngPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/GradientFade_Baked.png");
        System.IO.File.WriteAllBytes(pngPath, baked.EncodeToPNG());
        Object.DestroyImmediate(baked);
        AssetDatabase.ImportAsset(pngPath);

        var importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
        importer.textureType         = TextureImporterType.Sprite;
        importer.spriteImportMode    = SpriteImportMode.Single;
        importer.mipmapEnabled       = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize      = Mathf.NextPowerOfTwo(Mathf.Max(w, h));
        importer.SaveAndReimport();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
        if (sprite == null) { Debug.LogError($"GradientFade: failed to load sprite at {pngPath}"); return; }

        var img = fade.GetComponent<UnityEngine.UI.Image>();
        var defaultMat = img.defaultMaterial;
        Undo.RecordObject(img, "Bake GradientFade to Sprite");
        img.sprite   = sprite;
        img.color    = Color.white;
        img.material = defaultMat;
        Undo.DestroyObjectImmediate(fade);

        EditorUtility.SetDirty(img.gameObject);
        Debug.Log($"GradientFade baked → {pngPath}  ({w}×{h})");
    }

    static Color SampleLut(Color[] lut, float t)
    {
        float f = Mathf.Clamp01(t) * (lut.Length - 1);
        int   i = Mathf.Min((int)f, lut.Length - 2);
        return Color.LerpUnclamped(lut[i], lut[i + 1], f - i);
    }

    bool DrawSection(string label, bool open, System.Action body)
    {
        var bg = GUI.backgroundColor;
        GUI.backgroundColor = _sectionColor;

        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = bg;

        EditorGUILayout.BeginHorizontal();
        var arrow = open ? "▾" : "▸";
        if (GUILayout.Button($"{arrow}  {label}", _headerStyle, GUILayout.ExpandWidth(true)))
            open = !open;
        EditorGUILayout.EndHorizontal();

        if (open)
        {
            EditorGUILayout.Space(2);
            body();
            EditorGUILayout.Space(2);
        }

        EditorGUILayout.EndVertical();
        return open;
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Modules
{
    public enum GradientFadeShape { RoundedRect, Circle, Polygon }

    [RequireComponent(typeof(Image))]
    [ExecuteAlways]
    public class GradientFade : MonoBehaviour
    {
        // ── Shape ─────────────────────────────────────────────────────────────
        [FormerlySerializedAs("shapeMode")]
        [SerializeField] private GradientFadeShape _shapeMode = GradientFadeShape.RoundedRect;

        // RoundedRect / shared
        [FormerlySerializedAs("paddingPixels")]
        [SerializeField] private Vector4 _paddingPixels      = new Vector4(140f, 140f, 90f, 90f);
        [FormerlySerializedAs("cornerRadiusPixels")]
        [SerializeField] private float   _cornerRadiusPixels = 60f;

        // Circle
        [FormerlySerializedAs("circleRadiusFraction")]
        [SerializeField] private float   _circleRadiusFraction = 0.45f;

        // Polygon — up to 8 points, each (angleDegrees, radiusFraction 0-1)
        [FormerlySerializedAs("polygonPoints")]
        [SerializeField] private List<Vector2> _polygonPoints = new List<Vector2>
        {
            new Vector2(  0f, 0.45f),
            new Vector2( 60f, 0.45f),
            new Vector2(120f, 0.45f),
            new Vector2(180f, 0.45f),
            new Vector2(240f, 0.45f),
            new Vector2(300f, 0.45f),
        };
        [FormerlySerializedAs("polygonEdgeSoftness")]
        [SerializeField] private float _polygonEdgeSoftness = 0.02f;

        // ── Gradients ─────────────────────────────────────────────────────────
        [FormerlySerializedAs("gradientHorizontal")]
        [SerializeField] private Gradient _gradientHorizontal = DefaultGradient();
        [FormerlySerializedAs("borderWidthHPixels")]
        [SerializeField] private float    _borderWidthHPixels = 250f;

        [FormerlySerializedAs("gradientVertical")]
        [SerializeField] private Gradient _gradientVertical   = DefaultGradient();
        [FormerlySerializedAs("borderWidthVPixels")]
        [SerializeField] private float    _borderWidthVPixels = 250f;

        // ── Baking ────────────────────────────────────────────────────────────
        [FormerlySerializedAs("textureResolution")]
        [SerializeField] private int _textureResolution = 256;

        // ── Privates ──────────────────────────────────────────────────────────
        private Image         _image;
        private RectTransform _rt;
        private Material      _material;
        private Texture2D     _textureH;
        private Texture2D     _textureV;
        private Vector2       _lastRectSize;

        private static readonly int s_GradientTexH  = Shader.PropertyToID("_GradientTexH");
        private static readonly int s_GradientTexV  = Shader.PropertyToID("_GradientTexV");
        private static readonly int s_PaddingId     = Shader.PropertyToID("_Padding");
        private static readonly int s_InnerRadius   = Shader.PropertyToID("_InnerRadius");
        private static readonly int s_BorderWidthH  = Shader.PropertyToID("_BorderWidthH");
        private static readonly int s_BorderWidthV  = Shader.PropertyToID("_BorderWidthV");
        private static readonly int s_AspectId      = Shader.PropertyToID("_Aspect");
        private static readonly int s_ShapeModeId   = Shader.PropertyToID("_ShapeMode");
        private static readonly int s_CircleRadId   = Shader.PropertyToID("_CircleRadius");
        private static readonly int s_PolyCountId   = Shader.PropertyToID("_PolyCount");
        private static readonly int s_PolyPointsId  = Shader.PropertyToID("_PolyPoints");
        private static readonly int s_PolyEdgeSoft  = Shader.PropertyToID("_PolyEdgeSoftness");

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void OnValidate()
        {
            if (_image == null) _image = GetComponent<Image>();
            if (_rt    == null) _rt    = GetComponent<RectTransform>();
            EnsureMaterial();
        }

        private void OnEnable()
        {
            _image = GetComponent<Image>();
            _rt    = GetComponent<RectTransform>();
            EnsureMaterial();
        }

        private void Update()
        {
            if (_material == null || _rt == null) return;
            Vector2 size = _rt.rect.size;
            if (size != _lastRectSize)
            {
                _lastRectSize = size;
                ApplyShapeParams();
            }
        }

        private void OnDisable()
        {
            SafeDestroy(_textureH);
            SafeDestroy(_textureV);
            _textureH = null;
            _textureV = null;
            // Keep _material alive so Image retains it when the script is disabled.
            // Material is destroyed in OnDestroy instead.
        }

        private void OnDestroy()
        {
            SafeDestroy(_material);
            _material = null;
        }

        // ── Internal ──────────────────────────────────────────────────────────
        private void EnsureMaterial()
        {
            if (_material != null) return;

            Shader shader = Shader.Find("UI/Gradient Fade");
            if (shader == null)
            {
#if UNITY_EDITOR
                if (UnityEditor.AssetDatabase.IsAssetImportWorkerProcess()) return;

                if (UnityEditor.EditorApplication.isUpdating || UnityEditor.EditorApplication.isCompiling)
                {
                    UnityEditor.EditorApplication.delayCall += () => { if (this != null && _image != null) EnsureMaterial(); };
                    return;
                }
#endif
                Debug.LogWarning("GradientFade: shader 'UI/Gradient Fade' not found.");
                return;
            }

            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _image.material = _material;
            BakeAndApply();
        }

        private void BakeAndApply()
        {
            if (_material == null) return;

            SafeDestroy(_textureH);
            SafeDestroy(_textureV);
            _textureH = BakeGradient(_gradientHorizontal);
            _textureV = BakeGradient(_gradientVertical);

            _material.SetTexture(s_GradientTexH, _textureH);
            _material.SetTexture(s_GradientTexV, _textureV);
            ApplyShapeParams();
        }

        private Texture2D BakeGradient(Gradient gradient)
        {
            var tex = new Texture2D(_textureResolution, 1, TextureFormat.RGBA32, false)
            {
                wrapMode  = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[_textureResolution];
            for (int i = 0; i < _textureResolution; i++)
                pixels[i] = gradient.Evaluate((float)i / (_textureResolution - 1));
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void ApplyShapeParams()
        {
            if (_material == null) return;
            if (_rt == null) _rt = GetComponent<RectTransform>();

            Vector2 size = _rt != null ? _rt.rect.size : Vector2.one;
            float w = Mathf.Max(size.x, 1f);
            float h = Mathf.Max(size.y, 1f);
            float aspect = w / h;

            _material.SetFloat(s_AspectId,    aspect);
            _material.SetInt  (s_ShapeModeId, (int)_shapeMode);

            if (_shapeMode == GradientFadeShape.RoundedRect)
            {
                float padL = Mathf.Clamp01(_paddingPixels.x / w);
                float padR = Mathf.Clamp01(_paddingPixels.y / w);
                float padT = Mathf.Clamp01(_paddingPixels.z / h);
                float padB = Mathf.Clamp01(_paddingPixels.w / h);

                float radius = Mathf.Clamp(_cornerRadiusPixels / h, 0f, 0.5f);
                float bwH    = Mathf.Max(_borderWidthHPixels / h, 0.001f);
                float bwV    = Mathf.Max(_borderWidthVPixels / h, 0.001f);

                _material.SetVector(s_PaddingId,    new Vector4(padL, padR, padT, padB));
                _material.SetFloat (s_InnerRadius,  radius);
                _material.SetFloat (s_BorderWidthH, bwH);
                _material.SetFloat (s_BorderWidthV, bwV);
            }
            else if (_shapeMode == GradientFadeShape.Circle)
            {
                float shorter = Mathf.Min(w, h);
                float radiusH = _circleRadiusFraction * shorter / h;
                float bwH     = Mathf.Max(_borderWidthHPixels / h, 0.001f);

                _material.SetFloat(s_CircleRadId,   radiusH);
                _material.SetFloat(s_BorderWidthH,  bwH);
            }
            else // Polygon
            {
                // Sort a copy by angle so the shader always receives CCW-wound points.
                var sorted = new List<Vector2>(_polygonPoints);
                sorted.Sort((a, b) => a.x.CompareTo(b.x));
                int count = Mathf.Min(sorted.Count, 8);
                var pts   = new Vector4[8];

                for (int i = 0; i < count; i++)
                {
                    float angleRad = sorted[i].x * Mathf.Deg2Rad;
                    float radius   = sorted[i].y;
                    // Aspect-corrected height-unit space: center=(aspect*0.5, 0.5)
                    // Negate sin so angle 90° points upward (toward lower UV = top of screen).
                    pts[i] = new Vector4(
                        0.5f * aspect + Mathf.Cos(angleRad) * radius * aspect,
                        0.5f          - Mathf.Sin(angleRad) * radius,
                        0f, 0f
                    );
                }

                float bwH = Mathf.Max(_borderWidthHPixels / h, 0.001f);

                _material.SetInt        (s_PolyCountId,  count);
                _material.SetVectorArray(s_PolyPointsId, pts);
                _material.SetFloat      (s_PolyEdgeSoft, Mathf.Max(_polygonEdgeSoftness, 0.001f));
                _material.SetFloat      (s_BorderWidthH, bwH);
            }
        }

        private static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        public GradientFadeShape ShapeMode          => _shapeMode;
        public Vector4           PaddingPixels       => _paddingPixels;
        public float             CornerRadiusPixels  => _cornerRadiusPixels;
        public float             CircleRadiusFraction => _circleRadiusFraction;
        public List<Vector2>     PolygonPoints       => _polygonPoints;
        public float             PolygonEdgeSoftness => _polygonEdgeSoftness;
        public Gradient          GradientHorizontal  => _gradientHorizontal;
        public Gradient          GradientVertical    => _gradientVertical;
        public float             BorderWidthHPixels  => _borderWidthHPixels;
        public float             BorderWidthVPixels  => _borderWidthVPixels;
        public int               TextureResolution   => _textureResolution;

        public void RebuildMaterial()
        {
            SafeDestroy(_material);
            _material = null;
            EnsureMaterial();
        }

        private static Gradient DefaultGradient()
        {
            return new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(Color.black, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 1f) }
            };
        }
    }
}

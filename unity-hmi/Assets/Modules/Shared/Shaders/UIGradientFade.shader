Shader "UI/Gradient Fade"
{
    Properties
    {
        _GradientTexH ("Horizontal Gradient (L/R)", 2D) = "white" {}
        _GradientTexV ("Vertical Gradient (T/B)",   2D) = "white" {}

        _Padding      ("Padding (L,R,T,B 0-1)", Vector) = (0.1, 0.1, 0.1, 0.1)
        _InnerRadius  ("Corner Radius (height units)", Range(0, 0.5)) = 0.05
        _BorderWidthH ("Border Width H (height units)", Range(0.001, 2.0)) = 0.4
        _BorderWidthV ("Border Width V (height units)", Range(0.001, 2.0)) = 0.4
        _Aspect       ("Aspect (w/h)", Float) = 1.0

        // 0 = RoundedRect, 1 = Circle, 2 = Polygon
        _ShapeMode    ("Shape Mode", Int) = 0

        // Circle
        _CircleRadius ("Circle Radius (height units)", Range(0, 1)) = 0.45

        // Polygon (up to 8 points in aspect-corrected height-unit space)
        _PolyCount    ("Polygon Point Count", Int) = 6
        _PolyEdgeSoftness ("Polygon Edge Softness", Range(0.001, 0.1)) = 0.02

        [HideInInspector] _MainTex          ("Stencil Tex", 2D) = "white" {}
        [HideInInspector] _StencilComp      ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil          ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp        ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask  ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask        ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color    : COLOR;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float2 uv       : TEXCOORD0;
                float4 color    : COLOR;
                float4 worldPos : TEXCOORD1;
            };

            sampler2D _GradientTexH;
            sampler2D _GradientTexV;
            float4    _Padding;
            float     _InnerRadius;
            float     _BorderWidthH;
            float     _BorderWidthV;
            float     _Aspect;
            int       _ShapeMode;
            float     _CircleRadius;
            int       _PolyCount;
            float4    _PolyPoints[8];
            float     _PolyEdgeSoftness;
            float4    _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex   = UnityObjectToClipPos(v.vertex);
                o.uv       = v.texcoord;
                o.color    = v.color;
                o.worldPos = v.vertex;
                return o;
            }

            // ── SDF helpers ───────────────────────────────────────────────

            float sdfRoundedRect(float2 p, float2 center, float2 halfSize, float radius)
            {
                float2 q = abs(p - center) - (halfSize - radius);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            float sdfCircle(float2 p, float2 center, float radius)
            {
                return length(p - center) - radius;
            }

            // Signed distance to a convex polygon (points sorted CCW by angle from C#).
            // Positive = outside, negative = inside.
            float sdfPolygon(float2 p, int count)
            {
                float d = -1e9;
                for (int i = 0; i < count; i++)
                {
                    int    j   = (i + 1) % count;
                    float2 a   = _PolyPoints[i].xy;
                    float2 b   = _PolyPoints[j].xy;
                    float2 e   = b - a;
                    float  len = length(e);
                    if (len < 0.0001) continue;
                    // Left-hand normal of CCW edge = outward normal
                    float2 n = float2(e.y, -e.x) / len;
                    d = max(d, dot(p - a, n));
                }
                return d;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 ap = float2(i.uv.x * _Aspect, i.uv.y);

                float d   = 0.0;
                float tH  = 0.0;
                float tV  = 0.0;
                float wH  = 0.5;
                float wV  = 0.5;

                if (_ShapeMode == 0) // RoundedRect
                {
                    float2 minUV  = float2(_Padding.x, _Padding.w);
                    float2 maxUV  = float2(1.0 - _Padding.y, 1.0 - _Padding.z);
                    float2 center = float2((minUV.x + maxUV.x) * 0.5 * _Aspect,
                                          (minUV.y + maxUV.y) * 0.5);
                    float2 halfSize   = float2((maxUV.x - minUV.x) * 0.5 * _Aspect,
                                          (maxUV.y - minUV.y) * 0.5);
                    d = sdfRoundedRect(ap, center, halfSize, _InnerRadius);

                    // Per-axis blend weight for H vs V gradient
                    float dx = max(minUV.x * _Aspect - ap.x, ap.x - maxUV.x * _Aspect);
                    float dy = max(minUV.y           - ap.y, ap.y - maxUV.y          );
                    float dxp = max(dx, 0.0);
                    float dyp = max(dy, 0.0);
                    float tot = dxp + dyp;
                    wH = (tot > 0.0001) ? dxp / tot : 0.5;
                    wV = 1.0 - wH;

                    tH = saturate(d / _BorderWidthH);
                    tV = saturate(d / _BorderWidthV);
                }
                else if (_ShapeMode == 1) // Circle
                {
                    float2 center = float2(0.5 * _Aspect, 0.5);
                    d  = sdfCircle(ap, center, _CircleRadius);
                    tH = saturate(d / _BorderWidthH);
                    tV = tH;
                }
                else // Polygon
                {
                    d  = sdfPolygon(ap, _PolyCount);
                    // Smooth the hard polygon edges slightly
                    d  = d - _PolyEdgeSoftness;
                    tH = saturate(d / _BorderWidthH);
                    tV = tH;
                }

                fixed4 gradH = tex2D(_GradientTexH, float2(tH, 0.5));
                fixed4 gradV = tex2D(_GradientTexV, float2(tV, 0.5));
                fixed4 grad  = gradH * wH + gradV * wV;

                fixed4 col = i.color;
                col.rgb *= grad.rgb;
                col.a   *= grad.a;

                col.a *= UnityGet2DClipping(i.worldPos.xy, _ClipRect);
                clip(col.a - 0.001);

                return col;
            }
            ENDCG
        }
    }
}

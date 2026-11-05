// Hand-drawn ink / pencil marks for the storybook settings page, procedural (Image without sprite):
//   0 Line    — a wobbly stroke across the rect (slider track, fill, section underline, level meter)
//   1 Box     — a sketched frame of four overshooting strokes (dropdown field, toggle box), optional paper fill
//   2 Seal    — a wax seal blob with a pressed ring (slider handle)
//   3 Check   — a quick check mark (toggle, dropdown item)
//   4 Chevron — a little downward "v" (dropdown arrow)
// _Size = rect size in px (set by UIShaderSize).
Shader "DuskTwilight/UI/TitleInk"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Size ("Size (px)", Vector) = (380, 12, 0, 0)
        [Enum(Line,0,Box,1,Seal,2,Check,3,Chevron,4)] _Shape ("Shape", Float) = 0
        _Seed ("Seed", Float) = 1

        _InkColor ("Ink", Color) = (0.20, 0.13, 0.08, 0.9)
        _Weight ("Stroke weight (px)", Float) = 2.5
        _Wobble ("Wobble (px)", Float) = 1.2
        _Grain ("Pencil grain (0 = ink, 1 = graphite)", Range(0, 1)) = 0
        _Taper ("End taper", Range(0, 1)) = 0.6
        _Inset ("Inset from the rect edge (px)", Float) = 3

        [Header(Box)]
        _FillColor ("Fill", Color) = (0.86, 0.78, 0.62, 0)
        _Overshoot ("Corner overshoot (px)", Float) = 5

        [Header(Seal)]
        _SealDark ("Seal shade", Color) = (0.30, 0.06, 0.05, 1)
        _SealSize ("Seal radius (share of rect)", Range(0.1, 0.5)) = 0.4

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex TitleVert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "TitleUI.cginc"

            fixed4 _Color, _InkColor, _FillColor, _SealDark;
            float4 _Size;
            float _Shape, _Seed, _Weight, _Wobble, _Grain, _Taper, _Inset, _Overshoot, _SealSize;

            // coverage of a hand-drawn stroke from a to b: wobbly path, pressure that thins towards the ends
            float Stroke(float2 p, float2 a, float2 b, float w, float seed)
            {
                float2 ab = b - a;
                float len = max(length(ab), 1e-3);
                float2 dir = ab / len, nrm = float2(-dir.y, dir.x);
                float along = dot(p - a, dir);
                float t = saturate(along / len);
                float off = (TitleNoise(float2(along * 0.035, seed)) - 0.5) * 2.0 * _Wobble
                          + (TitleNoise(float2(along * 0.15, seed + 7.0)) - 0.5) * 0.5 * _Wobble;
                float across = dot(p - a, nrm) - off;
                float alongOut = max(-along, along - len);
                float press = lerp(1.0, smoothstep(0.0, 0.18, t) * smoothstep(1.0, 0.75, t) * 0.7 + 0.3, _Taper);
                float hw = w * 0.5 * press;
                float d = length(float2(max(alongOut, 0), across)) - hw;
                return 1.0 - smoothstep(-0.6, 0.6, d);
            }

            float Grain(float2 p, float g)
            {
                float n = TitleNoise(p * float2(0.9, 1.7)) * 0.6 + TitleNoise(p * 2.3) * 0.4;
                return lerp(0.85 + 0.15 * n, smoothstep(0.25, 0.75, n), g);
            }

            fixed4 frag(TitleV2F i) : SV_Target
            {
                float2 size = max(_Size.xy, 1);
                float2 p = (i.uv - 0.5) * size;
                float2 hb = size * 0.5 - _Inset;
                int shape = (int)round(_Shape);
                fixed4 c = 0;

                if (shape == 0)
                {
                    float ink = Stroke(p, float2(-hb.x, 0), float2(hb.x, 0), _Weight, _Seed);
                    c = fixed4(_InkColor.rgb, _InkColor.a * ink * Grain(p + _Seed * 11.0, _Grain));
                }
                else if (shape == 1)
                {
                    float o = _Overshoot;
                    float s = Stroke(p, float2(-hb.x - o, hb.y), float2(hb.x + o * 0.4, hb.y + 0.8), _Weight, _Seed);
                    s = max(s, Stroke(p, float2(-hb.x - o * 0.5, -hb.y), float2(hb.x + o, -hb.y - 0.6), _Weight, _Seed + 2.0));
                    s = max(s, Stroke(p, float2(-hb.x, hb.y + o), float2(-hb.x - 0.8, -hb.y - o * 0.4), _Weight, _Seed + 4.0));
                    s = max(s, Stroke(p, float2(hb.x, hb.y + o * 0.5), float2(hb.x + 0.6, -hb.y - o), _Weight, _Seed + 6.0));
                    float inside = step(abs(p.x), hb.x) * step(abs(p.y), hb.y);
                    fixed4 fill = fixed4(_FillColor.rgb, _FillColor.a * inside);
                    float a = _InkColor.a * s * Grain(p + _Seed * 13.0, _Grain);
                    c.a = a + fill.a * (1.0 - a);
                    c.rgb = (_InkColor.rgb * a + fill.rgb * fill.a * (1.0 - a)) / max(c.a, 1e-4);
                }
                else if (shape == 2)
                {
                    float r = min(size.x, size.y) * _SealSize;
                    float ang = atan2(p.y, p.x);
                    float blob = r * (1.0 + 0.10 * (TitleNoise(float2(ang * 2.2 + _Seed, _Seed)) - 0.5) + 0.05 * sin(ang * 7.0 + _Seed));
                    float d = length(p) - blob;
                    float seal = 1.0 - smoothstep(-0.7, 0.7, d);
                    // pressed ring and a soft highlight on the upper-left
                    float ring = 1.0 - smoothstep(0.4, 1.4, abs(length(p) - r * 0.62));
                    float3 col = lerp(_SealDark.rgb, _InkColor.rgb, saturate(0.65 - dot(p / r, float2(0.45, -0.45))));
                    col = lerp(col, _SealDark.rgb, ring * 0.7);
                    col += 0.18 * exp(-length(p / r - float2(-0.35, 0.38)) * 6.0);
                    // a darker rim where the wax pooled
                    col *= 1.0 - 0.3 * smoothstep(-r * 0.25, 0.0, d);
                    c = fixed4(col, seal * _InkColor.a);
                }
                else if (shape == 3)
                {
                    float2 a = float2(-hb.x * 0.75, hb.y * 0.05);
                    float2 m = float2(-hb.x * 0.2, -hb.y * 0.65);
                    float2 b = float2(hb.x * 0.95, hb.y * 0.95);
                    float s = max(Stroke(p, a, m, _Weight * 1.15, _Seed), Stroke(p, m + float2(-0.5, 0.5), b, _Weight, _Seed + 3.0));
                    c = fixed4(_InkColor.rgb, _InkColor.a * s);
                }
                else
                {
                    float2 a = float2(-hb.x * 0.7, hb.y * 0.35);
                    float2 m = float2(0, -hb.y * 0.4);
                    float2 b = float2(hb.x * 0.7, hb.y * 0.4);
                    float s = max(Stroke(p, a, m, _Weight, _Seed), Stroke(p, m, b, _Weight, _Seed + 3.0));
                    c = fixed4(_InkColor.rgb, _InkColor.a * s);
                }

                c *= _Color * i.color;
                return TitleFinish(c, i);
            }
            ENDCG
        }
    }
}

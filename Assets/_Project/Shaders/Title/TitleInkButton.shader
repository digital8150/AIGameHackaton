// Storybook menu entry, procedural (Image without sprite), drawn on a paper note:
// idle = only the handwritten label; hover = an ink underline is drawn under the label (timed by _Shine)
// and a small inked leaf/pointer slides in on the left; press = the ink blooms thicker with a few splatters.
// Uses the same state properties as TitleButton.shader (_Hover, _Press, _Shine) so TitleButton drives it.
// _Size = rect size in px (set by UIShaderSize).
Shader "DuskTwilight/UI/TitleInkButton"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Size ("Size (px)", Vector) = (420, 64, 0, 0)
        _Seed ("Seed", Float) = 2

        _InkColor ("Ink", Color) = (0.42, 0.12, 0.08, 0.9)
        _WashColor ("Hover wash", Color) = (0.55, 0.35, 0.18, 0.10)
        _LineWidth ("Underline length (share of width)", Range(0.1, 1)) = 0.5
        _LineY ("Underline height (share of half height, negative = below centre)", Range(-1, 1)) = -0.55
        _Weight ("Stroke weight (px)", Float) = 3
        _Wobble ("Wobble (px)", Float) = 1.6
        _MarkX ("Pointer distance left of centre (px)", Float) = 120
        _MarkSize ("Pointer size (px)", Float) = 9

        [Header(State (driven by TitleButton))]
        _Hover ("Hover", Range(0, 1)) = 0
        _Press ("Press", Range(0, 1)) = 0
        _Shine ("Draw progress (0..1)", Range(0, 1)) = 1

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

            fixed4 _Color, _InkColor, _WashColor;
            float4 _Size;
            float _Seed, _LineWidth, _LineY, _Weight, _Wobble, _MarkX, _MarkSize, _Hover, _Press, _Shine;

            fixed4 frag(TitleV2F i) : SV_Target
            {
                float2 size = max(_Size.xy, 1);
                float2 p = (i.uv - 0.5) * size;
                float halfW = size.x * 0.5, halfH = size.y * 0.5;
                float h = _Hover;

                // soft watercolour wash behind the label
                float wash = exp(-pow(p.x / (halfW * 0.55), 2.0) - pow(p.y / (halfH * 0.55), 2.0));
                wash *= 0.8 + 0.2 * TitleNoise(p * 0.08 + _Seed);
                float a = wash * _WashColor.a * h;
                fixed3 col = _WashColor.rgb;

                // underline: drawn left → right as _Shine runs, a slight upward drift like a quick pen stroke
                float L = halfW * _LineWidth;
                float draw = 1.0 - pow(1.0 - _Shine, 3.0);
                float x0 = -L, x1 = lerp(-L, L, draw);
                float t = saturate((p.x - x0) / (2.0 * L));
                float y = halfH * _LineY + t * 3.0
                        + (TitleNoise(float2(p.x * 0.03, _Seed)) - 0.5) * 2.0 * _Wobble;
                float press = lerp(1.0, smoothstep(0.0, 0.12, t) * smoothstep(1.0, 0.7, t) * 0.75 + 0.25, 0.8);
                float w = (_Weight * (1.0 + _Press * 0.6)) * 0.5 * press;
                float dx = max(x0 - p.x, p.x - x1);
                float d = length(float2(max(dx, 0), p.y - y)) - w;
                float line_ = (1.0 - smoothstep(-0.6, 0.6, d)) * h;
                line_ *= 0.8 + 0.2 * TitleNoise(p * float2(0.6, 1.5));

                // inked pointer: a little leaf shape that slides in from the left
                float mx = -_MarkX - (1.0 - h) * 18.0;
                float2 mp = p - float2(mx, 0);
                float leaf = 1.0 - smoothstep(-0.6, 0.6, abs(mp.y) - _MarkSize * 0.45 * (1.0 - pow(abs(mp.x) / _MarkSize, 2.0)));
                leaf *= step(abs(mp.x), _MarkSize) * h;

                // press: a few splatter dots
                float splat = 0;
                for (int k = 0; k < 5; k++)
                {
                    float2 r = TitleHash2(float2(k, _Seed)) - 0.5;
                    float2 sp = float2(r.x * L * 2.2, halfH * _LineY + r.y * 18.0);
                    splat = max(splat, 1.0 - smoothstep(0.0, 1.2, length(p - sp) - (1.0 + 2.5 * frac(r.x * 13.0)) * _Press));
                }
                splat *= _Press;

                float ink = saturate(max(max(line_, leaf), splat)) * _InkColor.a;
                col = lerp(col, _InkColor.rgb, ink / max(ink + a * (1 - ink), 1e-4));
                a = ink + a * (1.0 - ink);

                fixed4 c = fixed4(col, saturate(a)) * _Color * i.color;
                return TitleFinish(c, i);
            }
            ENDCG
        }
    }
}

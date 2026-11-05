// Full-screen drifting embers / motes of dusk light (additive), procedural — no particle system needed
// on an overlay canvas. Three depth layers rise, sway and flicker. _Size = rect size in px (UIShaderSize).
Shader "DuskTwilight/UI/TitleEmbers"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Size ("Size (px)", Vector) = (1920, 1080, 0, 0)
        _WarmColor ("Warm ember", Color) = (1, 0.62, 0.28, 1)
        _CoolColor ("Cool mote", Color) = (0.7, 0.62, 1, 1)
        _Cell ("Cell size (px)", Float) = 90
        _Density ("Density", Range(0, 1)) = 0.35
        _Speed ("Rise speed (cells/s)", Float) = 0.18
        _Intensity ("Intensity", Range(0, 4)) = 1.4
        _Fade ("Fade toward top", Range(0, 1)) = 0.6

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
        Blend SrcAlpha One
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

            fixed4 _Color, _WarmColor, _CoolColor;
            float4 _Size;
            float _Cell, _Density, _Speed, _Intensity, _Fade;

            float3 Layer(float2 px, float scale, float speed, float seed)
            {
                float t = _Time.y;
                float cell = _Cell * scale;
                float2 q = px / cell;
                q.y -= t * _Speed * speed;
                q.x += sin(q.y * 0.7 + seed) * 0.35;
                float2 id = floor(q), f = frac(q);
                float h = TitleHash(id + seed);
                if (h > _Density) return 0;
                float2 c = TitleHash2(id + seed * 3.1) * 0.6 + 0.2;
                c.x += sin(t * (0.6 + h) + h * 40.0) * 0.12;
                float r = length((f - c) * cell);
                float radius = lerp(1.2, 3.4, frac(h * 13.7)) * scale;
                float core = exp(-(r * r) / (radius * radius));
                float flicker = 0.55 + 0.45 * sin(t * (2.0 + h * 5.0) + h * 30.0);
                float3 col = frac(h * 7.3) > 0.3 ? _WarmColor.rgb : _CoolColor.rgb;
                return col * core * flicker;
            }

            fixed4 frag(TitleV2F i) : SV_Target
            {
                float2 px = i.uv * _Size.xy;
                float3 e = Layer(px, 1.0, 1.0, 1.7) + Layer(px, 0.6, 0.7, 5.3) * 0.7 + Layer(px, 1.6, 1.4, 9.1) * 0.8;
                e *= _Intensity * lerp(1.0, 1.0 - i.uv.y, _Fade);
                float a = saturate(max(e.r, max(e.g, e.b)));
                fixed4 c = fixed4(e / max(a, 1e-3), a) * _Color * i.color;
                return TitleFinish(c, i);
            }
            ENDCG
        }
    }
}

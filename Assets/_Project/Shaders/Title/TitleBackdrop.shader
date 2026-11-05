// Title backdrop: the painted dusk sky comes alive — god rays fanning from the sun, a breathing sun glow,
// rippling lake reflection with glints, a slow dusk/night colour drift, vignette and fade-in from black.
Shader "DuskTwilight/UI/TitleBackdrop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Painting", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Sun and rays)]
        _SunPos ("Sun (uv)", Vector) = (0.536, 0.32, 0, 0)
        _Aspect ("Aspect (w/h)", Float) = 1.765
        _RayColor ("Ray colour", Color) = (1, 0.82, 0.52, 1)
        _RayStrength ("Ray strength", Range(0, 1)) = 0.22
        _RayCount ("Ray count", Float) = 26
        _RaySpeed ("Ray drift", Float) = 0.06
        _SunGlow ("Sun glow", Range(0, 1)) = 0.35

        [Header(Lake)]
        _HorizonY ("Lake top (uv)", Range(0, 1)) = 0.185
        _Ripple ("Ripple", Range(0, 4)) = 1
        _Glint ("Glints", Range(0, 2)) = 0.9

        [Header(Grade)]
        _Exposure ("Exposure", Range(0, 2)) = 1
        _Desaturate ("Desaturate", Range(0, 1)) = 0
        _SepiaColor ("Sepia tone", Color) = (0.62, 0.5, 0.38, 1)
        _Sepia ("Sepia amount", Range(0, 1)) = 0
        _Contrast ("Contrast", Range(0.5, 2)) = 1
        _NightColor ("Night side tint", Color) = (0.42, 0.36, 0.78, 1)
        _NightAmount ("Night side amount", Range(0, 1)) = 0.25
        _Vignette ("Vignette", Range(0, 2)) = 0.85
        _Reveal ("Reveal (fade from black)", Range(0, 1)) = 1

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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

            fixed4 _Color, _RayColor, _NightColor;
            float4 _SunPos;
            float _Aspect, _RayStrength, _RayCount, _RaySpeed, _SunGlow;
            float _HorizonY, _Ripple, _Glint, _NightAmount, _Vignette, _Reveal;
            fixed4 _SepiaColor;
            float _Exposure, _Desaturate, _Sepia, _Contrast;

            fixed4 frag(TitleV2F i) : SV_Target
            {
                float t = _Time.y;
                float2 uv = i.uv;

                // --- lake: ripple the reflection below the horizon line ---
                float lake = saturate((_HorizonY - uv.y) / max(_HorizonY, 1e-3));
                float lakeMask = step(uv.y, _HorizonY);
                float2 off = 0;
                off.x = sin(uv.y * 260.0 + t * 1.7 + TitleNoise(uv * 30.0 + t * 0.3) * 4.0) * 0.0016;
                off.y = sin(uv.y * 140.0 - t * 1.1) * 0.0008;
                uv += off * lake * lakeMask * _Ripple;

                fixed4 c = (tex2D(_MainTex, uv) + _TextureSampleAdd) * _Color * i.color;

                // --- grade the painting: desaturate → sepia → contrast → exposure ---
                float lum = dot(c.rgb, float3(0.299, 0.587, 0.114));
                c.rgb = lerp(c.rgb, lum.xxx, _Desaturate);
                c.rgb = lerp(c.rgb, lum * _SepiaColor.rgb * 1.6, _Sepia);
                c.rgb = saturate((c.rgb - 0.5) * _Contrast + 0.5);
                c.rgb *= _Exposure;

                float2 d = (i.uv - _SunPos.xy) * float2(_Aspect, 1);
                float dist = length(d);
                float sky = smoothstep(_HorizonY - 0.02, _HorizonY + 0.12, i.uv.y);

                // --- god rays (angular bands that slowly turn and flicker) ---
                float ang = atan2(d.y, d.x);
                float bands = sin(ang * _RayCount + t * _RaySpeed * 6.2832) * 0.5 + 0.5;
                bands *= TitleNoise(float2(ang * 6.0 + t * _RaySpeed * 3.0, t * 0.15)) * 0.8 + 0.2;
                bands = pow(bands, 2.5);
                float rayFall = exp(-dist * 1.35) * smoothstep(0.02, 0.18, dist);
                float rays = bands * rayFall * sky * _RayStrength;

                // --- sun glow that breathes ---
                float glow = exp(-dist * 7.0) * _SunGlow * (0.85 + 0.15 * sin(t * 0.9));

                c.rgb += _RayColor.rgb * (rays + glow);

                // --- glints on the water near the sun's reflection column ---
                float2 gp = float2(i.uv.x * 140.0, i.uv.y * 520.0 + t * 0.6);
                float g = TitleNoise(gp) * TitleNoise(gp * 1.7 + t * 0.8);
                g = pow(g, 6.0) * 6.0;
                float column = exp(-abs(i.uv.x - _SunPos.x) * 7.0);
                c.rgb += _RayColor.rgb * g * column * lake * lakeMask * _Glint;

                // --- dusk (right) / night (left) slow drift ---
                float side = smoothstep(0.65, 0.0, i.uv.x) * (0.75 + 0.25 * sin(t * 0.25));
                c.rgb = lerp(c.rgb, c.rgb * _NightColor.rgb * 1.35, side * _NightAmount * sky);

                // --- vignette + reveal ---
                float2 v = (i.uv - 0.5) * float2(1.0, 1.25);
                c.rgb *= saturate(1.0 - dot(v, v) * _Vignette);
                c.rgb *= _Reveal;

                return TitleFinish(c, i);
            }
            ENDCG
        }
    }
}

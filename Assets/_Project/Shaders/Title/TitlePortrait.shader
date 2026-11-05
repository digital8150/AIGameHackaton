// Title portrait cutout: back rim light on the edges that face the light, a wind shimmer travelling along
// the rim, a paper-burn reveal (ember edge rising from the bottom), and soft fades at the cut-off edges.
Shader "DuskTwilight/UI/TitlePortrait"
{
    Properties
    {
        [PerRendererData] _MainTex ("Cutout", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Rim light)]
        _RimColor ("Rim colour", Color) = (1, 0.78, 0.45, 1)
        _RimDir ("Light direction (uv, toward light)", Vector) = (0.6, 0.5, 0, 0)
        _RimWidth ("Rim width (uv)", Range(0, 0.05)) = 0.012
        _RimStrength ("Rim strength", Range(0, 4)) = 1.6
        _Shimmer ("Rim shimmer", Range(0, 1)) = 0.5

        [Header(Reveal)]
        _Reveal ("Reveal", Range(0, 1)) = 1
        _BurnColor ("Burn edge colour", Color) = (1, 0.55, 0.2, 1)
        _BurnWidth ("Burn edge width", Range(0.001, 0.2)) = 0.06
        _NoiseScale ("Burn noise scale", Float) = 7

        [Header(Edge fades)]
        _FadeBottom ("Fade bottom", Range(0, 0.6)) = 0.22
        _FadeLeft ("Fade left", Range(0, 0.5)) = 0
        _FadeRight ("Fade right", Range(0, 0.5)) = 0
        _Shade ("Shade (dim toward dark)", Range(0, 1)) = 0.12

        [Header(Grade)]
        _Desaturate ("Desaturate", Range(0, 1)) = 0
        _SepiaColor ("Sepia tone", Color) = (0.62, 0.5, 0.38, 1)
        _Sepia ("Sepia amount", Range(0, 1)) = 0
        _ShadowTint ("Shadow tint (low-key)", Color) = (0.16, 0.11, 0.14, 1)
        _LowKey ("Low-key (lift shadows to tint, crush mids)", Range(0, 1)) = 0
        _LowKeyTop ("Low-key light from top (keeps faces lit)", Range(0, 1)) = 0.5

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

            fixed4 _Color, _RimColor, _BurnColor;
            float4 _RimDir;
            float _RimWidth, _RimStrength, _Shimmer, _Reveal, _BurnWidth, _NoiseScale;
            float _FadeBottom, _FadeLeft, _FadeRight, _Shade;
            fixed4 _SepiaColor, _ShadowTint;
            float _Desaturate, _Sepia, _LowKey, _LowKeyTop;

            float A(float2 uv) { return tex2D(_MainTex, uv).a; }

            fixed4 frag(TitleV2F i) : SV_Target
            {
                float t = _Time.y;
                float2 uv = i.uv;
                fixed4 c = (tex2D(_MainTex, uv) + _TextureSampleAdd) * _Color;

                // rim: this pixel is opaque, but stepping toward the light leaves the silhouette
                float2 dir = normalize(_RimDir.xy + 1e-5);
                float outside = 0;
                outside += 1 - A(uv + dir * _RimWidth * 0.35);
                outside += 1 - A(uv + dir * _RimWidth * 0.7);
                outside += 1 - A(uv + dir * _RimWidth);
                float rim = saturate(outside / 3.0) * c.a;
                float shimmer = TitleNoise(float2(uv.y * 18.0 - t * 1.4, uv.x * 6.0 + t * 0.4));
                rim *= lerp(1.0, 0.35 + 1.3 * shimmer, _Shimmer);

                // grade toward the dim storybook palette
                float lum = dot(c.rgb, float3(0.299, 0.587, 0.114));
                c.rgb = lerp(c.rgb, lum.xxx, _Desaturate);
                c.rgb = lerp(c.rgb, lum * _SepiaColor.rgb * 1.6, _Sepia);
                // low-key: darker toward the bottom (the figure sinks into the gloom), shadows tinted
                float lightK = lerp(1.0, smoothstep(0.0, 1.0, uv.y), _LowKeyTop);
                float3 dark = lerp(_ShadowTint.rgb, c.rgb, lightK);
                c.rgb = lerp(c.rgb, dark * (0.55 + 0.45 * lightK), _LowKey);

                c.rgb *= 1.0 - _Shade;
                c.rgb += _RimColor.rgb * rim * _RimStrength;

                // paper-burn reveal rising from the bottom
                float n = TitleFbm(uv * _NoiseScale + 3.1);
                float thr = n * 0.45 + (1.0 - uv.y) * 0.55;          // bottom burns in first
                float edge = _Reveal * 1.08 - thr;
                float shown = smoothstep(0.0, 0.015, edge);
                float burn = (1.0 - smoothstep(0.0, _BurnWidth, edge)) * step(0.0, edge) * (_Reveal < 0.999);
                c.rgb = lerp(c.rgb, _BurnColor.rgb * 2.2, burn * c.a);
                c.a *= shown;

                // cut-off edges dissolve softly into the backdrop
                c.a *= smoothstep(0.0, _FadeBottom + 1e-4, uv.y);
                c.a *= smoothstep(0.0, _FadeLeft + 1e-4, uv.x);
                c.a *= smoothstep(1.0, 1.0 - _FadeRight - 1e-4, uv.x);

                c *= i.color;
                return TitleFinish(c, i);
            }
            ENDCG
        }
    }
}

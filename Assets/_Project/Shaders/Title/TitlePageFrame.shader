// Full-screen storybook-page overlay (multiply): paper grain and fibres, faint stains and foxing,
// and frayed, darkened page edges — makes the painted title read as an illustration printed in an old book.
// Put it above the painting and characters, below the logo/menu. Vertex/Image alpha fades the effect out.
Shader "DuskTwilight/UI/TitlePageFrame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        _Color ("Tint (alpha = strength)", Color) = (1,1,1,1)
        _PaperTint ("Paper tint", Color) = (1.0, 0.93, 0.80, 1)
        _Grain ("Paper grain", Range(0, 0.5)) = 0.14
        _Stain ("Stains / foxing", Range(0, 1)) = 0.35
        _EdgeColor ("Edge burn", Color) = (0.20, 0.12, 0.07, 1)
        _Edge ("Edge width (share of height)", Range(0, 0.3)) = 0.07
        _EdgeStrength ("Edge strength", Range(0, 1)) = 0.85
        _Fray ("Frayed edge noise", Range(0, 0.1)) = 0.035
        _Aspect ("Aspect (w/h)", Float) = 1.777

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
        Blend DstColor Zero
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex TitleVert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "TitleUI.cginc"

            fixed4 _Color, _PaperTint, _EdgeColor;
            float _Grain, _Stain, _Edge, _EdgeStrength, _Fray, _Aspect;

            fixed4 frag(TitleV2F i) : SV_Target
            {
                float2 uv = i.uv;
                float2 p = uv * float2(_Aspect, 1);
                float fib = TitleNoise(p * float2(40, 900)) * 0.35 + TitleNoise(p * float2(700, 30)) * 0.15 + TitleNoise(p * 260) * 0.5;
                float3 m = _PaperTint.rgb * (1.0 - _Grain + _Grain * 2.0 * fib);
                float blot = smoothstep(0.55, 0.9, TitleFbm(p * 2.2 + 3.1));
                float fox = smoothstep(0.9, 1.0, TitleNoise(p * 55.0)) * TitleNoise(p * 6.0);
                m *= 1.0 - _Stain * (0.18 * blot + 0.45 * fox);

                float2 e = min(uv, 1 - uv) * float2(_Aspect, 1);
                float edge = min(e.x, e.y) + (TitleFbm(p * 12.0) - 0.5) * _Fray * 2.0;
                float burn = 1.0 - smoothstep(0.0, _Edge, edge);
                m = lerp(m, _EdgeColor.rgb, burn * burn * _EdgeStrength);

                float k = _Color.a * i.color.a;
                fixed4 c = fixed4(lerp(1.0, m, k), 1);
                #ifdef UNITY_UI_CLIP_RECT
                c.rgb = lerp(1.0, c.rgb, UnityGet2DClipping(i.worldPosition.xy, _ClipRect));
                #endif
                return c;
            }
            ENDCG
        }
    }
}

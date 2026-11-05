// A storybook illustration printed on the page (RawImage): the picture is multiplied onto the paper underneath
// in sepia, its edge fades out in a soft, ink-frayed vignette so it sits in the page instead of on top of it.
Shader "DuskTwilight/UI/TitleInkPicture"
{
    Properties
    {
        [PerRendererData] _MainTex ("Picture", 2D) = "white" {}
        _Color ("Tint (alpha = strength)", Color) = (1,1,1,1)
        _InkDark ("Sepia ink", Color) = (0.20, 0.12, 0.07, 1)
        _InkLight ("Paper", Color) = (1, 1, 1, 1)
        _Contrast ("Contrast", Range(0.5, 2)) = 1.15
        _Fade ("Edge fade (share of the rect)", Range(0.01, 0.5)) = 0.16
        _Fray ("Frayed edge noise", Range(0, 0.2)) = 0.06

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

            fixed4 _Color, _InkDark, _InkLight;
            float _Contrast, _Fade, _Fray;

            fixed4 frag(TitleV2F i) : SV_Target
            {
                float3 pic = tex2D(_MainTex, i.uv).rgb;
                float l = dot(pic, float3(0.3, 0.59, 0.11));
                l = saturate((l - 0.5) * _Contrast + 0.5);
                float3 m = lerp(_InkDark.rgb, _InkLight.rgb, l);

                float2 e = min(i.uv, 1 - i.uv);
                float edge = min(e.x, e.y) + (TitleFbm(i.uv * 18.0) - 0.5) * _Fray;
                float k = smoothstep(0.0, _Fade, edge) * _Color.a * i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                k *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                return fixed4(lerp(1.0, m, k), 1);
            }
            ENDCG
        }
    }
}

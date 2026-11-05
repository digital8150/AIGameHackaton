// A character portrait inside its emblem: the same glint sweep and switch flash as the frame, so the badge shines as one piece.
Shader "DuskTwilight/UI/EmblemPortrait"
{
    Properties
    {
        [PerRendererData] _MainTex ("Portrait", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GlintColor ("Glint", Color) = (1, 0.96, 0.82, 1)
        _GlintStrength ("Glint strength", Range(0, 1)) = 0.55
        _FlashStrength ("Flash strength", Range(0, 1)) = 0.35
        _Flash ("Flash (script)", Range(0, 1)) = 0
        _Glint ("Glint (script)", Range(0, 1)) = 0

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
            #include "Emblem.cginc"

            float _GlintStrength, _FlashStrength;

            fixed4 frag(TitleV2F i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv) + _TextureSampleAdd;
                float2 p = (i.uv - 0.5) * 2;
                tex.rgb = lerp(tex.rgb, _GlintColor.rgb, saturate(EmblemGlintBand(p, 1) * _GlintStrength));
                tex.rgb = lerp(tex.rgb, _GlintColor.rgb, _Flash * _FlashStrength);
                return TitleFinish(tex * _Color * i.color, i);
            }
            ENDCG
        }
    }
}

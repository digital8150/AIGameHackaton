// A character portrait inside its emblem: a procedural disc with one frame of the idle sheet on it, cut to a circle.
// The same glint sweep and switch flash as the frame, so the badge shines as one piece.
Shader "DuskTwilight/UI/EmblemPortrait"
{
    Properties
    {
        [PerRendererData] _MainTex ("Portrait sheet", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BackColor ("Disc colour", Color) = (0.227, 0.173, 0.259, 1)
        _Radius ("Disc radius (1 = rect edge)", Range(0.5, 1)) = 0.98
        _Feather ("Edge softness", Range(0, 0.2)) = 0.015
        _Zoom ("Portrait zoom", Range(0.5, 3)) = 1
        _PortraitOffset ("Portrait offset (x, y in frames)", Vector) = (0, 0, 0, 0)
        _FrameRect ("Frame in sheet (script: x, y, w, h)", Vector) = (0, 0.91666667, 0.08333333, 0.08333333)
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

            float _GlintStrength, _FlashStrength, _Radius, _Feather, _Zoom;
            fixed4 _BackColor;
            float4 _FrameRect, _PortraitOffset;

            fixed4 frag(TitleV2F i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2;
                float r = length(p);

                // uv covers the rect, so the frame is looked up here rather than through the mesh
                float2 cell = (i.uv - 0.5) / _Zoom + 0.5 + _PortraitOffset.xy;
                fixed4 tex = tex2D(_MainTex, _FrameRect.xy + cell * _FrameRect.zw) + _TextureSampleAdd;
                float2 inside = step(0, cell) * step(cell, 1);
                tex.a *= inside.x * inside.y;

                fixed4 c = _BackColor;
                c.rgb = lerp(c.rgb, tex.rgb, tex.a);
                c.rgb = lerp(c.rgb, _GlintColor.rgb, saturate(EmblemGlintBand(p, 1) * _GlintStrength));
                c.rgb = lerp(c.rgb, _GlintColor.rgb, _Flash * _FlashStrength);

                float edge = max(_Feather, fwidth(r));
                c.a *= 1 - smoothstep(_Radius - edge, _Radius, r);
                return TitleFinish(c * _Color * i.color, i);
            }
            ENDCG
        }
    }
}

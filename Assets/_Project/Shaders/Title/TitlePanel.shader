// Ornate panel plate (settings window, dropdown lists, slider tracks), procedural:
// translucent indigo parchment with fibre noise, a double gilt frame, corner diamonds, inner glow at the top.
// _Size = rect size in px (set by UIShaderSize). Set _Frame 0 for a plain track/list.
Shader "DuskTwilight/UI/TitlePanel"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Size ("Size (px)", Vector) = (900, 600, 0, 0)
        _Radius ("Corner radius (px)", Float) = 10

        _TopColor ("Fill top", Color) = (0.13, 0.10, 0.22, 0.92)
        _BottomColor ("Fill bottom", Color) = (0.05, 0.04, 0.10, 0.94)
        _Fibre ("Paper fibre", Range(0, 0.5)) = 0.08
        _LineColor ("Gilt frame", Color) = (0.95, 0.76, 0.45, 1)
        _Frame ("Frame (0 = none)", Range(0, 1)) = 1
        _Inset1 ("Outer frame inset (px)", Float) = 6
        _Inset2 ("Inner frame inset (px)", Float) = 12
        _Corner ("Corner diamond size (px)", Float) = 7
        _Glow ("Top glow", Range(0, 1)) = 0.25

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

            fixed4 _Color, _TopColor, _BottomColor, _LineColor;
            float4 _Size;
            float _Radius, _Fibre, _Frame, _Inset1, _Inset2, _Corner, _Glow;

            // signed distance to a rounded box (negative inside)
            float RBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0)) + min(max(q.x, q.y), 0) - r;
            }

            fixed4 frag(TitleV2F i) : SV_Target
            {
                float2 size = max(_Size.xy, 1);
                float2 p = (i.uv - 0.5) * size;
                float2 hb = size * 0.5;

                float d = RBox(p, hb, _Radius);
                float inside = 1.0 - smoothstep(-1.0, 0.5, d);

                fixed4 fill = lerp(_BottomColor, _TopColor, i.uv.y);
                float fibre = TitleNoise(p * float2(0.04, 0.9)) * 0.6 + TitleNoise(p * 0.15) * 0.4;
                fill.rgb *= 1.0 - _Fibre + _Fibre * 2.0 * fibre;
                fill.rgb += _LineColor.rgb * _Glow * exp(-(hb.y - p.y) / (size.y * 0.25)) * 0.25;

                fixed3 col = fill.rgb;
                float a = fill.a * inside;

                if (_Frame > 0)
                {
                    float l1 = 1.0 - smoothstep(0.6, 1.6, abs(RBox(p, hb - _Inset1, max(_Radius - _Inset1, 0))));
                    float l2 = 1.0 - smoothstep(0.3, 1.0, abs(RBox(p, hb - _Inset2, max(_Radius - _Inset2, 0))));
                    float2 cp = abs(p) - (hb - _Inset2);
                    float corner = TitleDiamond(cp, _Corner, 0.8);
                    float frame = saturate(l1 + l2 * 0.6 + corner) * _Frame;
                    col = lerp(col, _LineColor.rgb, frame);
                    a = max(a, frame * inside);
                }

                fixed4 c = fixed4(col, a) * _Color * i.color;
                return TitleFinish(c, i);
            }
            ENDCG
        }
    }
}

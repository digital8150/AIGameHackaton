// Small glowing diamond (slider handle, toggle mark, ornaments, logo divider), procedural.
// Fills the rect; facets shade the two halves, a soft halo glows around it.
Shader "DuskTwilight/UI/TitleGem"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GemColor ("Gem", Color) = (1, 0.84, 0.55, 1)
        _DarkColor ("Facet shade", Color) = (0.62, 0.38, 0.2, 1)
        _GemSize ("Gem size (0..0.5 of rect)", Range(0.05, 0.5)) = 0.3
        _Halo ("Halo", Range(0, 2)) = 0.6
        _Pulse ("Pulse speed", Float) = 1.5

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

            fixed4 _Color, _GemColor, _DarkColor;
            float _GemSize, _Halo, _Pulse;

            fixed4 frag(TitleV2F i) : SV_Target
            {
                float2 p = i.uv - 0.5;
                float aa = fwidth(p.x) * 1.2;
                float gem = TitleDiamond(p, _GemSize, aa);
                fixed3 col = (p.x + p.y) > 0 ? _GemColor.rgb : lerp(_DarkColor.rgb, _GemColor.rgb, 0.45);
                col = lerp(col, 1, TitleDiamond(p - float2(-0.06, 0.06) * _GemSize * 3, _GemSize * 0.18, aa) * 0.8);
                float halo = exp(-(abs(p.x) + abs(p.y)) / (_GemSize * 0.9)) * _Halo * (0.8 + 0.2 * sin(_Time.y * _Pulse));
                float a = max(gem, halo * (1 - gem));
                col = lerp(_GemColor.rgb, col, gem);
                fixed4 c = fixed4(col, saturate(a)) * _Color * i.color;
                return TitleFinish(c, i);
            }
            ENDCG
        }
    }
}

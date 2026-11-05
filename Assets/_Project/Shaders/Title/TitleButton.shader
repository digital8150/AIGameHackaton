// Menu button plate, fully procedural (Image without sprite): a dusky band that fades at both ends,
// gilt hairlines above and below that grow on hover, diamond studs that slide in beside the label,
// a light sweep on hover and a warm pulse on press. _Size = rect size in px (set by UIShaderSize).
Shader "DuskTwilight/UI/TitleButton"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Size ("Size (px)", Vector) = (420, 64, 0, 0)

        [Header(Colours)]
        _BandColor ("Band (idle)", Color) = (0.08, 0.06, 0.16, 0.45)
        _BandHoverColor ("Band (hover)", Color) = (0.55, 0.32, 0.16, 0.6)
        _LineColor ("Gilt line", Color) = (1, 0.82, 0.52, 1)
        _GlowColor ("Glow", Color) = (1, 0.7, 0.38, 1)

        [Header(Shape)]
        _LineInset ("Line inset (px)", Float) = 3
        _LineIdle ("Line length idle (0..1)", Range(0, 1)) = 0.35
        _LineHover ("Line length hover (0..1)", Range(0, 1)) = 0.9
        _StudX ("Stud distance from centre (px)", Float) = 130
        _StudSize ("Stud size (px)", Float) = 7

        [Header(State (driven by TitleButton))]
        _Hover ("Hover", Range(0, 1)) = 0
        _Press ("Press", Range(0, 1)) = 0
        _Shine ("Shine sweep (0..1)", Range(0, 1)) = 1

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

            fixed4 _Color, _BandColor, _BandHoverColor, _LineColor, _GlowColor;
            float4 _Size;
            float _LineInset, _LineIdle, _LineHover, _StudX, _StudSize, _Hover, _Press, _Shine;

            fixed4 frag(TitleV2F i) : SV_Target
            {
                float t = _Time.y;
                float2 size = max(_Size.xy, 1);
                float2 p = (i.uv - 0.5) * size;
                float halfW = size.x * 0.5, halfH = size.y * 0.5;
                float h = _Hover;

                // band: fades at the ends, gentle vertical falloff, hover warms it
                float ends = 1.0 - smoothstep(halfW * lerp(0.15, 0.45, h), halfW, abs(p.x));
                float vert = 1.0 - pow(abs(p.y) / halfH, 4.0);
                fixed4 band = lerp(_BandColor, _BandHoverColor, h);
                band.a *= ends * vert;
                fixed3 col = band.rgb;
                float a = band.a;

                // gilt hairlines (top + bottom)
                float lineLen = lerp(_LineIdle, _LineHover, h) * halfW;
                float ly = abs(abs(p.y) - (halfH - _LineInset));
                float gilt = (1.0 - smoothstep(0.4, 1.3, ly)) * (1.0 - smoothstep(lineLen * 0.5, lineLen, abs(p.x)));
                gilt *= 0.55 + 0.45 * h;
                col = lerp(col, _LineColor.rgb, gilt);
                a = max(a, gilt * _LineColor.a);

                // centre jewel on the lines, always there
                float jewel = TitleDiamond(float2(p.x, abs(p.y) - (halfH - _LineInset)), 3.0 + h, 0.8);
                col = lerp(col, _LineColor.rgb, jewel);
                a = max(a, jewel);

                // diamond studs slide in beside the label on hover
                float sx = _StudX + (1.0 - h) * 24.0;
                float stud = TitleDiamond(float2(abs(p.x) - sx, p.y), _StudSize * h, 0.8);
                float studGlow = exp(-length(float2(abs(p.x) - sx, p.y)) / (6.0 + 6.0 * h)) * h;
                col = lerp(col, _LineColor.rgb * 1.2, stud);
                col += _GlowColor.rgb * studGlow * 0.6;
                a = max(a, max(stud, studGlow * 0.5));

                // light sweep across the band on hover
                float sw = lerp(-0.2, 1.2, _Shine) * size.x - halfW;
                float sd = (p.x + p.y * 0.6 - sw) / 40.0;
                float sweep = exp(-sd * sd) * h * ends * (_Shine < 0.999);
                col += _GlowColor.rgb * sweep * 0.8;
                a = max(a, sweep * 0.5);

                // breathing glow + press flash
                float breathe = (0.75 + 0.25 * sin(t * 2.4)) * h;
                col += _GlowColor.rgb * (breathe * 0.12 + _Press * 0.45) * ends * vert;

                fixed4 c = fixed4(col, saturate(a)) * _Color * i.color;
                return TitleFinish(c, i);
            }
            ENDCG
        }
    }
}

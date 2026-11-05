// Storybook / memo-pad paper sheet (settings page, menu note, dropdown lists), procedural:
// aged paper with fibres, stains and foxing, darkened frayed edges, an optional torn top edge,
// ruled lines with a margin line, punched holes with ring binding, a strip of tape and a soft drop shadow.
// _Size = rect size in px (set by UIShaderSize). The paper sits _Pad px inside the rect (room for shadow/tape/rings).
Shader "DuskTwilight/UI/TitlePaper"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Size ("Size (px)", Vector) = (900, 720, 0, 0)
        _Pad ("Padding around the sheet (px)", Float) = 24
        _Seed ("Seed", Float) = 3.1

        [Header(Paper)]
        _PaperColor ("Paper", Color) = (0.80, 0.71, 0.55, 1)
        _AgedColor ("Aged edge / stains", Color) = (0.50, 0.36, 0.22, 1)
        _Fibre ("Fibre", Range(0, 0.3)) = 0.08
        _Stain ("Stains and foxing", Range(0, 1)) = 0.5
        _EdgeAge ("Edge darkening width (px)", Float) = 60
        _Fray ("Frayed edge (px)", Range(0, 12)) = 2.5
        _TornTop ("Torn top edge (px, 0 = clean)", Range(0, 20)) = 0

        [Header(Ruling)]
        _RuleColor ("Rule lines", Color) = (0.35, 0.40, 0.48, 0.32)
        _RuleTop ("First rule from top (px)", Float) = 110
        _RuleSpacing ("Rule spacing (px, 0 = none)", Float) = 46
        _RuleBottom ("Last rule from bottom (px)", Float) = 30
        _MarginX ("Margin line from left (px, 0 = none)", Float) = 100
        _MarginColor ("Margin line", Color) = (0.62, 0.22, 0.16, 0.45)

        [Header(Binding)]
        _Holes ("Punched holes along the top (count, 0 = none)", Float) = 0
        _HoleY ("Hole centre from top (px)", Float) = 22
        _HoleR ("Hole radius (px)", Float) = 7
        _Rings ("Ring binding", Range(0, 1)) = 1
        _RingColor ("Ring metal", Color) = (0.32, 0.27, 0.22, 1)

        [Header(Tape)]
        _Tape ("Tape strip", Range(0, 1)) = 0
        _TapeColor ("Tape", Color) = (0.86, 0.80, 0.64, 0.55)
        _TapeRect ("Tape (x, y from top-centre px, w, h)", Vector) = (0, 0, 110, 30)
        _TapeAngle ("Tape angle (deg)", Float) = -4

        [Header(Shadow)]
        _Shadow ("Shadow", Range(0, 1)) = 0.6
        _ShadowOffset ("Shadow offset (px)", Vector) = (6, -10, 0, 0)
        _ShadowBlur ("Shadow blur (px)", Float) = 14

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

            fixed4 _Color, _PaperColor, _AgedColor, _RuleColor, _MarginColor, _RingColor, _TapeColor;
            float4 _Size, _ShadowOffset, _TapeRect;
            float _Pad, _Seed, _Fibre, _Stain, _EdgeAge, _Fray, _TornTop;
            float _RuleTop, _RuleSpacing, _RuleBottom, _MarginX;
            float _Holes, _HoleY, _HoleR, _Rings, _Tape, _TapeAngle;
            float _Shadow, _ShadowBlur;

            float Box(float2 p, float2 b)
            {
                float2 q = abs(p) - b;
                return length(max(q, 0)) + min(max(q.x, q.y), 0);
            }

            // distance to the sheet outline, with a slightly irregular (cut by hand) edge and an optional torn top
            float Sheet(float2 p, float2 hb)
            {
                float d = Box(p, hb - 2.0) - 2.0;
                float2 s = p + _Seed * 37.0;
                float along = abs(p.x) > abs(p.y) * (hb.x / hb.y) ? s.y : s.x;
                d += (TitleNoise(float2(along * 0.05, _Seed)) - 0.5) * _Fray * 1.6;
                d += (TitleNoise(float2(along * 0.6, _Seed * 3.0)) - 0.5) * _Fray * 0.5;
                if (_TornTop > 0)
                {
                    float top = hb.y - p.y;   // distance from the top edge, inwards
                    float tear = (TitleNoise(float2(s.x * 0.09, 1.7)) * 0.65 + TitleNoise(float2(s.x * 0.45, 4.2)) * 0.35) * _TornTop;
                    d = max(d, tear - top);
                }
                return d;
            }

            fixed4 frag(TitleV2F i) : SV_Target
            {
                float2 size = max(_Size.xy, 1);
                float2 p = (i.uv - 0.5) * size;
                float2 hb = size * 0.5 - _Pad;
                float2 fromTL = float2(p.x + hb.x, hb.y - p.y);   // px from the sheet's top-left corner

                // drop shadow
                float ds = Sheet(p - _ShadowOffset.xy, hb);
                float shadow = (1.0 - smoothstep(-_ShadowBlur * 0.5, _ShadowBlur, ds)) * _Shadow;

                float d = Sheet(p, hb);
                float inside = 1.0 - smoothstep(-0.8, 0.8, d);

                // punched holes: transparent through the sheet
                float holes = 0;
                float hx = 0;
                if (_Holes > 0)
                {
                    // nearest hole centre, clamped to the real holes so nothing repeats past the sheet's sides
                    float spacing = hb.x * 2.0 / _Holes;
                    float idx = clamp(floor((p.x + hb.x) / spacing), 0.0, _Holes - 1.0);
                    hx = p.x + hb.x - (idx + 0.5) * spacing;
                    float hd = length(float2(hx, fromTL.y - _HoleY)) - _HoleR;
                    holes = 1.0 - smoothstep(-0.8, 0.8, hd);
                    inside *= 1.0 - holes;
                }

                // paper body
                float2 q = p + _Seed * 91.0;
                float fib = TitleNoise(q * float2(0.05, 0.9)) * 0.5 + TitleNoise(q * float2(0.9, 0.06)) * 0.2 + TitleNoise(q * 0.3) * 0.3;
                fixed3 col = _PaperColor.rgb * (1.0 - _Fibre + _Fibre * 2.0 * fib);
                float blot = smoothstep(0.5, 0.85, TitleFbm(q * 0.006));
                float fox = smoothstep(0.88, 1.0, TitleNoise(q * 0.09)) * TitleNoise(q * 0.012);
                col = lerp(col, _AgedColor.rgb, _Stain * (blot * 0.22 + fox * 0.45));
                float edge = saturate(-d / max(_EdgeAge, 1));
                edge = edge + (TitleFbm(q * 0.03) - 0.5) * 0.25;
                col = lerp(_AgedColor.rgb, col, smoothstep(0.0, 1.0, saturate(edge)) * 0.75 + 0.25);
                col = lerp(_AgedColor.rgb * 0.7, col, smoothstep(0.0, 2.5, -d));   // dark burnt rim

                // ruled lines + margin
                if (_RuleSpacing > 0)
                {
                    float y = fromTL.y - _RuleTop;
                    float maxY = hb.y * 2.0 - _RuleBottom - _RuleTop;
                    float wob = (TitleNoise(float2(p.x * 0.02, floor(y / _RuleSpacing + 0.5))) - 0.5) * 0.8;
                    float ly = abs(frac(y / _RuleSpacing + 0.5) - 0.5) * _RuleSpacing + wob * 0.3;
                    float rule = (1.0 - smoothstep(0.35, 1.1, ly)) * step(-0.5 * _RuleSpacing, y) * step(y, maxY + 0.5);
                    rule *= 0.75 + 0.25 * TitleNoise(p * float2(0.2, 1.0));   // faded print
                    col = lerp(col, _RuleColor.rgb, rule * _RuleColor.a);
                }
                if (_MarginX > 0)
                {
                    float mx = abs(fromTL.x - _MarginX);
                    float margin = (1.0 - smoothstep(0.4, 1.2, mx)) + (1.0 - smoothstep(0.3, 0.9, abs(fromTL.x - _MarginX - 4.0))) * 0.6;
                    margin *= step(_HoleY + _HoleR + 8.0, fromTL.y);
                    col = lerp(col, _MarginColor.rgb, saturate(margin) * _MarginColor.a);
                }
                // holes: a slight dark ring where the punch crushed the paper
                if (_Holes > 0)
                {
                    float hd = length(float2(hx, fromTL.y - _HoleY)) - _HoleR;
                    col *= 1.0 - 0.35 * (1.0 - smoothstep(0.0, 3.0, hd));
                }

                float4 c = float4(col, inside);
                c = lerp(float4(0, 0, 0, shadow), c, inside);   // shadow under the sheet

                // ring binding: coils from each hole over the top edge
                if (_Holes > 0 && _Rings > 0)
                {
                    // coil = a tall loop: comes up out of the hole on the front (left half), arcs over the top edge
                    // and goes down the back (right half), which is hidden once it drops behind the sheet
                    float2 q = float2(hx, _HoleY - fromTL.y);           // y up, 0 = hole centre
                    float top = _HoleY + 14.0;                          // loop height above the hole
                    float2 rr = float2(_HoleR * 1.1, top * 0.5);
                    float2 qc = q - float2(0, rr.y);
                    float e = length(qc / rr);
                    float rd = abs((e - 1.0) * e / max(length(qc / (rr * rr)), 1e-4)) - 1.5;
                    float front = step(qc.x, 0.0);
                    float vis = max(front, step(_HoleY + 0.5, q.y));    // back half only above the paper edge
                    float ring = (1.0 - smoothstep(-0.7, 0.7, rd)) * vis * _Rings;
                    float shade = front > 0 ? 1.0 + 0.35 * exp(-pow((e - 1.0) * 6.0, 2.0)) : 0.55;
                    float3 metal = _RingColor.rgb * shade + (front > 0 ? 0.12 : 0.0);
                    c.rgb = lerp(c.rgb, metal, ring);
                    c.a = max(c.a, ring);
                }

                // tape strip
                if (_Tape > 0)
                {
                    float a = radians(_TapeAngle);
                    float2 tc = float2(_TapeRect.x, hb.y - _TapeRect.y);
                    float2 tp = p - tc;
                    tp = float2(cos(a) * tp.x + sin(a) * tp.y, -sin(a) * tp.x + cos(a) * tp.y);
                    float2 th = _TapeRect.zw * 0.5;
                    // jagged torn ends
                    float jag = (TitleNoise(float2(tp.y * 0.5, sign(tp.x) * 3.0 + _Seed)) - 0.5) * 6.0;
                    float td = max(abs(tp.x) - th.x - jag, abs(tp.y) - th.y);
                    float tape = (1.0 - smoothstep(-0.6, 0.6, td)) * _Tape;
                    float tn = 0.9 + 0.1 * TitleNoise(tp * float2(0.08, 0.6));
                    float3 tcol = _TapeColor.rgb * tn;
                    float ta = tape * _TapeColor.a;
                    float oa = ta + c.a * (1.0 - ta);
                    c.rgb = (tcol * ta + c.rgb * c.a * (1.0 - ta)) / max(oa, 1e-4);
                    c.a = oa;
                }

                fixed4 o = fixed4(c.rgb, saturate(c.a)) * _Color * i.color;
                return TitleFinish(o, i);
            }
            ENDCG
        }
    }
}

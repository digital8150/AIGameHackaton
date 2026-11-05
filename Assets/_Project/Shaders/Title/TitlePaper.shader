// Storybook paper (settings spread, pause page, menu card, HUD notes, dropdown lists), procedural:
// aged paper with fibres, stains and foxing, burnt frayed edges, a printed ornamental frame, and optionally
// an open-book spread (spine shadow, both pages framed), a page block and a leather cover board behind it.
// Older memo-pad extras (ruling, margin line, ring binding, tape, torn top) are still here but off by default.
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

        [Header(Storybook)]
        _Tear ("Torn parchment edge (px)", Range(0, 12)) = 0
        _Burn ("Burnt edge width (px)", Range(0, 40)) = 10
        _Border ("Printed frame inset (px, 0 = none)", Float) = 0
        _BorderColor ("Printed frame ink", Color) = (0.36, 0.20, 0.12, 0.75)
        _Corner ("Frame corner ornament (px)", Float) = 9
        _Spread ("Open book spread (two pages)", Range(0, 1)) = 0
        _Gutter ("Spine shadow", Range(0, 1)) = 0.6
        _Stack ("Page block layers", Range(0, 6)) = 0
        _Cover ("Cover board margin (px, 0 = none)", Float) = 0
        _CoverColor ("Cover leather", Color) = (0.27, 0.12, 0.08, 1)

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
            float _Tear, _Burn, _Border, _Corner, _Spread, _Gutter, _Stack, _Cover;
            fixed4 _BorderColor, _CoverColor;

            float RBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0)) + min(max(q.x, q.y), 0) - r;
            }

            // premultiplied "over"
            float4 Over(float4 top, float4 under)
            {
                float a = top.a + under.a * (1.0 - top.a);
                return float4((top.rgb * top.a + under.rgb * under.a * (1.0 - top.a)) / max(a, 1e-4), a);
            }

            float Box(float2 p, float2 b)
            {
                float2 q = abs(p) - b;
                return length(max(q, 0)) + min(max(q.x, q.y), 0);
            }

            // distance to the sheet outline, with a slightly irregular (cut by hand) edge and an optional torn top
            float Sheet(float2 p, float2 hb)
            {
                float d = Box(p, hb - 2.0) - 2.0;
                d += _Spread * 7.0 * exp(-abs(p.x) / 90.0) * smoothstep(hb.y * 0.6, hb.y, abs(p.y));   // pages bow into the spine
                float2 s = p + _Seed * 37.0;
                float along = abs(p.x) > abs(p.y) * (hb.x / hb.y) ? s.y : s.x;
                d += (TitleNoise(float2(along * 0.05, _Seed)) - 0.5) * _Fray * 1.6;
                d += (TitleNoise(float2(along * 0.6, _Seed * 3.0)) - 0.5) * _Fray * 0.5;
                if (_Tear > 0)
                {
                    // parchment torn by hand: ragged fibres all round, the odd little bite and a rare deeper nick
                    float side = abs(p.x) > abs(p.y) * (hb.x / hb.y) ? sign(p.x) * 2.0 : sign(p.y) * 5.0;
                    float2 e = float2(along, side + _Seed);
                    float rag = TitleNoise(e * float2(0.15, 1)) * 0.55 + TitleNoise(e * float2(0.7, 1) + 3.1) * 0.3 + TitleNoise(e * float2(2.2, 1) + 7.7) * 0.15;
                    float bite = smoothstep(0.72, 0.95, TitleNoise(e * float2(0.022, 1) + 11.3));
                    float nick = smoothstep(0.90, 0.99, TitleNoise(e * float2(0.05, 1) + 23.9)) * (0.6 + 0.4 * TitleNoise(e * float2(0.9, 1)));
                    d += _Tear * (rag + bite * 1.6 + nick * 3.5);
                }
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

                // drop shadow (of the cover board when there is one)
                float ds = _Cover > 0 ? RBox(p - _ShadowOffset.xy, hb + _Cover, 8.0) : Sheet(p - _ShadowOffset.xy, hb + _Stack * 1.5);
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
                if (_Burn > 0)
                {
                    // scorched, eroded border like the worn storybook pages of the stage transition
                    float er = -d + (TitleFbm(q * 0.05) - 0.5) * _Burn * 0.9;
                    float burn = 1.0 - smoothstep(0.0, _Burn, er);
                    col = lerp(col, _AgedColor.rgb * 0.45, burn * burn * 0.85);
                    float scr = smoothstep(0.985, 1.0, TitleNoise(float2(q.x * 0.02 + q.y * 0.005, q.y * 1.3)));
                    col = lerp(col, _PaperColor.rgb * 1.08, scr * 0.25);   // fine scratches
                }

                // open book: spine shadow, a crease, and pages darkening as they curve into it
                if (_Spread > 0)
                {
                    float g = abs(p.x);
                    col *= 1.0 - _Spread * _Gutter * (0.38 * exp(-g / 55.0) + 0.45 * exp(-g / 2.5));
                    col *= 1.0 - _Spread * 0.10 * pow(saturate(g / hb.x), 6.0);
                }

                // printed ornamental frame (one per page on a spread)
                if (_Border > 0)
                {
                    float hw = _Spread > 0 ? hb.x * 0.5 : hb.x;
                    float2 pl = _Spread > 0 ? float2(abs(p.x) - hw, p.y) : p;
                    float2 fb = float2(hw, hb.y) - _Border;
                    float l1 = 1.0 - smoothstep(0.5, 1.4, abs(Box(pl, fb)));
                    float l2 = 1.0 - smoothstep(0.3, 0.9, abs(Box(pl, fb - 6.0)));
                    float2 cp = abs(pl) - fb;   // corner ornaments: a diamond with a little leaf each way
                    float orn = TitleDiamond(cp, _Corner * 0.55, 0.8);
                    orn = max(orn, TitleDiamond((cp - float2(-_Corner * 1.1, 0)) * float2(0.7, 1.8), _Corner * 0.45, 0.8));
                    orn = max(orn, TitleDiamond((cp - float2(0, -_Corner * 1.1)) * float2(1.8, 0.7), _Corner * 0.45, 0.8));
                    float frame = saturate(l1 + l2 * 0.7 + orn) * (0.75 + 0.25 * TitleNoise(q * 0.4));   // worn print
                    col = lerp(col, _BorderColor.rgb, frame * _BorderColor.a);
                }

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

                float4 c = float4(0, 0, 0, shadow);
                // leather cover board
                if (_Cover > 0)
                {
                    float cd = RBox(p, hb + _Cover, 8.0);
                    float cov = 1.0 - smoothstep(-0.8, 0.8, cd);
                    float grain = TitleNoise(q * 0.35) * 0.5 + TitleFbm(q * 0.04) * 0.5;
                    float3 lc = _CoverColor.rgb * (0.78 + 0.4 * grain);
                    lc *= 1.0 - 0.45 * (1.0 - smoothstep(0.0, 10.0, -cd));       // worn dark rim
                    lc += 0.05 * (1.0 - smoothstep(0.4, 1.2, abs(cd + 7.0)));       // tooled line
                    lc *= _Spread > 0 ? 1.0 - 0.35 * exp(-abs(p.x) / 30.0) : 1.0;  // spine
                    c = Over(float4(lc, cov), c);
                }
                // page block: the edges of the pages underneath
                for (int k = 6; k >= 1; k--)
                {
                    if (k > _Stack) continue;
                    float2 off = float2((_Spread > 0 ? (p.x >= 0 ? 1.0 : -1.0) : 1.0) * k * 1.4, -k * 1.8);
                    float sd = Sheet(p - off, hb);
                    float layer = 1.0 - smoothstep(-0.6, 0.6, sd);
                    float3 pc = _PaperColor.rgb * (0.80 - k * 0.025);
                    pc = lerp(pc, _AgedColor.rgb * 0.6, 1.0 - smoothstep(0.0, 1.2, -sd));
                    c = Over(float4(pc, layer), c);
                }
                c = Over(float4(col, inside), c);

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

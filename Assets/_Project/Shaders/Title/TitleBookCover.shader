// Front of a closed storybook (drawn on BookFlipGraphic while the cover swings open): worn leather board
// with a hinge groove at the spine, a tooled gilt frame with corner ornaments and a medallion in the middle.
// The rect is the closed cover incl. padding; the board matches TitlePaper's cover (same _Pad / _Cover).
// uv.x = 0 at the spine. _Size = rect size in px (UIShaderSize).
Shader "DuskTwilight/UI/TitleBookCover"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Size ("Size (px)", Vector) = (744, 908, 0, 0)
        _Pad ("Padding (px, as the paper)", Float) = 44
        _Cover ("Board margin around the pages (px)", Float) = 16
        _Seed ("Seed", Float) = 5

        _Leather ("Leather", Color) = (0.30, 0.13, 0.08, 1)
        _LeatherDark ("Worn leather", Color) = (0.13, 0.05, 0.03, 1)
        _Gilt ("Gilt", Color) = (0.80, 0.62, 0.36, 1)
        _FrameInset ("Gilt frame inset (px)", Float) = 38
        _Medallion ("Medallion radius (px)", Float) = 70
        _Wear ("Wear / scuffs", Range(0, 1)) = 0.6

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

            fixed4 _Color, _Leather, _LeatherDark, _Gilt;
            float4 _Size;
            float _Pad, _Cover, _Seed, _FrameInset, _Medallion, _Wear;

            float Box(float2 p, float2 b)
            {
                float2 q = abs(p) - b;
                return length(max(q, 0)) + min(max(q.x, q.y), 0);
            }

            fixed4 frag(TitleV2F i) : SV_Target
            {
                float2 size = max(_Size.xy, 1);
                float2 px = i.uv * size;                       // px from the spine-bottom corner
                float inset = max(_Pad - _Cover, 0);
                // board: from the spine (x = 0) to the outer edge, rounded on the outer corners
                float2 bmin = float2(0, inset), bmax = size - inset;
                float2 bc = (bmin + bmax) * 0.5, bh = (bmax - bmin) * 0.5;
                float2 p = px - bc;
                float r = 9.0;
                float2 q = abs(p) - bh + r;
                float d = length(max(q, 0)) + min(max(q.x, q.y), 0) - r;
                d = px.x < r ? max(abs(p.y) - bh.y, -px.x) : d;   // square at the spine
                float board = 1.0 - smoothstep(-0.8, 0.8, d);

                float2 s = px + _Seed * 53.0;
                float grain = TitleNoise(s * 0.35) * 0.5 + TitleFbm(s * 0.03) * 0.5;
                float scuff = smoothstep(0.62, 0.9, TitleFbm(s * 0.012 + 3.3)) * _Wear;
                float3 col = lerp(_LeatherDark.rgb, _Leather.rgb, 0.55 + 0.45 * grain);
                col = lerp(col, _LeatherDark.rgb * 1.4, scuff * 0.5);
                col *= 1.0 - 0.5 * (1.0 - smoothstep(0.0, 14.0, -d));          // worn, darker rim
                // hinge groove a little way from the spine, the spine itself rounder and darker
                col *= 1.0 - 0.45 * exp(-pow((px.x - 26.0) / 3.0, 2.0));
                col *= lerp(0.7, 1.0, smoothstep(0.0, 22.0, px.x));

                // tooled gilt frame + corner ornaments
                float2 fb = bh - _FrameInset;
                float2 fp = p - float2(8, 0);   // frame sits slightly away from the hinge
                float l1 = 1.0 - smoothstep(0.6, 1.6, abs(Box(fp, fb)));
                float l2 = 1.0 - smoothstep(0.4, 1.2, abs(Box(fp, fb - 9.0)));
                float2 cp = abs(fp) - fb;
                float orn = TitleDiamond(cp, 7.0, 0.8);
                orn = max(orn, TitleDiamond((cp - float2(-12, 0)) * float2(0.7, 1.8), 5.0, 0.8));
                orn = max(orn, TitleDiamond((cp - float2(0, -12)) * float2(1.8, 0.7), 5.0, 0.8));
                // medallion: ring, inner ring, a star of diamonds
                float rr = length(fp);
                float med = (1.0 - smoothstep(0.8, 2.0, abs(rr - _Medallion))) + (1.0 - smoothstep(0.5, 1.3, abs(rr - _Medallion * 0.82)));
                float ang = atan2(fp.y, fp.x);
                float petals = TitleDiamond(float2(rr - _Medallion * 0.45, (frac(ang / 6.2831853 * 8.0 + 0.5) - 0.5) * rr * 0.785), _Medallion * 0.22, 0.9);
                med = max(med, petals * step(rr, _Medallion * 0.8));
                med = max(med, TitleDiamond(fp, _Medallion * 0.12, 0.8));
                float gilt = saturate(l1 + l2 * 0.8 + orn + med);
                gilt *= 0.65 + 0.35 * TitleNoise(s * 0.5) * (1.0 - scuff);    // rubbed-off gold
                float3 giltCol = _Gilt.rgb * (0.8 + 0.4 * saturate(0.5 - p.y / bh.y * 0.5));
                col = lerp(col * (1.0 - gilt * 0.35), giltCol, gilt);

                fixed4 c = fixed4(col, board) * _Color * i.color;
                return TitleFinish(c, i);
            }
            ENDCG
        }
    }
}

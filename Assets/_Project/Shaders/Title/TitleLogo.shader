// Title lettering (uGUI Text): gilded vertical gradient, engraved hatch texture, a light sweep that crosses
// the letters now and then, a reveal (left-to-right ink, or a paper burn like the portraits), and optional burnt-paper holes (patches eaten away with a
// charred edge and a thin smouldering ember line; off when Burn amount = 0). _Rect = text rect in local pixels (set by TitleLogo).
Shader "DuskTwilight/UI/TitleLogo"
{
    Properties
    {
        [PerRendererData] _MainTex ("Font Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _TopColor ("Top", Color) = (1, 0.95, 0.8, 1)
        _MidColor ("Middle", Color) = (0.98, 0.74, 0.38, 1)
        _BottomColor ("Bottom", Color) = (0.62, 0.36, 0.22, 1)
        _Hatch ("Engraved hatch", Range(0, 1)) = 0.18

        [Header(Light sweep)]
        _ShineColor ("Shine", Color) = (1, 1, 0.92, 1)
        _ShineWidth ("Shine width (0..1 of rect)", Range(0.01, 0.5)) = 0.08
        _ShinePeriod ("Shine period (s)", Float) = 6
        _ShineStrength ("Shine strength", Range(0, 3)) = 1.2

        [Header(Burnt paper)]
        _BurnAmount ("Burn amount (0 = off)", Range(0, 1)) = 0
        _BurnScale ("Hole size (noise per px)", Range(0.01, 0.3)) = 0.07
        _BurnPatchScale ("Patch size (noise per px)", Range(0.002, 0.1)) = 0.018
        _BurnPatchBias ("Patchiness", Range(0, 1.5)) = 0.7
        _CharColor ("Char", Color) = (0.13, 0.08, 0.06, 1)
        _CharWidth ("Char width", Range(0.005, 0.3)) = 0.06
        _ScorchColor ("Scorch", Color) = (0.5, 0.33, 0.2, 1)
        _ScorchWidth ("Scorch width", Range(0.01, 0.5)) = 0.16
        _EmberColor ("Ember", Color) = (1, 0.5, 0.18, 1)
        _EmberWidth ("Ember width", Range(0.001, 0.1)) = 0.018
        _EmberSpeed ("Ember pulse speed", Range(0, 5)) = 0.8
        _BurnHoleColor ("Hole fill", Color) = (0.42, 0.12, 0.06, 1)
        _BurnHoleOpacity ("Hole opacity (1 = letter never cut)", Range(0, 1)) = 0
        _EmberBoost ("Ember brightness", Range(0.5, 4)) = 1
        _EmberMin ("Ember minimum glow", Range(0, 1)) = 0

        [Header(Reveal)]
        _Reveal ("Reveal", Range(0, 1)) = 1
        _RevealSoft ("Reveal softness", Range(0.01, 0.5)) = 0.18
        [Toggle] _RevealBurn ("Paper-burn reveal (off = ink left to right)", Float) = 0
        _RevealBurnColor ("Burn edge colour", Color) = (1, 0.55, 0.2, 1)
        _RevealBurnWidth ("Burn edge width", Range(0.001, 0.3)) = 0.09
        _RevealNoiseScale ("Burn edge noise (per px)", Range(0.002, 0.1)) = 0.02
        _Rect ("Rect (xMin, xMax, yMin, yMax)", Vector) = (-500, 500, -60, 60)

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

            fixed4 _Color, _TopColor, _MidColor, _BottomColor, _ShineColor;
            float4 _Rect;
            float _Hatch, _ShineWidth, _ShinePeriod, _ShineStrength, _Reveal, _RevealSoft;
            fixed4 _CharColor, _ScorchColor, _EmberColor, _BurnHoleColor;
            float _BurnHoleOpacity;
            float _BurnAmount, _BurnScale, _BurnPatchScale, _BurnPatchBias, _CharWidth, _ScorchWidth, _EmberWidth, _EmberSpeed, _EmberBoost, _EmberMin;
            fixed4 _RevealBurnColor;
            float _RevealBurn, _RevealBurnWidth, _RevealNoiseScale;

            fixed4 frag(TitleV2F i) : SV_Target
            {
                float t = _Time.y;
                float a = (tex2D(_MainTex, i.uv) + _TextureSampleAdd).a;

                float2 r = float2((i.local.x - _Rect.x) / max(_Rect.y - _Rect.x, 1),
                                  (i.local.y - _Rect.z) / max(_Rect.w - _Rect.z, 1));

                // gilded gradient per line (local y), banded like leaf gold
                float y = saturate(r.y);
                fixed3 col = y > 0.5 ? lerp(_MidColor.rgb, _TopColor.rgb, (y - 0.5) * 2.0)
                                     : lerp(_BottomColor.rgb, _MidColor.rgb, y * 2.0);
                float hatch = sin((i.local.x + i.local.y) * 0.9) * 0.5 + 0.5;
                col *= 1.0 - _Hatch * hatch * (1.0 - y);

                // burnt paper: d < 0 is burned away, then outward from the hole ember -> char -> scorch -> clean
                if (_BurnAmount > 0.001)
                {
                    // noise is anchored to the text rect: uGUI batches in root-canvas space, so raw i.local
                    // would slide the holes through the letters as the layer sways and follows the pointer
                    float2 anchored = i.local - float2(_Rect.x, _Rect.z);
                    float patch = TitleNoise(anchored * _BurnPatchScale);
                    float n = TitleFbm(anchored * _BurnScale);
                    float d = n + (patch - 0.5) * _BurnPatchBias - _BurnAmount;
                    col = lerp(_ScorchColor.rgb, col, smoothstep(_CharWidth, _CharWidth + _ScorchWidth, d));
                    col = lerp(_CharColor.rgb, col, smoothstep(_EmberWidth, _CharWidth, d));
                    float pulse = TitleNoise(anchored * _BurnScale * 0.6 + float2(t * _EmberSpeed, -t * _EmberSpeed * 0.5));
                    col = lerp(col, _EmberColor.rgb * _EmberBoost, (1.0 - smoothstep(0.0, _EmberWidth, d)) * lerp(_EmberMin, 1.0, smoothstep(0.3, 0.7, pulse)));
                    float solid = smoothstep(0.0, 0.006, d);
                    col = lerp(_BurnHoleColor.rgb, col, solid);
                    a *= lerp(_BurnHoleOpacity, 1.0, solid);
                }

                // diagonal light sweep
                float phase = frac(t / max(_ShinePeriod, 0.1));
                float sx = lerp(-0.3, 1.3, saturate(phase * 2.5));
                float s = r.x + (r.y - 0.5) * 0.25 - sx;
                float shine = exp(-(s * s) / (_ShineWidth * _ShineWidth)) * _ShineStrength * (phase < 0.4);
                col += _ShineColor.rgb * shine;

                float rev;
                if (_RevealBurn > 0.5)
                {
                    // paper-burn reveal rising from the bottom, same shape as TitlePortrait's
                    float rn = TitleFbm((i.local - float2(_Rect.x, _Rect.z)) * _RevealNoiseScale + 3.1);
                    float edge = _Reveal * 1.08 - (rn * 0.45 + (1.0 - y) * 0.55);
                    float front = (1.0 - smoothstep(0.0, _RevealBurnWidth, edge)) * step(0.0, edge) * (_Reveal < 0.999);
                    col = lerp(col, _RevealBurnColor.rgb * 2.2, front);
                    rev = smoothstep(0.0, 0.015, edge);
                }
                else
                {
                    // ink reveal left to right: the soft front starts fully left of the rect (nothing shown at 0)
                    // and ends fully right of it (everything shown at 1)
                    float front = lerp(-_RevealSoft, 1.0 + _RevealSoft, _Reveal);
                    rev = 1.0 - smoothstep(front - _RevealSoft, front, r.x);
                    rev *= step(0.0001, _Reveal);
                }

                fixed4 c = fixed4(col, a * rev) * _Color * i.color;
                return TitleFinish(c, i);
            }
            ENDCG
        }
    }
}

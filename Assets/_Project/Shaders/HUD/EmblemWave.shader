// The princess's emblem: sun rays that move like a waveform, an eye crowning the ring. Procedural; drawn over the portrait.
Shader "DuskTwilight/UI/EmblemWave"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Rim ("Ring radius (1 = rect edge)", Range(0.2, 0.9)) = 0.48
        _RingWidth ("Ring width", Range(0.002, 0.06)) = 0.018
        _T ("Unscaled time (script)", Float) = 0
        _Activity ("Activity (script): 1 = leader, lower = partner", Range(0, 1)) = 1
        _Energy ("Energy (script): voice level", Range(0, 1)) = 0
        _Flash ("Flash (script): 1 to 0 after a switch", Range(0, 1)) = 0
        _Glint ("Glint (script): 0 to 1 sweep", Range(0, 1)) = 0
        _RingColor ("Ring and rays", Color) = (0.87, 0.70, 0.39, 1)
        _PaleColor ("Ray tips, eye", Color) = (0.97, 0.89, 0.69, 1)
        _GlintColor ("Glint", Color) = (1, 0.96, 0.82, 1)
        [Header(Waveform)]
        _Rays ("Rays (even)", Float) = 24
        _RayWidth ("Ray width (fraction of its slot)", Range(0.1, 0.5)) = 0.4
        _RayBase ("Shortest ray", Range(0, 0.3)) = 0.05
        _RayAmp ("Waveform height", Range(0, 0.5)) = 0.3
        _MinorScale ("Minor ray scale", Range(0, 1)) = 0.55
        _TravelCycles ("Waves around the ring", Float) = 3
        _TravelSpeed ("Wave speed", Float) = 1.6
        _JitterSpeed ("Per-ray flicker speed", Float) = 3.2
        _TravelMix ("Travelling wave vs flicker", Range(0, 1)) = 0.6
        _EnergyAmp ("Extra height from voice", Range(0, 0.5)) = 0.16
        [Header(Eye)]
        _EyeSize ("Eye half-width", Range(0, 0.3)) = 0.13

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

            float _Rays, _RayWidth, _RayBase, _RayAmp, _MinorScale, _TravelCycles, _TravelSpeed, _JitterSpeed, _TravelMix, _EnergyAmp, _EyeSize;

            fixed4 frag(TitleV2F i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2;
                float r = length(p);
                float px = fwidth(r) * 1.2;
                float rim = _Rim;

                // one slot per ray; slots are whole numbers so nothing tears where atan wraps
                float slots = (atan2(p.y, p.x) + 3.14159265) / EMBLEM_TAU * _Rays;
                float id = fmod(floor(slots), _Rays);
                float x = abs(frac(slots) - 0.5);
                float major = fmod(id, 2) < 0.5 ? 1 : _MinorScale;

                float seed = TitleHash(float2(id * 0.137, 3.1));
                float flicker = 0.5 + 0.5 * sin(_T * _JitterSpeed * (0.7 + seed * 0.6) + seed * EMBLEM_TAU);
                float travel = 0.5 + 0.5 * sin(_T * _TravelSpeed - id / _Rays * EMBLEM_TAU * _TravelCycles);
                float level = lerp(flicker, travel, _TravelMix);
                float len = (_RayBase + _RayAmp * level * lerp(0.35, 1, _Activity) + _EnergyAmp * _Energy) * major;
                len = max(len, 0.01);

                float along = (r - rim) / len;
                float edge = _RayWidth * (1 - saturate(along));
                float slotPx = px / max(r * EMBLEM_TAU / _Rays, 0.001);
                float ray = smoothstep(edge + slotPx, edge - slotPx, x) * step(rim - 0.02, r) * step(along, 1);

                fixed3 rayCol = lerp(_RingColor.rgb, _PaleColor.rgb, saturate(along));
                fixed4 c = fixed4(0.1, 0.06, 0.14, EmblemBacking(r, rim) * step(rim, r));
                c = EmblemOver(c, rayCol, ray);

                float ring = smoothstep(_RingWidth + px, _RingWidth - px, abs(r - rim));
                c = EmblemOver(c, _RingColor.rgb, ring);

                // eye crowning the ring
                float2 q = p - float2(0, rim + _EyeSize * 0.2);
                float eyeW = max(_EyeSize, 0.001);
                float nx = min(1, abs(q.x) / eyeW);
                float lid = (1 - nx * nx) * eyeW * 0.62;
                float eye = smoothstep(lid + px, lid - px, abs(q.y)) * step(abs(q.x), eyeW);
                float inner = smoothstep(lid * 0.62 + px, lid * 0.62 - px, abs(q.y)) * step(abs(q.x), eyeW * 0.8);
                float pupil = smoothstep(eyeW * 0.26 + px, eyeW * 0.26 - px, length(q));
                c = EmblemOver(c, _PaleColor.rgb, eye);
                c = EmblemOver(c, fixed3(0.11, 0.07, 0.14), inner);
                c = EmblemOver(c, _RingColor.rgb, pupil);

                // glint, twinkle, switch burst
                float ink = saturate(ring + ray + eye);
                c.rgb = lerp(c.rgb, _GlintColor.rgb, saturate(EmblemGlintBand(p, rim) * ink));
                c = EmblemOver(c, _GlintColor.rgb, saturate(EmblemTwinkle(p, float2(rim, rim) * 0.7071, 0.3)));
                c = EmblemOver(c, _PaleColor.rgb, saturate(EmblemBurst(r, rim)));
                c.rgb = lerp(c.rgb, _PaleColor.rgb, ink * _Flash * 0.6);

                return TitleFinish(c * _Color * i.color, i);
            }
            ENDCG
        }
    }
}

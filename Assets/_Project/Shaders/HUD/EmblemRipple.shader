// The prince's emblem: a crescent cradling the ring and calm ripples spreading outward, like rings on still water. Procedural; drawn over the portrait.
Shader "DuskTwilight/UI/EmblemRipple"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Rim ("Ring radius (1 = rect edge)", Range(0.2, 0.9)) = 0.48
        _RingWidth ("Ring width", Range(0.002, 0.06)) = 0.018
        _T ("Unscaled time (script)", Float) = 0
        _Activity ("Activity (script): 1 = leader, lower = partner", Range(0, 1)) = 1
        _Energy ("Energy (script): echo charge", Range(0, 1)) = 0
        _Flash ("Flash (script): 1 to 0 after a switch", Range(0, 1)) = 0
        _Glint ("Glint (script): 0 to 1 sweep", Range(0, 1)) = 0
        _RingColor ("Ring and crescent", Color) = (0.75, 0.80, 0.87, 1)
        _PaleColor ("Ripples", Color) = (0.86, 0.93, 1, 1)
        _GlintColor ("Glint", Color) = (1, 1, 1, 1)
        [Header(Ripples)]
        _RippleCount ("Ripples across the width", Float) = 2.6
        _RippleSpeed ("Ripple speed", Float) = 0.28
        _RippleThickness ("Ripple thickness", Range(0.05, 0.6)) = 0.22
        _RippleAlpha ("Ripple strength", Range(0, 1)) = 0.8
        _Wobble ("Water wobble", Range(0, 0.05)) = 0.012
        _EnergySpeed ("Extra speed while charging", Float) = 0.9
        [Header(Crescent)]
        _CrescentWidth ("Crescent thickness", Range(0, 0.2)) = 0.075
        _CrescentOffset ("Crescent cut offset", Range(0, 0.3)) = 0.09

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

            float _RippleCount, _RippleSpeed, _RippleThickness, _RippleAlpha, _Wobble, _EnergySpeed, _CrescentWidth, _CrescentOffset;

            fixed4 frag(TitleV2F i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2;
                float r = length(p);
                float px = fwidth(r) * 1.2;
                float rim = _Rim;
                float ang = atan2(p.y, p.x);

                // whole-number lobes, so the wobble has no seam where atan wraps
                float wob = sin(ang * 5 + _T * 0.9) * _Wobble + sin(ang * 9 - _T * 1.3) * _Wobble * 0.6;
                float s = saturate((r + wob - rim) / (1 - rim));
                float phase = s * _RippleCount - _T * (_RippleSpeed + _EnergySpeed * _Energy);
                float w = frac(phase);
                float crest = smoothstep(0, _RippleThickness * 0.5, w) * smoothstep(_RippleThickness, _RippleThickness * 0.5, w);
                float fade = pow(1 - s, 1.6) * step(rim, r);
                float ripple = crest * fade * _RippleAlpha * saturate(lerp(0.4, 1, _Activity) + _Energy * 0.6);

                fixed4 c = fixed4(0.05, 0.07, 0.13, EmblemBacking(r, rim) * step(rim, r));
                c = EmblemOver(c, _PaleColor.rgb, ripple);

                float ring = smoothstep(_RingWidth + px, _RingWidth - px, abs(r - rim));

                float outer = smoothstep(rim + _CrescentWidth + px, rim + _CrescentWidth - px, r);
                float cut = smoothstep(rim + 0.01 - px, rim + 0.01 + px, length(p - float2(_CrescentOffset, 0)));
                float crescent = outer * cut * step(rim - _RingWidth, r);

                c = EmblemOver(c, _RingColor.rgb, ring);
                c = EmblemOver(c, _RingColor.rgb, crescent);

                float ink = saturate(ring + crescent);
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

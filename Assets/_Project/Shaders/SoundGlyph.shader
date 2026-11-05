// How the Dusk Princess "sees" sound: trembling concentric arcs, pixel-snapped.
// Invisible through the prince's senses (he hears the real thing instead).
// Per-instance: _Intensity (0..1, pulsed by script), _GlyphColor.
Shader "DuskTwilight/SoundGlyph"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        _GlyphColor ("Colour", Color) = (0.55,0.95,1.0,1)
        _Intensity ("Intensity", Range(0,1)) = 1
        _Rings ("Ring Count", Float) = 3
        _Speed ("Speed", Float) = 1.2
        _Jitter ("Vibration", Range(0,0.1)) = 0.035
        _Arc ("Arc Coverage", Range(0,1)) = 0.65
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DuskSense.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _GlyphColor;
                float _Intensity, _Rings, _Speed, _Jitter, _Arc;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 centerWS : TEXCOORD1; float radius : TEXCOORD2; float4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.centerWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 edge = TransformObjectToWorld(float3(0.5, 0, 0));
                o.radius = max(distance(edge, o.centerWS), 0.01);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 wp = DT_Snap(i.positionWS.xy);
                float2 p = (wp - i.centerWS.xy) / i.radius;
                float d = length(p);
                float ang = atan2(p.y, p.x);
                // vibration: the radius trembles along the arc
                d += sin(ang * 14.0 + _DT_Now * 28.0) * _Jitter * d;
                float t = frac(d * _Rings - _DT_Now * _Speed);
                float ring = step(0.84, t);
                // broken arcs, rotating slowly
                float seg = frac(ang / 6.2831 * 6.0 + _DT_Now * 0.15);
                float arcs = step(seg, _Arc);
                float fade = (1.0 - smoothstep(0.55, 1.0, d)) * step(0.12, d);
                float light = 1.0 - DT_PrinceAmount(wp);
                float a = ring * arcs * fade * _Intensity * light * i.color.a;
                return half4(_GlyphColor.rgb * i.color.rgb, a);
            }
            ENDHLSL
        }
    }
}

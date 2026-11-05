// The prince's echo, beside him in the world: a thin arc that sweeps clockwise from the top with a soft glow and a hot
// head. Refills during the cooldown, fills gold-ward while charging, and sheds one bright ring the moment it is ready.
// Procedural, fed by EchoCooldownRing.cs. Premultiplied: rgb carries the light, alpha only the solid line, so the glow adds.
Shader "DuskTwilight/FX/EchoCooldownRing"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        _Color ("Glow", Color) = (0.55, 0.78, 1, 1)
        _HotColor ("Head of the arc", Color) = (0.92, 0.98, 1, 1)
        _ChargeColor ("While charging", Color) = (1, 0.82, 0.5, 1)
        _Radius ("Ring radius (1 = quad edge)", Range(0.1, 0.9)) = 0.56
        _Width ("Line half-width", Range(0.005, 0.1)) = 0.028
        _TrackAlpha ("Empty track", Range(0, 1)) = 0.22
        _Intensity ("Line brightness", Range(0, 6)) = 1.8
        _GlowIntensity ("Glow brightness", Range(0, 4)) = 0.8
        _GlowFalloff ("Glow tightness", Range(2, 40)) = 11
        _HeadIntensity ("Head brightness", Range(0, 8)) = 3
        _HeadTightness ("Head tightness", Range(20, 400)) = 120
        _BreathSpeed ("Glow breathing speed", Float) = 7
        _Fill ("Fill (script)", Range(0, 1)) = 0.6
        _Ready ("Ready burst (script): 1 to 0", Range(0, 1)) = 0
        _ChargeMix ("Charging (script)", Range(0, 1)) = 0
        _TickMid ("Medium echo mark (script)", Range(0, 1)) = 0.36
        _Alpha ("Fade (script)", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color, _HotColor, _ChargeColor;
                float _Radius, _Width, _TrackAlpha, _Intensity, _GlowIntensity, _GlowFalloff, _HeadIntensity, _HeadTightness, _BreathSpeed;
                float _Fill, _Ready, _ChargeMix, _TickMid, _Alpha;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            // Distance along the ring between two turns (0..1), the short way round, in the same units as r.
            float ArcDistance(float a, float b, float radius)
            {
                float d = abs(a - b);
                return min(d, 1.0 - d) * TWO_PI * radius;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;
                float r = length(p);
                float px = max(fwidth(r), 1e-4);
                float turn = frac(atan2(p.x, p.y) / TWO_PI);   // 0 at the top, growing clockwise

                float d = abs(r - _Radius);
                float line_ = smoothstep(_Width + px, _Width - px, d);
                float aa = px / max(TWO_PI * r, 1e-3);
                float arc = _Fill >= 0.999 ? 1.0 : smoothstep(0.0, aa * 2.0, _Fill - turn) * step(0.001, _Fill);
                float along = saturate(turn / max(_Fill, 0.001));   // tail 0, head 1

                float3 base = lerp(_Color.rgb, _ChargeColor.rgb, _ChargeMix);
                float3 hot = lerp(_HotColor.rgb, float3(1.0, 0.95, 0.8), _ChargeMix);
                float breath = 0.85 + 0.15 * sin(_Time.y * _BreathSpeed);
                float halo = exp(-d * _GlowFalloff);

                float3 col = base * line_ * _TrackAlpha;
                col += arc * line_ * lerp(base, hot, along * along * along) * _Intensity * lerp(0.7, 1.0, along);
                col += arc * halo * base * _GlowIntensity * lerp(0.3, 1.0, along) * breath;

                float solid = line_ * max(_TrackAlpha, arc);

                // a spark rides the leading edge
                if (_Fill > 0.001 && _Fill < 0.999)
                {
                    float hd = ArcDistance(turn, _Fill, _Radius);
                    col += hot * exp(-(hd * hd + d * d) * _HeadTightness) * _HeadIntensity;
                }

                // while charging, a dot marks where the echo becomes medium
                if (_ChargeMix > 0.001)
                {
                    float md = ArcDistance(turn, _TickMid, _Radius);
                    float dot_ = smoothstep(0.05 + px, 0.05 - px, sqrt(md * md + d * d));
                    col = lerp(col, hot * 1.6, dot_ * _ChargeMix);
                    solid = max(solid, dot_ * _ChargeMix);
                }

                // ready: one ring leaves the line and thins out, a breath of light behind it
                if (_Ready > 0.001)
                {
                    float wave = _Radius + (1.0 - _Ready) * (0.92 - _Radius);
                    col += hot * exp(-abs(r - wave) * 22.0) * _Ready * 2.2;
                    col += base * exp(-r * 2.5) * _Ready * _Ready * 0.7;
                }

                float edge = smoothstep(1.0, 0.82, r);   // nothing reaches the quad's edge
                return half4(col * edge * _Alpha, solid * edge * _Alpha);
            }
            ENDHLSL
        }
    }
}

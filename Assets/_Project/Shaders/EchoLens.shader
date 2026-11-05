// The prince drawing his breath for an echo: a clear convex lens swells around him, magnifying what is behind it.
// Only a faint glassy rim marks its edge, so it still reads against the prince's near-black world.
// Letting go sends one soft refraction ripple outward.
// Full-screen pass (Renderer2D > Echo Lens); on only while needed, fed by EchoChargeLens.cs through globals.
Shader "DuskTwilight/FX/EchoLens"
{
    Properties
    {
        [Header(Lens)]
        _Magnify ("Magnification at the centre (0 = none)", Range(0, 0.8)) = 0.38
        _Aberration ("Colour split near the rim", Range(0, 0.1)) = 0.012
        [Header(Glass rim)]
        _RimColor ("Rim colour", Color) = (0.78, 0.9, 1, 1)
        _RimIntensity ("Rim brightness", Range(0, 2)) = 0.3
        _RimWidth ("Rim width (share of radius)", Range(0.005, 0.3)) = 0.05
        _SheenIntensity ("Upper-left sheen", Range(0, 1)) = 0.12
        [Header(Release ripple)]
        _RippleWidth ("Ripple width (screen heights)", Range(0.005, 0.2)) = 0.035
        _RippleBend ("Ripple bend", Range(0, 3)) = 0.9
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off
        Pass
        {
            Name "EchoLens"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RimColor;
                float _Magnify, _Aberration, _RimIntensity, _RimWidth, _SheenIntensity, _RippleWidth, _RippleBend;
            CBUFFER_END

            // set by EchoChargeLens: xy = centre (uv), z = rim radius (screen heights), w = strength 0..1
            float4 _EchoLensA;
            // x = ripple radius (screen heights), y = ripple amount 0..1, z = y-flip applied to the centre
            float4 _EchoLensB;

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 centre = float2(_EchoLensA.x, lerp(_EchoLensA.y, 1.0 - _EchoLensA.y, _EchoLensB.z));
                float2 aspect = float2(_ScreenParams.x / _ScreenParams.y, 1.0);

                float2 v = (uv - centre) * aspect;
                float r = max(length(v), 1e-5);
                float2 dir = v / r;

                float R = max(_EchoLensA.z, 1e-4);
                float s = _EchoLensA.w;
                float x = r / R;
                float inside = saturate(1.0 - x * x);

                // convex: the middle samples closer to the centre (bigger image), fading to no bend at the rim
                float bent = r * (1.0 - _Magnify * s * inside * inside);

                float rippleBand = _EchoLensB.y > 0.001 ? exp(-pow((r - _EchoLensB.x) / _RippleWidth, 2.0)) : 0.0;
                bent -= rippleBand * _EchoLensB.y * _RippleWidth * _RippleBend * sign(r - _EchoLensB.x);

                float split = _Aberration * s * x * inside * 4.0;
                float2 toUv = dir / aspect;
                float3 col;
                col.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, centre + toUv * (bent + split * R)).r;
                col.g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, centre + toUv * bent).g;
                col.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, centre + toUv * (bent - split * R)).b;

                // thin soft edge, brighter toward the upper left like a lit glass bead
                float rim = exp(-pow((x - 1.0) / _RimWidth, 2.0));
                float lit = 0.55 + 0.45 * dot(dir, normalize(float2(-1.0, 1.0)));
                col += _RimColor.rgb * rim * lit * _RimIntensity * s;
                float sheen = smoothstep(1.0, 0.55, x) * smoothstep(0.2, 0.85, x) * saturate(dot(dir, normalize(float2(-1.0, 1.0))));
                col += _RimColor.rgb * sheen * sheen * _SheenIntensity * s;

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}

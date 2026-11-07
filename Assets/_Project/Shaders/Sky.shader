// Full-screen backdrop that follows the camera.
//  Light: dithered dusk gradient, banded low sun, drifting pink clouds, first stars.
//  Dark : blue-black void; only sound wavefronts ripple through the air.
Shader "DuskTwilight/Sky"
{
    Properties
    {
        _Top ("Top", Color) = (0.20,0.14,0.36,1)
        _Upper ("Upper", Color) = (0.52,0.28,0.50,1)
        _Mid ("Mid", Color) = (0.96,0.52,0.44,1)
        _Horizon ("Horizon", Color) = (1.0,0.80,0.50,1)
        _SunColor ("Sun", Color) = (1.0,0.94,0.70,1)
        _CloudLight ("Cloud Light", Color) = (1.0,0.74,0.66,1)
        _CloudShade ("Cloud Shade", Color) = (0.78,0.44,0.54,1)
        _HorizonY ("Horizon (viewport y)", Range(0,1)) = 0.32
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
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
                float4 _Top, _Upper, _Mid, _Horizon, _SunColor, _CloudLight, _CloudShade;
                float _HorizonY;
            CBUFFER_END
            float4 _DT_Cam; // xy = camera world pos

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float4 screenPos : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }

            static const float bayer4[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

            float Bayer(float2 px)
            {
                int2 p = int2(fmod(abs(px), 4.0));
                return bayer4[p.y * 4 + p.x] / 16.0;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 wp = DT_Snap(i.positionWS.xy);
                float2 px = floor(i.positionWS.xy * DT_PPU);
                float2 suv = i.screenPos.xy / i.screenPos.w;
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float prince = DT_PrinceAmount(wp);

                // ---------------- LIGHT --------------------------------------------------------
                // dithered, banded gradient (pixel-art sky)
                float v = saturate((suv.y - _HorizonY * 0.6) / (1.0 - _HorizonY * 0.6));
                float steps = 10.0;
                float vq = floor(v * steps + Bayer(px)) / steps;
                float3 sky = lerp(_Horizon.rgb, _Mid.rgb, smoothstep(0.0, 0.35, vq));
                sky = lerp(sky, _Upper.rgb, smoothstep(0.35, 0.7, vq));
                sky = lerp(sky, _Top.rgb, smoothstep(0.7, 1.0, vq));

                // low banded sun
                float2 sp = (suv - _DT_SunScreen.xy) * float2(aspect, 1.0);
                float sd = length(sp);
                float sunR = 0.13;
                float stripeCut = 1.0 - step(0.0, -sp.y) * step(frac(-sp.y * 30.0), saturate(-sp.y / sunR) * 0.75);
                float sun = step(sd, sunR) * stripeCut;
                float halo = pow(saturate(1.0 - sd * 1.6), 3.0);
                sky = lerp(sky, _SunColor.rgb, sun);
                sky += _SunColor.rgb * halo * 0.28;

                // clouds (parallax-scrolled fbm, two tones, pixel-snapped)
                float2 cp = (wp - _DT_Cam.xy) * float2(0.09, 0.22) + _DT_Cam.xy * float2(0.012, 0.0) + float2(_DT_Now * 0.012, 0);
                float band = smoothstep(0.42, 0.55, suv.y) * (1.0 - smoothstep(0.82, 0.95, suv.y));
                float n = DT_Fbm(cp * 2.2);
                float cloud = step(0.56, n) * band;
                float cloudShade = step(0.56, n) * (1.0 - step(0.6, DT_Fbm(cp * 2.2 + float2(0.0, 0.08)))) * band;
                float3 cl = lerp(_CloudLight.rgb, _CloudShade.rgb, cloudShade);
                // clouds catch the sun on the near side
                cl += _SunColor.rgb * halo * 0.4;
                sky = lerp(sky, cl, cloud * (1.0 - sun));

                // first stars of dusk at the top
                float starMask = smoothstep(0.72, 0.98, suv.y);
                float2 sc = floor((wp - _DT_Cam.xy * 0.9) * DT_PPU / 2.0);
                float star = step(0.992, DT_Hash(sc)) * starMask * (0.6 + 0.4 * sin(_DT_Now * 2.0 + DT_Hash(sc + 3.1) * 6.28));
                sky += star * 0.8;

                // ---------------- DARK ---------------------------------------------------------
                float3 echo3 = DT_Echo3(wp);
                float2 echo = echo3.xy;
                float3 night = DT_NIGHT * lerp(0.7, 1.3, 1.0 - suv.y);
                // dust motes in the air shimmer when sound passes through them
                float mote = step(0.985, DT_Hash(floor(wp * DT_PPU / 2.0)));
                night += DT_ECHO_LINE * (echo.y * 0.4 + mote * (echo.x * 0.5 + echo.y * 1.2));
                night += DT_VOICE_LINE * echo3.z * (0.45 + mote * 1.2);

                float3 col = lerp(sky, night, prince);
                col += DT_RIM_GOLD * DT_SwitchRim(wp) * 0.9;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}

// One shader for every world sprite / tilemap. Same geometry, two senses.
//  Light (Dusk Princess's eyes): warm dusk grade, sun-side glow, atmospheric haze by depth.
//  Dark  (Twilight Prince's ears): near-black; outlines appear only where sound has travelled.
Shader "DuskTwilight/SenseSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Light sense)]
        _LightAlpha ("Light Alpha", Range(0,1)) = 1
        _Depth ("Haze Depth", Range(0,1)) = 0
        _Glint ("Glass Glint", Range(0,1)) = 0
        _Warmth ("Warm Grade", Range(0,1)) = 1

        [Header(Dark sense)]
        _DarkAlpha ("Dark Alpha", Range(0,1)) = 1
        _EdgeBoost ("Echo Edge Boost", Range(0,3)) = 1
        _SelfLit ("Self Outline (actors)", Range(0,1)) = 0
        _EchoTint ("Echo Tint", Color) = (0.82,0.92,1,1)
        _AlphaEdges ("Use Alpha Edges", Range(0,1)) = 0

        [Header(2.5D layer (environment art))]
        _WindAmp ("Wind Sway (world units)", Range(0,1)) = 0
        _WindSpeed ("Wind Speed", Range(0,6)) = 1.3
        _WindAnchor ("Wind Anchor (0 = rooted bottom, 1 = hanging top)", Range(0,1)) = 0
        _Blur ("Depth Blur (texels)", Range(0,12)) = 0
        _Shade ("Foreground Shade", Range(0,1)) = 0
        _ShadeColor ("Shade Color", Color) = (0.13,0.08,0.16,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DuskSense.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _LightAlpha, _Depth, _Glint, _Warmth;
                float _DarkAlpha, _EdgeBoost, _SelfLit, _AlphaEdges;
                float4 _EchoTint;
                float _WindAmp, _WindSpeed, _WindAnchor, _Blur, _Shade;
                float4 _ShadeColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float4 screenPos : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                if (_WindAmp > 0)
                {
                    // sway grows away from the anchored edge; phase varies along x so a strip ripples
                    float bend = lerp(v.uv.y, 1.0 - v.uv.y, _WindAnchor);
                    bend *= bend;
                    float t = _DT_Now * _WindSpeed + o.positionWS.x * 0.35;
                    float gust = sin(t) * 0.7 + sin(t * 2.3 + 1.7) * 0.3;
                    o.positionWS.x += gust * _WindAmp * bend;
                }
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                o.color = v.color * _Color;
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }

            float AlphaEdge(float2 uv)
            {
                float2 t = _MainTex_TexelSize.xy;
                float a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
                float n = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(t.x, 0)).a
                        * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(t.x, 0)).a
                        * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, t.y)).a
                        * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(0, t.y)).a;
                return a * (1.0 - step(0.5, n));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                if (_Blur > 0)
                {
                    // cheap depth-of-field for near/far layers: 8-tap ring + centre
                    float2 r = _MainTex_TexelSize.xy * _Blur;
                    float4 acc = tex;
                    [unroll] for (int k = 0; k < 8; k++)
                    {
                        float ang = k * 0.785398;
                        acc += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(cos(ang), sin(ang)) * r);
                    }
                    tex = acc / 9.0;
                }
                tex *= i.color;
                if (tex.a < 0.01) discard;
                // near-camera layers sink toward a dark silhouette (rim of light kept on bright texels)
                tex.rgb = lerp(tex.rgb, _ShadeColor.rgb * (0.6 + 0.8 * DT_Luma(tex.rgb)), _Shade);

                float2 wp = DT_Snap(i.positionWS.xy);
                float2 suv = i.screenPos.xy / i.screenPos.w;
                float prince = DT_PrinceAmount(wp);
                float luma = DT_Luma(tex.rgb);

                // ---------------- LIGHT: the princess sees everything, bathed in dusk --------------
                float3 lightCol = tex.rgb;
                // warm grade: lift reds, settle blues toward violet
                float3 graded = lightCol * float3(1.07, 0.97, 0.86) + float3(0.03, 0.0, 0.035);
                lightCol = lerp(lightCol, graded, _Warmth);
                // sun-side glow (sun sits low on the horizon, screen-space)
                float2 toSun = (suv - _DT_SunScreen.xy) * float2(_ScreenParams.x / _ScreenParams.y, 1.0);
                float sunGlow = pow(saturate(1.0 - length(toSun) * 0.9), 2.5) * _DT_SunScreen.z;
                lightCol += DT_RIM_GOLD * sunGlow * 0.35 * (0.4 + luma);
                // low world = cooler violet shadow
                float low = saturate(1.0 - (wp.y + 1.5) / 7.0);
                lightCol = lerp(lightCol, lightCol * float3(0.78, 0.66, 0.92), low * 0.45 * _Warmth);
                // atmospheric depth
                lightCol = lerp(lightCol, _DT_HazeColor.rgb, _Depth);
                // glass glint: echo stones are invisible to the eye — only a rare stray pixel of
                // sunlight catches on them (never the silhouette), so the princess cannot read the path
                float glint = 0;
                if (_Glint > 0)
                {
                    glint = step(0.996, DT_Hash(floor(wp * DT_PPU) + floor(_DT_Now * 3))) * _Glint;
                    lightCol = lerp(lightCol, float3(1.0, 0.97, 0.88), glint);
                }
                float lightA = tex.a * saturate(_LightAlpha + glint * 0.45);

                // ---------------- DARK: the prince hears the world into being -----------------------
                float2 echo = DT_Echo(wp);
                float reveal = echo.x;
                float front = echo.y;
                // Kenney art has dark outlines: invert them into bright contour lines
                float contour = saturate((0.5 - luma) * 3.0);
                if (_AlphaEdges > 0.5) contour = max(contour, AlphaEdge(i.uv));
                float texture_ = luma * 0.32 * saturate(_EdgeBoost);
                float lit = reveal * 0.85 + front * 1.4;
                float3 darkCol = DT_NIGHT * (0.6 + 0.4 * luma);
                darkCol += _EchoTint.rgb * (contour * _EdgeBoost * lit + texture_ * lit * 0.6);
                darkCol += _EchoTint.rgb * front * 0.10;
                // actors stay recognisable: moonlit, desaturated self + bright outline
                float3 moon = lerp(luma.xxx, tex.rgb, 0.35) * float3(0.42, 0.52, 0.85) + DT_ECHO_LINE * AlphaEdge(i.uv) * 0.9;
                darkCol = lerp(darkCol, moon, _SelfLit);
                float darkA = tex.a * _DarkAlpha;
                // objects only sound can reveal: alpha follows the echo
                if (_LightAlpha < 0.05)
                    darkA = tex.a * _DarkAlpha * saturate(lit * 1.3 + _SelfLit);

                float3 col = lerp(lightCol, darkCol, prince);
                float a = lerp(lightA, darkA, prince);

                // switch wavefront rim — the signature "world flips" moment
                float rim = DT_SwitchRim(wp);
                col += DT_RIM_GOLD * rim * 1.2 * tex.a;

                return half4(col, a);
            }
            ENDHLSL
        }
    }
    Fallback "Sprites/Default"
}

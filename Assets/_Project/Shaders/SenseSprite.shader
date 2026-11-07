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
        _DarkEchoOnly ("Dark: visible only where sound reaches (fake walls)", Range(0,1)) = 0
        _EchoTint ("Echo Tint", Color) = (0.82,0.92,1,1)
        _AlphaEdges ("Use Alpha Edges", Range(0,1)) = 0
        _EchoFill ("Echo Fill (shape shows faintly where sound passed)", Range(0,1)) = 0.6
        _InkThreshold ("Ink Threshold (darker than this = contour; illustrations ~0.18, Kenney 0.5)", Range(0,1)) = 0.5

        [Header(2.5D layer (environment art))]
        _WindAmp ("Wind Sway (world units)", Range(0,1)) = 0
        _WindSpeed ("Wind Speed", Range(0,6)) = 1.3
        _WindAnchor ("Wind Anchor (0 = rooted bottom, 1 = hanging top)", Range(0,1)) = 0
        _Blur ("Depth Blur (texels)", Range(0,12)) = 0
        _Shade ("Foreground Shade", Range(0,1)) = 0
        _ShadeColor ("Shade Color", Color) = (0.13,0.08,0.16,1)

        [Header(World texture (illustrated tilemap fill))]
        [NoScaleOffset] _WorldTex ("World Texture (Repeat wrap)", 2D) = "white" {}
        _WorldTile ("World Tile Size (units, 0 = off)", Range(0,32)) = 0

        [Header(2D Lights (light sense only))]
        _LightReceive ("Receive 2D Lights", Range(0,1)) = 1

        [Header(Echo glint (hidden planks, a wiped glass sweep))]
        [HDR] _GlintColor ("Glint Colour", Color) = (1,1,1,1)
        _GlintWidth ("Glint Wide Band (m)", Range(0.02,1.5)) = 0.34
        _GlintThinWidth ("Glint Thin Band (m)", Range(0.01,0.6)) = 0.09
        _GlintGap ("Glint Band Gap (m)", Range(0,0.8)) = 0.11
        _GlintSlant ("Glint Slant (degrees from upright)", Range(-70,70)) = 28
        _GlintSoftness ("Glint Edge Softness (m)", Range(0.001,0.1)) = 0.012
        _GlintSeconds ("Glint Sweep Seconds", Range(0.05,2)) = 0.4
        [HDR] _GlintOutlineColor ("Outline Colour", Color) = (0.86,0.95,1.15,1)
        _GlintOutlineWidth ("Outline Width (m)", Range(0.005,0.2)) = 0.045
        _GlintOutlineSeconds ("Outline Seconds", Range(0,10)) = 1.25
        _GlintOutlineFade ("Outline Fade-out Seconds", Range(0.01,3)) = 0.5
        [HideInInspector] _GlintOutline ("Outline Strength (script)", Range(0,1)) = 0
        _GlintRememberAlpha ("Found: Dashed Outline Strength", Range(0,1)) = 0.32
        _GlintDashMetres ("Found: Dash Length (m)", Range(0.05,1)) = 0.22
        [HideInInspector] _GlintRemember ("Found (script)", Range(0,1)) = 0
        [HideInInspector] _GlintProgress ("Glint Progress (script)", Range(0,1)) = 0
        [HideInInspector] _GlintDir ("Glint Direction (script)", Float) = 1
        [HideInInspector] _GlintBounds ("Glint Bounds: xMin xMax yCentre halfHeight (script)", Vector) = (0,0,0,0)
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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightVariables.hlsl"

            // URP 2D light textures (Light2D components) — same math as Sprite-Lit-Default, without mask/normal maps.
            // With no Light2D in the scene no USE_SHAPE_LIGHT_TYPE_* is on and colour passes through unchanged.
            half3 DT_Apply2DLights(half3 col, float2 screenUV)
            {
            #if USE_SHAPE_LIGHT_TYPE_0 || USE_SHAPE_LIGHT_TYPE_1 || USE_SHAPE_LIGHT_TYPE_2 || USE_SHAPE_LIGHT_TYPE_3
                half4 modulate = 0, additive = 0;
                #if USE_SHAPE_LIGHT_TYPE_0
                    half4 l0 = SAMPLE_TEXTURE2D(_ShapeLightTexture0, sampler_ShapeLightTexture0, screenUV);
                    modulate += l0 * _ShapeLightBlendFactors0.x; additive += l0 * _ShapeLightBlendFactors0.y;
                #endif
                #if USE_SHAPE_LIGHT_TYPE_1
                    half4 l1 = SAMPLE_TEXTURE2D(_ShapeLightTexture1, sampler_ShapeLightTexture1, screenUV);
                    modulate += l1 * _ShapeLightBlendFactors1.x; additive += l1 * _ShapeLightBlendFactors1.y;
                #endif
                #if USE_SHAPE_LIGHT_TYPE_2
                    half4 l2 = SAMPLE_TEXTURE2D(_ShapeLightTexture2, sampler_ShapeLightTexture2, screenUV);
                    modulate += l2 * _ShapeLightBlendFactors2.x; additive += l2 * _ShapeLightBlendFactors2.y;
                #endif
                #if USE_SHAPE_LIGHT_TYPE_3
                    half4 l3 = SAMPLE_TEXTURE2D(_ShapeLightTexture3, sampler_ShapeLightTexture3, screenUV);
                    modulate += l3 * _ShapeLightBlendFactors3.x; additive += l3 * _ShapeLightBlendFactors3.y;
                #endif
                return max(0, _HDREmulationScale * (col * modulate.rgb + additive.rgb));
            #else
                return col;
            #endif
            }

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_WorldTex); SAMPLER(sampler_WorldTex);
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _LightAlpha, _Depth, _Glint, _Warmth;
                float _DarkAlpha, _EdgeBoost, _SelfLit, _AlphaEdges;
                float4 _EchoTint;
                float _WindAmp, _WindSpeed, _WindAnchor, _Blur, _Shade;
                float4 _ShadeColor;
                float _WorldTile;
                float _LightReceive;
                float _InkThreshold;
                float _EchoFill;
                float _DarkEchoOnly;
                float4 _GlintColor, _GlintBounds;
                float _GlintWidth, _GlintThinWidth, _GlintGap, _GlintSlant, _GlintSoftness, _GlintSeconds, _GlintProgress, _GlintDir;
                float4 _GlintOutlineColor;
                float _GlintOutlineWidth, _GlintOutlineSeconds, _GlintOutlineFade, _GlintOutline;
                float _GlintRememberAlpha, _GlintDashMetres, _GlintRemember;
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
                if (_WorldTile > 0)
                {
                    // the sprite/tile only supplies the shape; colour comes from one seamless world-space texture
                    tex.rgb = SAMPLE_TEXTURE2D(_WorldTex, sampler_WorldTex, i.positionWS.xy / _WorldTile).rgb;
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
                // scene lighting (Light2D): global ambient + lamps/shafts. Far layers can take less of it.
                lightCol = lerp(lightCol, DT_Apply2DLights(lightCol, suv), _LightReceive);
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
                float3 echo3 = DT_Echo3(wp);
                float reveal = echo3.x;
                float voiceFront = echo3.z;
                float front = max(echo3.y, voiceFront);
                // Kenney art has dark outlines: invert them into bright contour lines
                // dark ink becomes a bright contour line. Pixel art only has dark outlines (0.5 works);
                // hatched illustrations are dark all over, so they use a low threshold: only the heaviest ink lights up
                float contour = saturate((_InkThreshold - luma) * 1.5 / max(_InkThreshold, 0.05)); // = old (0.5 - luma) * 3 at 0.5
                if (_AlphaEdges > 0.5) contour = max(contour, AlphaEdge(i.uv));
                float texture_ = luma * 0.32 * saturate(_EdgeBoost);
                float lit = reveal * 0.85 + front * 1.4;
                // switching: near the torn edge the dark side is scratched in as white line art
                lit = max(lit, DT_SwitchScratch(wp) * 1.1);
                float3 darkCol = DT_NIGHT * (0.6 + 0.4 * luma);
                darkCol += _EchoTint.rgb * (contour * _EdgeBoost * lit + texture_ * lit * 0.6);
                // illustrated art has few pure-ink lines: let the whole shape show faintly where the sound passed
                darkCol += _EchoTint.rgb * (0.14 + luma * 0.6) * saturate(lit * 1.5) * _EchoFill;
                darkCol += _EchoTint.rgb * front * 0.10;
                // where the player's voice is passing, the lines glow gold instead of moonlight
                float voiceShare = saturate(voiceFront * 1.6 - echo3.y);
                darkCol = lerp(darkCol, DT_Luma(darkCol) * DT_VOICE_LINE * 2.4 + DT_VOICE_LINE * voiceFront * 0.08, voiceShare);
                // actors stay recognisable: moonlit, desaturated self + bright outline
                float3 moon = lerp(luma.xxx, tex.rgb, 0.35) * float3(0.42, 0.52, 0.85) + DT_ECHO_LINE * AlphaEdge(i.uv) * 0.9;
                darkCol = lerp(darkCol, moon, _SelfLit);
                float darkA = tex.a * _DarkAlpha;
                // objects only sound can reveal: alpha follows the echo
                if (_LightAlpha < 0.05 || _DarkEchoOnly > 0.5)
                    darkA = tex.a * _DarkAlpha * saturate(lit * 1.3 + _SelfLit);

                float3 col = lerp(lightCol, darkCol, prince);
                float a = lerp(lightA, darkA, prince);

                // switch wavefront rim — the signature "world flips" moment
                float rim = DT_SwitchRim(wp);
                col += DT_RIM_GOLD * rim * 1.8 * tex.a;

                // after an echo the plank's outline stays for a moment: the sprite's alpha edge, plus its bounds for
                // art that fills the whole rect. Width is in metres, so short and long planks match.
                // a plank the prince has already found keeps a faint dashed outline: it is still there, just unseen
                float remembered = 0.0;
                if (_GlintRemember > 0.0)
                {
                    float dash = step(0.5, frac((i.positionWS.x + i.positionWS.y) / max(_GlintDashMetres, 0.01)));
                    remembered = dash * _GlintRememberAlpha * _GlintRemember;
                }
                float outlineK = max(_GlintOutline, remembered);
                if (outlineK > 0.0)
                {
                    float2 uvPerMetre = (abs(ddx(i.uv)) + abs(ddy(i.uv))) / max(abs(ddx(i.positionWS.xy)) + abs(ddy(i.positionWS.xy)), 1e-5);
                    float2 step_ = uvPerMetre * _GlintOutlineWidth;
                    float around = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(step_.x, 0)).a
                                 * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv - float2(step_.x, 0)).a
                                 * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(0, step_.y)).a
                                 * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv - float2(0, step_.y)).a;
                    float toEdge = min(min(i.positionWS.x - _GlintBounds.x, _GlintBounds.y - i.positionWS.x), _GlintBounds.w - abs(i.positionWS.y - _GlintBounds.z));
                    float edgeBand = smoothstep(_GlintOutlineWidth + 0.006, _GlintOutlineWidth - 0.006, toEdge);
                    float outline = max(1.0 - around, edgeBand) * tex.a * prince * _GlintOutlineColor.a * outlineK;
                    float litA = max(a, outline);
                    col = (col * a + _GlintOutlineColor.rgb * outline) / max(litA, 0.001);
                    a = litA;
                }

                // echo glint: a wide and a thin slanted band wipe across once, like light on cleaned glass.
                // In metres so a long plank gets the same bands as a short one; only the sprite's own shape shows it.
                if (_GlintProgress > 0.0 && _GlintProgress < 1.0)
                {
                    float slope = tan(radians(_GlintSlant));
                    float slanted = i.positionWS.x - (i.positionWS.y - _GlintBounds.z) * slope;   // positive slant leans like "/"
                    float halfWide = _GlintWidth * 0.5;
                    float margin = halfWide + _GlintGap + _GlintThinWidth + abs(slope) * _GlintBounds.w + _GlintSoftness;
                    float from = _GlintDir > 0 ? _GlintBounds.x - margin : _GlintBounds.y + margin;
                    float to = _GlintDir > 0 ? _GlintBounds.y + margin : _GlintBounds.x - margin;
                    float d = (slanted - lerp(from, to, _GlintProgress)) * _GlintDir;   // + = ahead of the wide band
                    float wide = smoothstep(halfWide + _GlintSoftness, halfWide - _GlintSoftness, abs(d));
                    float thinCentre = -(halfWide + _GlintGap + _GlintThinWidth * 0.5);
                    float thin = smoothstep(_GlintThinWidth * 0.5 + _GlintSoftness, _GlintThinWidth * 0.5 - _GlintSoftness, abs(d - thinCentre));
                    float sweep = max(wide, thin) * tex.a * prince * _GlintColor.a;
                    // added light, even over a plank that is still invisible (a = 0)
                    float lifted = max(a, sweep);
                    col = (col * a + _GlintColor.rgb * sweep) / max(lifted, 0.001);
                    a = lifted;
                }

                return half4(col, a);
            }
            ENDHLSL
        }
    }
    Fallback "Sprites/Default"
}

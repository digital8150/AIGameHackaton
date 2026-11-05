// Storybook page turn between stages. _MainTex is a capture of the last frame of the old stage; the page curls
// up around a cylinder from one corner and peels away, revealing the new stage underneath (this overlay is
// transparent where the page has gone). The back of the page is plain paper with fibres and a faint ghost of
// the picture showing through.
Shader "DuskTwilight/UI/PageTurn"
{
    Properties
    {
        [PerRendererData] _MainTex ("Captured page", 2D) = "white" {}
        _Progress ("Progress", Range(0, 1)) = 0
        _Angle ("Fold angle (deg, 0 = vertical fold moving left)", Range(-60, 60)) = -18
        _Radius ("Curl radius (share of screen height)", Range(0.02, 0.4)) = 0.11
        _PaperColor ("Paper (back of the page)", Color) = (0.93, 0.85, 0.68, 1)
        _PaperInk ("Paper fibres / hatching", Color) = (0.55, 0.42, 0.28, 1)
        _ShowThrough ("Picture showing through the paper", Range(0, 0.5)) = 0.12
        _Shadow ("Shadow strength", Range(0, 1)) = 0.55
        [Header(Worn storybook page)]
        _Age ("Worn-book look (animated 0 → 1 before the turn)", Range(0, 1)) = 0
        _InkDark ("Sepia ink (shadows)", Color) = (0.17, 0.11, 0.07, 1)
        _InkLight ("Old paper (highlights)", Color) = (0.86, 0.76, 0.58, 1)
        _KeepColor ("Original colour kept under the sepia", Range(0, 1)) = 0.18
        _Wear ("Burnt / frayed edges and stains", Range(0, 1)) = 0.7
        _Gutter ("Spine shadow down the middle", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "False" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            sampler2D _MainTex;
            float _Progress, _Angle, _Radius, _ShowThrough, _Shadow;
            float4 _PaperColor, _PaperInk, _InkDark, _InkLight;
            float _Age, _KeepColor, _Wear, _Gutter;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            float Fbm(float2 p) { return Noise(p) * 0.5 + Noise(p * 2.03) * 0.3 + Noise(p * 4.1) * 0.2; }

            // aged, stained paper and frayed, darkened edges — shared by both sides of the page
            float3 Age(float3 col, float2 uv, float aspect)
            {
                float2 p = uv * float2(aspect, 1);
                // blotchy stains and foxing
                float stain = smoothstep(0.55, 0.85, Fbm(p * 3.0 + 7.3));
                float fox = smoothstep(0.93, 1.0, Noise(p * 38.0)) * Noise(p * 5.0);
                col *= 1 - _Wear * (0.16 * stain + 0.35 * fox);
                // edges: vignette eroded by noise so the border looks frayed and burnt
                float2 e = min(uv, 1 - uv) * float2(aspect, 1);
                float edge = min(e.x, e.y) + (Fbm(p * 9.0) - 0.5) * 0.05;
                float burn = 1 - smoothstep(0.0, 0.16, edge);
                col = lerp(col, _InkDark.rgb * 0.8, _Wear * burn * 0.85);
                col *= 1 - _Wear * 0.25 * (1 - smoothstep(0.0, 0.45, edge));   // broad vignette
                // fine scratches across the page
                float scr = smoothstep(0.985, 1.0, Noise(float2(p.x * 1.5 + p.y * 0.4, p.y * 400.0)));
                col = lerp(col, _InkLight.rgb * 1.08, _Wear * scr * 0.35);
                return col;
            }

            // the captured stage turned into a picture-book page: sepia ink on old paper, spine in the middle
            float3 Worn(float3 pic, float2 uv, float aspect)
            {
                float2 p = uv * float2(aspect, 1);
                float l = dot(pic, float3(0.3, 0.59, 0.11));
                l = saturate((l - 0.04) * 1.25);
                float grain = Fbm(p * float2(30, 160)) - 0.5;
                float3 sep = lerp(_InkDark.rgb, _InkLight.rgb, saturate(pow(l, 1.15) + grain * 0.08));
                sep = lerp(sep, saturate(sep * (pic / max(l, 0.05))), _KeepColor * 0.5);
                sep = Age(sep, uv, aspect);
                // spine: soft shadow both sides of the middle and a dark crease
                float g = abs(uv.x - 0.5) * aspect;
                sep *= 1 - _Gutter * (0.45 * exp(-g * 26.0) + 0.5 * exp(-g * 400.0));
                return lerp(pic, sep, _Age);
            }

            float3 Front(float2 uv, float aspect) { return Worn(tex2D(_MainTex, uv).rgb, uv, aspect); }

            // back of the page: warm paper, long fibres and a little cross-hatching, the picture faintly through it
            float3 Paper(float2 uv, float aspect)
            {
                float2 p = uv * float2(aspect, 1);
                float fib = Noise(p * float2(6, 140)) * 0.6 + Noise(p * float2(3, 60)) * 0.4;
                float hatch = smoothstep(0.82, 1.0, Noise(float2(p.x * 2.0 + p.y * 9.0, p.y * 220.0)));
                float3 col = _PaperColor.rgb * (0.9 + 0.12 * fib);
                col = lerp(col, _PaperInk.rgb, hatch * 0.18);
                float3 pic = tex2D(_MainTex, float2(1 - uv.x, uv.y)).rgb;   // seen from behind: mirrored
                float l = dot(pic, float3(0.3, 0.59, 0.11));
                col = lerp(col, col * (0.55 + 0.6 * l), _ShowThrough);
                return lerp(col, Age(col * 0.94, uv, aspect), _Age);
            }

            bool Inside(float2 uv) { return all(uv >= 0) && all(uv <= 1); }

            float4 frag (v2f i) : SV_Target
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 q = i.uv * float2(aspect, 1);                  // square units: 1 = screen height
                float a = radians(_Angle);
                float2 n = float2(cos(a), sin(a));                    // points to where the page is lifted from
                float R = _Radius;

                // fold line travels from beyond the far corner to past the near corner (page fully gone)
                float d0 = dot(float2(0, 0), n), d1 = dot(float2(aspect, 0), n), d2 = dot(float2(0, 1), n), d3 = dot(float2(aspect, 1), n);
                float sMax = max(max(d0, d1), max(d2, d3)), sMin = min(min(d0, d1), min(d2, d3));
                float t = smoothstep(0, 1, _Progress);
                float s = lerp(sMax + 0.01, sMin - R - 0.03, t);
                float d = dot(q, n) - s;                              // > 0: the part being lifted

                float4 col = float4(0, 0, 0, 0);
                // 1) upper layer of the curl (back of the page) and the page lying flat behind it
                float arcUp = -1;
                if (d >= 0 && d <= R) arcUp = R * (UNITY_PI - asin(d / R));
                else if (d < 0) arcUp = UNITY_PI * R - d;
                if (arcUp >= 0)
                {
                    float2 uv0 = (q + (arcUp - d) * n) / float2(aspect, 1);
                    if (Inside(uv0))
                    {
                        float3 paper = Paper(uv0, aspect);
                        float shade = d >= 0 ? lerp(1.0, 0.72, 1 - sqrt(saturate(1 - (d / R) * (d / R)))) : 1.0;
                        // soft highlight along the top of the roll
                        shade += d >= 0 ? 0.12 * smoothstep(R, 0.3 * R, d) * smoothstep(0, 0.4 * R, d) : 0;
                        // the flat back gets darker towards the roll (it is in the curl's shade)
                        shade *= d < 0 ? lerp(0.86, 1.0, saturate(-d / (R * 2))) : 1.0;
                        return float4(paper * shade, 1);
                    }
                }
                // 2) lower layer of the curl (front of the page, turning away)
                if (d >= 0 && d <= R)
                {
                    float arc = R * asin(d / R);
                    float2 uv0 = (q + (arc - d) * n) / float2(aspect, 1);
                    if (Inside(uv0))
                    {
                        float3 pic = Front(uv0, aspect);
                        float c = sqrt(saturate(1 - (d / R) * (d / R)));
                        return float4(pic * lerp(0.45, 1.0, c), 1);
                    }
                }
                // 3) the page still lying flat (front)
                if (d < 0)
                {
                    float3 pic = Front(i.uv, aspect);
                    // shadow of the curl falling on the flat page just before the fold
                    float sh = _Shadow * 0.5 * smoothstep(-R * 1.2, 0, d);
                    return float4(pic * (1 - sh), 1);
                }
                // 4) revealed new page: only the curl's shadow
                float shadow = _Shadow * smoothstep(R + R * 1.4, R, d) * step(R, d);
                return float4(0, 0, 0, shadow);
            }
            ENDCG
        }
    }
}

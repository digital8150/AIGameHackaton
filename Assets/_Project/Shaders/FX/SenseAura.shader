// Additive soft sprite / particle that only exists where the prince can perceive the world: in his dark sense its
// alpha follows the same "lit" term SenseSprite uses (proximity circle + echo waves), per pixel in world space.
// In the princess's light sense it is unaffected. Used by WordAura (the standing "this one answers to voice" hint).
Shader "DuskTwilight/FX/SenseAura"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        [HDR] _Tint ("Tint", Color) = (1,1,1,1)
        _DarkGain ("Dark sense gain (1 = same as a SenseSprite's lit term)", Range(0.5,4)) = 2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha One
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Project/Shaders/DuskSense.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _DarkGain;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                o.color = v.color * _Tint;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                float2 wp = i.positionWS.xy;
                float prince = DT_PrinceAmount(wp);
                float2 echo = DT_Echo(wp);
                float lit = max(echo.x * 0.85 + echo.y * 1.4, DT_SwitchScratch(wp) * 1.1);
                float seen = saturate(lit * 1.3 * _DarkGain);
                tex.a *= lerp(1.0, seen, prince);
                return half4(tex.rgb, tex.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

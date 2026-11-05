// Additive pixel-snapped soft glow (lanterns, checkpoints, goal light).
// _LightAmount / _DarkAmount pick which sense can perceive it.
Shader "DuskTwilight/Glow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        _GlowColor ("Colour", Color) = (1,0.8,0.4,1)
        _Intensity ("Intensity", Range(0,4)) = 1
        _LightAmount ("Visible in Light", Range(0,1)) = 1
        _DarkAmount ("Visible in Dark", Range(0,1)) = 0
        _Steps ("Bands", Float) = 5
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
                float4 _GlowColor;
                float _Intensity, _LightAmount, _DarkAmount, _Steps;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 centerWS : TEXCOORD1; float radius : TEXCOORD2; float4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.centerWS = TransformObjectToWorld(float3(0, 0, 0));
                o.radius = max(distance(TransformObjectToWorld(float3(0.5, 0, 0)), o.centerWS), 0.01);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 wp = i.positionWS.xy;
                float d = saturate(length(wp - i.centerWS.xy) / i.radius);
                float g = pow(1.0 - d, 2.2);
                // _Steps > 0 bands the falloff (old pixel-art look); 0 = smooth (illustrated)
                if (_Steps > 0.5) g = floor(g * _Steps + 0.5) / _Steps;
                float prince = DT_PrinceAmount(wp);
                float vis = lerp(_LightAmount, _DarkAmount, prince);
                return half4(_GlowColor.rgb * i.color.rgb, g * _Intensity * vis * i.color.a);
            }
            ENDHLSL
        }
    }
}

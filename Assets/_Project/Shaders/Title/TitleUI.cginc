// Shared bits for the title-screen UI shaders (uGUI: masks, clip rect, vertex colour).
#ifndef DUSK_TITLE_UI_INCLUDED
#define DUSK_TITLE_UI_INCLUDED

#include "UnityCG.cginc"
#include "UnityUI.cginc"

struct TitleAppdata
{
    float4 vertex   : POSITION;
    float4 color    : COLOR;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TitleV2F
{
    float4 vertex        : SV_POSITION;
    fixed4 color         : COLOR;
    float2 uv            : TEXCOORD0;
    float4 worldPosition : TEXCOORD1;
    float2 local         : TEXCOORD2;   // object-space position (RectTransform pixels, pivot-relative)
    UNITY_VERTEX_OUTPUT_STEREO
};

sampler2D _MainTex;
float4 _MainTex_ST;
fixed4 _TextureSampleAdd;
float4 _ClipRect;

TitleV2F TitleVert(TitleAppdata v)
{
    TitleV2F o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.worldPosition = v.vertex;
    o.local = v.vertex.xy;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
    o.color = v.color;
    return o;
}

fixed4 TitleFinish(fixed4 c, TitleV2F i)
{
    #ifdef UNITY_UI_CLIP_RECT
    c.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
    #endif
    #ifdef UNITY_UI_ALPHACLIP
    clip(c.a - 0.001);
    #endif
    return c;
}

float TitleHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float2 TitleHash2(float2 p)
{
    float n = TitleHash(p);
    return float2(n, TitleHash(p + n * 17.17));
}

float TitleNoise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = TitleHash(i), b = TitleHash(i + float2(1, 0));
    float c = TitleHash(i + float2(0, 1)), d = TitleHash(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float TitleFbm(float2 p)
{
    float s = 0, a = 0.5;
    for (int k = 0; k < 4; k++) { s += a * TitleNoise(p); p = p * 2.03 + 11.7; a *= 0.5; }
    return s;
}

// Signed-distance-ish diamond (rhombus) of half-size r, returns 0..1 coverage with aa px softness.
float TitleDiamond(float2 p, float r, float aa)
{
    return 1.0 - smoothstep(r - aa, r + aa, abs(p.x) + abs(p.y));
}

#endif

// Shared bits of the sense emblems (HUD). All in "emblem space": p = (uv - 0.5) * 2 of the Frame rect, so r = 1 is its edge.
#ifndef DUSK_EMBLEM_INCLUDED
#define DUSK_EMBLEM_INCLUDED

#include "../Title/TitleUI.cginc"

#define EMBLEM_TAU 6.2831853

fixed4 _Color, _RingColor, _PaleColor, _GlintColor;
float _Rim, _RingWidth;
float _T, _Activity, _Energy, _Flash, _Glint;

// A soft diagonal band crossing the badge while _Glint goes 0 -> 1.
float EmblemGlintBand(float2 p, float rim)
{
    if (_Glint <= 0 || _Glint >= 1) return 0;
    float along = (p.x + p.y) * 0.7071 / max(rim, 0.001);
    float head = lerp(-1.4, 1.4, _Glint);
    float d = (along - head) / 0.32;
    return exp(-d * d);
}

// Four-pointed twinkle on the ring, peaking in the middle of the glint.
float EmblemTwinkle(float2 p, float2 at, float size)
{
    if (_Glint <= 0 || _Glint >= 1) return 0;
    float2 q = abs(p - at) / size;
    float env = sin(_Glint * 3.14159265);
    float star = saturate(1 - pow(q.x, 0.55) - pow(q.y, 0.55));
    return star * star * env;
}

// Ring expanding from the rim for a moment after a switch or an echo.
float EmblemBurst(float r, float rim)
{
    if (_Flash <= 0.001) return 0;
    float at = lerp(1.0, rim, _Flash);
    float d = (r - at) / 0.05;
    return exp(-d * d) * _Flash;
}

// Dark soft backing so the emblem reads on any background.
float EmblemBacking(float r, float rim)
{
    return 0.45 * smoothstep(rim + 0.32, rim - 0.02, r);
}

fixed4 EmblemOver(fixed4 under, fixed3 col, float a)
{
    float outA = a + under.a * (1 - a);
    fixed3 outC = outA > 0.0001 ? (col * a + under.rgb * under.a * (1 - a)) / outA : col;
    return fixed4(outC, outA);
}

#endif

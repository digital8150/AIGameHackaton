// Dusk Princess & Twilight Prince — shared "sense" model.
// Every world shader asks two questions per pixel:
//   1) whose senses are we looking through here?  (DT_PrinceAmount: 0 = princess/light, 1 = prince/dark)
//   2) has sound touched this spot recently?      (DT_Echo: reveal fill + wavefront line)
// Globals are driven by SenseController.cs.
#ifndef DUSK_SENSE_INCLUDED
#define DUSK_SENSE_INCLUDED

#define DT_MAX_ECHOES 12
#define DT_PPU 18.0

float4 _DT_Switch;        // xy = wipe centre (world), z = radius, w = edge softness
float  _DT_ModeFrom;      // mode outside the wipe
float  _DT_ModeTo;        // mode inside the wipe
float  _DT_Now;           // Time.time
float4 _DT_Echo[DT_MAX_ECHOES];   // xy = origin, z = start time, w = strength (0 = slot unused)
float4 _DT_EchoParams;    // x = speed (u/s), y = radius per strength, z = lifetime (s), w = director boost (>=1)
float4 _DT_Listener;      // xy = prince position, z = self-aura radius, w = unused
float4 _DT_SunScreen;     // xy = sun in viewport uv, z = sun strength
float4 _DT_HazeColor;
float4 _DT_SwitchFx;      // x = edge tear (units), y = tear scale, z = gold rim width, w = scratchboard band width     // light-mode atmospheric haze colour

// Snap a world position to the 18 ppu pixel grid so procedural effects stay pixel-true.
float2 DT_Snap(float2 wp)
{
    return (floor(wp * DT_PPU) + 0.5) / DT_PPU;
}

// x = reveal (area the sound has passed through, fading), y = wavefront line
float2 DT_Echo(float2 wp)
{
    float2 acc = 0;
    float speed = _DT_EchoParams.x;
    float life = _DT_EchoParams.z;
    [unroll]
    for (int i = 0; i < DT_MAX_ECHOES; i++)
    {
        float4 e = _DT_Echo[i];
        float age = _DT_Now - e.z;
        if (e.w <= 0.0 || age < 0.0 || age > life) continue;

        float maxR = e.w * _DT_EchoParams.y * _DT_EchoParams.w;
        float r = min(age * speed, maxR);
        float d = distance(wp, e.xy);
        float fade = 1.0 - saturate(age / life);
        float falloff = 1.0 - smoothstep(maxR * 0.7, maxR, d);

        float within = step(d, r) * falloff * fade * fade;
        // wavefront: thin line, quantised to pixel width, only while still travelling
        float travelling = 1.0 - step(maxR - 0.01, age * speed);
        float front = (1.0 - saturate(abs(d - r) * DT_PPU * 0.34)) * falloff * lerp(0.25, 1.0, travelling) * fade;
        // soft trailing glow just behind the front
        front = max(front, saturate(1.0 - (r - d) * 1.6) * step(d, r) * 0.35 * falloff * fade * travelling);

        acc.x = max(acc.x, within);
        acc.y = max(acc.y, front);
    }
    // the prince always feels a little of the space around him (touch / own breath)
    float self = 1.0 - smoothstep(_DT_Listener.z * 0.4, _DT_Listener.z, distance(wp, _DT_Listener.xy));
    acc.x = max(acc.x, self * 0.45);
    return acc;
}

float DT_Luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

// Cheap hash / value noise (used for glints and sky)
float DT_Hash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float DT_Noise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float a = DT_Hash(i);
    float b = DT_Hash(i + float2(1, 0));
    float c = DT_Hash(i + float2(0, 1));
    float d = DT_Hash(i + float2(1, 1));
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float DT_Fbm(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        v += a * DT_Noise(p);
        p = p * 2.03 + 17.1;
        a *= 0.5;
    }
    return v;
}

// ---------------- switch wipe: a torn-paper edge that sweeps the world from one sense to the other -----------
// Signed distance to the wipe front, roughened with fbm so the edge looks torn / ink-bled and keeps crawling.
float DT_SwitchEdge(float2 wp)
{
    float d = distance(wp, _DT_Switch.xy);
    float tear = _DT_SwitchFx.x > 0 ? (DT_Fbm(wp * _DT_SwitchFx.y + float2(_DT_Now * 0.6, -_DT_Now * 0.4)) * 2.0 - 1.0) * _DT_SwitchFx.x : 0.0;
    return d + tear - _DT_Switch.z;   // < 0 inside the new sense
}

float DT_PrinceAmount(float2 wp)
{
    float e = DT_SwitchEdge(wp);
    float inside = 1.0 - smoothstep(-_DT_Switch.w, 0.0, e);
    return lerp(_DT_ModeFrom, _DT_ModeTo, inside);
}

float DT_SwitchActive() { return step(0.001, abs(_DT_ModeTo - _DT_ModeFrom)); }

// Gold rim riding the torn edge while a transition is in flight, with flickering embers.
float DT_SwitchRim(float2 wp)
{
    float e = DT_SwitchEdge(wp);
    float w = max(_DT_SwitchFx.z, 0.2);
    float band = 1.0 - saturate(abs(e) / w);
    float core = 1.0 - saturate(abs(e) / (w * 0.25));
    float ember = step(0.975, DT_Hash(floor(wp * 18.0) + floor(_DT_Now * 16.0))) * (1.0 - saturate(abs(e) / (w * 2.2)));
    return saturate(band * band * 0.8 + core * 0.9 + ember) * DT_SwitchActive();
}

// Band behind the front where the dark side is drawn as white scratchboard lines (everything, not just echoes).
float DT_SwitchScratch(float2 wp)
{
    float e = DT_SwitchEdge(wp);
    float bw = max(_DT_SwitchFx.w, 0.1);
    return saturate(1.0 - abs(e) / bw) * DT_SwitchActive();
}

static const float3 DT_NIGHT = float3(0.0035, 0.005, 0.016);
static const float3 DT_ECHO_LINE = float3(0.82, 0.92, 1.0);
static const float3 DT_RIM_GOLD = float3(1.0, 0.86, 0.55);

#endif

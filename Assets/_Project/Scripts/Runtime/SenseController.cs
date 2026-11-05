using System;
using UnityEngine;

namespace DuskTwilight
{
    public enum Sense { Light = 0, Dark = 1 } // Light = Dusk Princess's eyes, Dark = Twilight Prince's ears

    /// <summary>
    /// Owns "whose senses are we perceiving through" and every sound wavefront in the world.
    /// Pushes everything to global shader variables consumed by DuskSense.hlsl.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SenseController : MonoBehaviour
    {
        public static SenseController I { get; private set; }

        [Header("Switch wipe")]
        public float switchDuration = 1.0f;
        public float switchMaxRadius = 34f;
        public float switchSoftness = 0.6f;

        [Header("Switch drama")]
        [Tooltip("How torn / ink-bled the wipe edge is (world units of wobble)")]
        [Range(0f, 4f)] public float edgeTear = 2.2f;
        [Tooltip("Size of the tear pattern (higher = finer)")]
        [Range(0.05f, 1.5f)] public float edgeTearScale = 0.32f;
        [Tooltip("Width of the gold rim on the edge")]
        [Range(0.1f, 3f)] public float rimWidth = 1.1f;
        [Tooltip("Band behind the edge where the dark side shows as white scratchboard line art")]
        [Range(0f, 10f)] public float scratchBand = 4f;
        [Tooltip("Time slows to this for a beat when the senses swap (1 = off)")]
        [Range(0.05f, 1f)] public float hitStopScale = 0.3f;
        [Tooltip("Real seconds of the slow beat (eases back to normal over the same time again)")]
        [Range(0f, 0.6f)] public float hitStopTime = 0.14f;
        [Tooltip("Camera pushes in by this share of its size, then settles")]
        [Range(0f, 0.2f)] public float zoomPunch = 0.07f;
        public float zoomTime = 0.7f;

        [Header("Echo")]
        public float echoSpeed = 11f;
        public float echoRadiusPerStrength = 10f;
        public float echoLifetime = 2.6f;
        public float listenerAura = 2.4f;

        [Header("Light sense")]
        public Vector2 sunViewport = new Vector2(0.74f, 0.36f);
        public float sunStrength = 1f;
        public Color haze = new Color(0.98f, 0.68f, 0.58f);

        [Header("Audio by sense")]
        [Tooltip("The princess cannot hear: in Light sense the world is silent.")]
        public float lightSenseVolume = 0f;
        public float audioFadeSpeed = 3f;

        public Sense Current { get; private set; } = Sense.Light;
        public float SwitchProgress { get; private set; } = 1f;
        public float DirectorBoost { get; set; } = 1f;
        public Transform Listener { get; set; }
        /// <summary>0-1 loudness of the world (SFX) at the listener; AudioRouter applies it to the SFX mixer group. Music is unaffected.</summary>
        public float WorldVolume { get; private set; } = 1f;

        public event Action<Sense> ModeChanged;
        public event Action<Vector2, float> EchoEmitted;

        const int MaxEchoes = 12;
        readonly Vector4[] echoes = new Vector4[MaxEchoes];
        int nextEcho;
        float from, to;
        Vector2 switchCenter;

        static readonly int IdSwitch = Shader.PropertyToID("_DT_Switch");
        static readonly int IdFrom = Shader.PropertyToID("_DT_ModeFrom");
        static readonly int IdTo = Shader.PropertyToID("_DT_ModeTo");
        static readonly int IdNow = Shader.PropertyToID("_DT_Now");
        static readonly int IdEcho = Shader.PropertyToID("_DT_Echo");
        static readonly int IdEchoParams = Shader.PropertyToID("_DT_EchoParams");
        static readonly int IdListener = Shader.PropertyToID("_DT_Listener");
        static readonly int IdSun = Shader.PropertyToID("_DT_SunScreen");
        static readonly int IdHaze = Shader.PropertyToID("_DT_HazeColor");
        static readonly int IdCam = Shader.PropertyToID("_DT_Cam");
        static readonly int IdSwitchFx = Shader.PropertyToID("_DT_SwitchFx");

        void Awake()
        {
            I = this;
            AudioListener.volume = 1f; // silence is per-group now (WorldVolume)
            from = to = (float)Current;
            for (int i = 0; i < MaxEchoes; i++) echoes[i] = new Vector4(0, 0, -999, 0);
            Push();
        }

        void OnDestroy() { if (I == this) I = null; }

        public bool IsDark => Current == Sense.Dark;

        public void SetSense(Sense s, Vector2 center, bool instant = false)
        {
            if (s == Current && SwitchProgress >= 1f) return;
            from = (float)Current; // the old sense stays outside the expanding wipe
            to = (float)s;
            Current = s;
            switchCenter = center;
            SwitchProgress = instant ? 1f : 0f;
            if (instant) from = to;
            else StartCoroutine(SwitchDrama());
            ModeChanged?.Invoke(s);
        }

        /// <summary>
        /// Ending: both worlds at once. Eases the whole screen to a mix of the two senses (0 = light, 1 = dark)
        /// with no wipe; the world stays there.
        /// </summary>
        public void BlendTo(float mix, float seconds)
        {
            StopAllCoroutines();
            SwitchProgress = 1f;
            StartCoroutine(Blend(mix, seconds));
        }

        System.Collections.IEnumerator Blend(float mix, float seconds)
        {
            float a = to;
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds))
            {
                from = to = Mathf.Lerp(a, mix, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
            from = to = mix;
        }

        /// <summary>CPU mirror of DT_PrinceAmount (0 = light, 1 = dark) for scripts that fade UI/text.</summary>
        float baseOrtho;

        System.Collections.IEnumerator SwitchDrama()
        {
            var cam = Camera.main;
            if (cam && baseOrtho <= 0f) baseOrtho = cam.orthographicSize;
            // a held breath: time slows for a beat while the world turns over
            if (hitStopScale < 1f && hitStopTime > 0f && !GameFlow.ModalOpen)
            {
                Time.timeScale = hitStopScale;
                yield return new WaitForSecondsRealtime(hitStopTime);
                for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / hitStopTime)
                {
                    Time.timeScale = Mathf.Lerp(hitStopScale, 1f, t);
                    yield return null;
                }
                Time.timeScale = 1f;
            }
        }

        void LateUpdate()
        {
            // camera push-in that follows the wipe and settles back
            var cam = Camera.main;
            if (!cam || zoomPunch <= 0f) return;
            if (baseOrtho <= 0f) baseOrtho = cam.orthographicSize;
            float t = Mathf.Clamp01(SwitchProgress * switchDuration / Mathf.Max(0.05f, zoomTime));
            float k = SwitchProgress >= 1f ? 0f : Mathf.Sin(t * Mathf.PI) * (1f - t * 0.3f);
            cam.orthographicSize = baseOrtho * (1f - zoomPunch * k);
        }

        void OnDisable() { Time.timeScale = 1f; }

        public float SenseAt(Vector2 p)
        {
            float r = Radius;
            float d = Vector2.Distance(p, switchCenter);
            float inside = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(r - switchSoftness, r, d));
            return Mathf.Lerp(from, to, inside);
        }

        float Radius => SwitchProgress >= 1f ? 9999f : EaseOutCubic(SwitchProgress) * switchMaxRadius;

        static float EaseOutCubic(float t) { t = 1f - t; return 1f - t * t * t; }

        public void EmitEcho(Vector2 pos, float strength)
        {
            echoes[nextEcho] = new Vector4(pos.x, pos.y, Time.time, strength);
            nextEcho = (nextEcho + 1) % MaxEchoes;
            EchoEmitted?.Invoke(pos, strength);
        }

        void Update()
        {
            if (SwitchProgress < 1f)
            {
                SwitchProgress = Mathf.Min(1f, SwitchProgress + Time.deltaTime / switchDuration);
                if (SwitchProgress >= 1f) from = to;
            }

            // The princess cannot hear: fade the whole mix with the sense at the listener.
            float darkHere = Listener ? SenseAt(Listener.position) : (float)Current;
            float target = Mathf.Lerp(lightSenseVolume, 1f, darkHere);
            WorldVolume = Mathf.MoveTowards(WorldVolume, target, Time.deltaTime * audioFadeSpeed);

            Push();
        }

        void Push()
        {
            Shader.SetGlobalVector(IdSwitch, new Vector4(switchCenter.x, switchCenter.y, Radius, switchSoftness));
            Shader.SetGlobalVector(IdSwitchFx, new Vector4(edgeTear, edgeTearScale, rimWidth, scratchBand));
            Shader.SetGlobalFloat(IdFrom, from);
            Shader.SetGlobalFloat(IdTo, to);
            Shader.SetGlobalFloat(IdNow, Time.time);
            Shader.SetGlobalVectorArray(IdEcho, echoes);
            Shader.SetGlobalVector(IdEchoParams, new Vector4(echoSpeed, echoRadiusPerStrength, echoLifetime, DirectorBoost));
            Vector2 l = Listener ? (Vector2)Listener.position : Vector2.zero;
            Shader.SetGlobalVector(IdListener, new Vector4(l.x, l.y, listenerAura, 0));
            Shader.SetGlobalVector(IdSun, new Vector4(sunViewport.x, sunViewport.y, sunStrength, 0));
            Shader.SetGlobalColor(IdHaze, haze);
            var cam = Camera.main;
            if (cam) Shader.SetGlobalVector(IdCam, cam.transform.position);
        }
    }
}

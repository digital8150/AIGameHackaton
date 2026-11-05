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
        public float switchDuration = 0.65f;
        public float switchMaxRadius = 32f;
        public float switchSoftness = 0.6f;

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

        void Awake()
        {
            I = this;
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
            ModeChanged?.Invoke(s);
        }

        /// <summary>CPU mirror of DT_PrinceAmount (0 = light, 1 = dark) for scripts that fade UI/text.</summary>
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
            AudioListener.volume = Mathf.MoveTowards(AudioListener.volume, target, Time.deltaTime * audioFadeSpeed);

            Push();
        }

        void Push()
        {
            Shader.SetGlobalVector(IdSwitch, new Vector4(switchCenter.x, switchCenter.y, Radius, switchSoftness));
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

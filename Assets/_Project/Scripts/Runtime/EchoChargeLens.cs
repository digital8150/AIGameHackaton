using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DuskTwilight
{
    /// <summary>
    /// Drives the full-screen convex lens (shader DuskTwilight/FX/EchoLens) while the prince charges his echo:
    /// it swells with the hold, kicks at each size, and collapses into a soft ripple when he lets go. The renderer feature
    /// is switched on only while there is something to draw, so the screen copy costs nothing the rest of the time.
    /// Sits next to EchoCaller on Systems; the look is in M_Echo_Lens.
    /// </summary>
    public class EchoChargeLens : MonoBehaviour
    {
        static readonly int LensAId = Shader.PropertyToID("_EchoLensA");
        static readonly int LensBId = Shader.PropertyToID("_EchoLensB");

        [Tooltip("Renderer2D's Echo Lens feature.")]
        public ScriptableRendererFeature lensFeature;
        [Tooltip("Blank = found on this object.")]
        public EchoCaller echo;

        [Header("Size (metres)")]
        [Tooltip("Rim of the distortion when a charge starts and at full charge.")]
        public Vector2 radius = new Vector2(1.2f, 2.8f);

        [Header("Charge")]
        [Tooltip("Above 1 stays faint on a tap and comes on late in the hold.")]
        [Range(0.5f, 3f)] public float chargeCurve = 1.3f;
        [Tooltip("Seconds the lens takes to catch up with the charge.")]
        public float followSeconds = 0.05f;
        [Tooltip("Extra strength for a moment when the charge reaches the next echo size.")]
        [Range(0, 1)] public float stepKick = 0.12f;
        public float stepKickSeconds = 0.14f;

        [Header("Release")]
        [Tooltip("Seconds the lens takes to collapse.")]
        public float collapseSeconds = 0.1f;
        [Tooltip("Seconds the shock takes to travel.")]
        public float shockSeconds = 0.42f;
        [Tooltip("How far the shock travels for small, medium and large echoes (metres).")]
        public Vector3 shockReach = new Vector3(3f, 5f, 8f);
        [Tooltip("Shock strength for small, medium and large echoes.")]
        public Vector3 shockAmount = new Vector3(0.35f, 0.65f, 1f);

        [Header("Output")]
        [Tooltip("Set if the lens sits upside down compared with the prince on this platform.")]
        public bool flipCentre;

        float strength, kick, followVelocity, shockT = -1f, shockReachNow, shockAmountNow;
        Vector2 worldCentre;
        int lastStep;
        bool featureOn;
        Camera cam;

        void Awake()
        {
            if (!echo) echo = GetComponent<EchoCaller>();
            SetFeature(false);
            Shader.SetGlobalVector(LensAId, Vector4.zero);
            Shader.SetGlobalVector(LensBId, Vector4.zero);
        }

        void OnEnable() { if (echo) echo.Released += OnReleased; }

        void OnDisable()
        {
            if (echo) echo.Released -= OnReleased;
            SetFeature(false);
        }

        void OnReleased(Vector2 at, EchoSize size)
        {
            int i = (int)size;
            shockT = 0f;
            shockReachNow = shockReach[i];
            shockAmountNow = shockAmount[i];
            worldCentre = at;
        }

        void LateUpdate()
        {
            if (!echo) return;
            float dt = Time.deltaTime;
            bool charging = echo.Charging;
            float charge = echo.Charge01;

            if (charging)
            {
                if (echo.CallPosition.HasValue) worldCentre = echo.CallPosition.Value;
                float mid = echo.mediumHold / Mathf.Max(echo.largeHold, 0.01f);
                int step = charge >= mid ? 1 : 0;
                if (step > lastStep) kick = stepKick;
                lastStep = step;
                float target = Mathf.Pow(charge, chargeCurve);
                strength = Mathf.SmoothDamp(strength, target, ref followVelocity, followSeconds, Mathf.Infinity, dt);
            }
            else
            {
                lastStep = 0;
                followVelocity = 0f;
                strength = Mathf.MoveTowards(strength, 0f, dt / Mathf.Max(collapseSeconds, 0.01f));
            }
            kick = Mathf.MoveTowards(kick, 0f, dt / Mathf.Max(stepKickSeconds, 0.01f) * Mathf.Max(stepKick, 0.01f));

            float shockAmt = 0f, shockProgress = 0f;
            if (shockT >= 0f)
            {
                shockT += dt / Mathf.Max(shockSeconds, 0.01f);
                if (shockT >= 1f) shockT = -1f;
                else
                {
                    shockProgress = 1f - (1f - shockT) * (1f - shockT);
                    shockAmt = shockAmountNow * (1f - shockT);
                }
            }

            bool active = strength > 0.002f || shockT >= 0f;
            SetFeature(active);
            if (!active) return;

            if (!cam) cam = Camera.main;
            if (!cam) return;

            float charged = Mathf.Clamp01(strength);
            float shown = Mathf.Clamp01(strength + kick);
            float rimMetres = Mathf.Lerp(radius.x, radius.y, charged);
            Vector3 c = cam.WorldToViewportPoint(worldCentre);
            float metre = cam.WorldToViewportPoint(worldCentre + Vector2.up).y - c.y;

            Shader.SetGlobalVector(LensAId, new Vector4(c.x, c.y, rimMetres * metre, shown));
            Shader.SetGlobalVector(LensBId, new Vector4(shockProgress * shockReachNow * metre, shockAmt, flipCentre ? 1f : 0f, 0f));
        }

        void SetFeature(bool on)
        {
            if (!lensFeature || featureOn == on && lensFeature.isActive == on) return;
            featureOn = on;
            lensFeature.SetActive(on);
        }

        void OnDrawGizmosSelected()
        {
            if (!echo) return;
            var pos = echo.CallPosition;
            if (!pos.HasValue) return;
            Gizmos.color = new Color(0.55f, 0.78f, 1f, 0.6f);
            Gizmos.DrawWireSphere(pos.Value, radius.x);
            Gizmos.DrawWireSphere(pos.Value, radius.y);
        }
    }
}

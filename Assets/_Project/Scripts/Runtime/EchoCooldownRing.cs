using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// The small glowing ring that floats behind the prince: it refills during the echo's cooldown, shows the charge
    /// while the key is held and flares once when the echo is ready again. Hidden the rest of the time, and always
    /// while the princess leads. Child of the TwilightPrince prefab; look is in M_Echo_CooldownRing.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class EchoCooldownRing : MonoBehaviour
    {
        static readonly int FillId = Shader.PropertyToID("_Fill");
        static readonly int ReadyId = Shader.PropertyToID("_Ready");
        static readonly int ChargeMixId = Shader.PropertyToID("_ChargeMix");
        static readonly int TickMidId = Shader.PropertyToID("_TickMid");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        [Tooltip("Blank = found in the scene.")]
        public EchoCaller echo;

        [Header("Place")]
        [Tooltip("Offset from the prince, in metres. X is mirrored so the ring stays behind him.")]
        public Vector2 offset = new Vector2(1.15f, 1.5f);
        [Tooltip("Smoothing time when he turns around.")]
        public float sideSmoothTime = 0.12f;

        [Header("Timing")]
        public float fadeInSeconds = 0.06f;
        public float fadeOutSeconds = 0.18f;
        [Tooltip("Seconds of the flare when the echo is ready again.")]
        public float readySeconds = 0.4f;

        SpriteRenderer sr;
        Material mat;
        Actor actor;
        float alpha, ready, side = 1f, sideVelocity, fillShown;
        bool wasCooling, princeSeen;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            mat = new Material(sr.sharedMaterial) { name = sr.sharedMaterial.name + " (Instance)" };
            sr.sharedMaterial = mat;
            actor = GetComponentInParent<Actor>();
            mat.SetFloat(AlphaId, 0f);
        }

        void Start()
        {
            if (!echo) echo = FindAnyObjectByType<EchoCaller>();
            if (actor) side = actor.Facing < 0 ? 1f : -1f;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            bool princeLeads = DuoController.I && DuoController.I.Leader && DuoController.I.Leader.sense == Sense.Dark;
            bool cooling = echo && echo.CooldownProgress < 1f;
            bool charging = echo && echo.Charging;

            if (wasCooling && !cooling && princeLeads) ready = 1f;
            wasCooling = cooling;
            ready = Mathf.MoveTowards(ready, 0f, dt / Mathf.Max(readySeconds, 0.01f));

            bool show = princeLeads && (cooling || charging || ready > 0f);
            alpha = Mathf.MoveTowards(alpha, show ? 1f : 0f, dt / Mathf.Max(show ? fadeInSeconds : fadeOutSeconds, 0.01f));
            // the sense switch must not flash a stale ring at the prince's side
            if (!princeLeads) princeSeen = false;
            else if (!princeSeen) { princeSeen = true; if (!show) alpha = 0f; }

            sr.enabled = alpha > 0f;
            if (!sr.enabled || !echo) return;

            if (charging) fillShown = echo.Charge01;
            else if (cooling) fillShown = echo.CooldownProgress;
            else fillShown = 1f;

            mat.SetFloat(FillId, fillShown);
            mat.SetFloat(ReadyId, ready);
            mat.SetFloat(ChargeMixId, charging ? 1f : 0f);
            mat.SetFloat(TickMidId, echo.mediumHold / Mathf.Max(echo.largeHold, 0.01f));
            mat.SetFloat(AlphaId, alpha);

            if (actor)
            {
                float wanted = actor.Facing < 0 ? 1f : -1f;
                side = Mathf.SmoothDamp(side, wanted, ref sideVelocity, sideSmoothTime, Mathf.Infinity, dt);
            }
            transform.localPosition = new Vector3(offset.x * side, offset.y, 0f);
        }

        void OnDestroy()
        {
            if (mat) Destroy(mat);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.55f, 0.78f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, transform.lossyScale.x * 0.28f);
        }
    }
}

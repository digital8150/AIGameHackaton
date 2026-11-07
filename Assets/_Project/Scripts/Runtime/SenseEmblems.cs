using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// The two character badges at the top left. On every switch they orbit each other clockwise and trade
    /// places, then both swell for a moment. The leader sits on Main Slot, the partner on Partner Slot: move and
    /// scale those two in the prefab to restyle the layout.
    /// </summary>
    public class SenseEmblems : MonoBehaviour
    {
        [Header("Elements")]
        public SenseEmblem princess;
        public SenseEmblem prince;
        [Tooltip("Position and scale of the leader's badge.")]
        public RectTransform mainSlot;
        [Tooltip("Position and scale of the partner's badge.")]
        public RectTransform partnerSlot;
        [Tooltip("Blank = found in the scene. Echo charge drives the prince's ripples.")]
        public EchoCaller echo;

        [Header("Switch")]
        [Tooltip("Smoothing time of the orbit; the swap takes about three times this.")]
        public float orbitSmoothTime = 0.16f;
        [Tooltip("How far the orbit bulges beyond the straight line between slots, as a fraction of its radius.")]
        [Range(0, 2)] public float orbitBulge = 0.5f;
        [Tooltip("How much both badges swell after landing.")]
        [Range(0, 1)] public float landPop = 0.2f;
        public float landPopSeconds = 0.4f;
        [Tooltip("Swell while they fly, on top of the slot scale.")]
        [Range(0, 1)] public float flightPop = 0.08f;

        [Header("Partner")]
        public Color partnerTint = new Color(0.6f, 0.6f, 0.66f, 0.92f);
        [Range(0, 1)] public float partnerActivity = 0.4f;
        [Tooltip("How much the partner badge swells at the top of a switch hint's breath (GuideNudges)")]
        [Range(0, 0.6f)] public float partnerNudgeScale = 0.22f;

        /// <summary>0..1, set each frame by GuideNudges while switching to the partner would help here.</summary>
        [System.NonSerialized] public float partnerNudge;

        [Header("Energy")]
        [Tooltip("Voice level is multiplied by this before clamping to 0..1.")]
        public float voiceGain = 25f;
        [Tooltip("Flash on the prince's badge when an echo goes off.")]
        [Range(0, 1)] public float echoFlash = 0.55f;

        // 0 = princess leads; +pi per switch. Never unwinds, so the motion is always clockwise.
        float angle, targetAngle, angleVelocity;
        float popT = -1f;
        bool landing;
        bool princeShown;   // who targetAngle puts on the main slot

        // DuoController sets Leader in Awake, so this holds in any Start order; SenseController only catches up in DuoController.Start.
        static bool PrinceLeads => DuoController.I && DuoController.I.Leader && DuoController.I.Leader.sense == Sense.Dark;

        void Start()
        {
            if (!princess || !prince || !mainSlot || !partnerSlot) { Debug.LogError("[SenseEmblems] elements are not assigned - use the HUD prefab.", this); enabled = false; return; }
            if (!echo) echo = FindAnyObjectByType<EchoCaller>();
            princeShown = PrinceLeads;
            angle = targetAngle = princeShown ? Mathf.PI : 0f;
            GameEvents.Switched += OnSwitched;
            EchoCaller.Called += OnEchoCalled;
            Apply();
        }

        void OnDestroy()
        {
            GameEvents.Switched -= OnSwitched;
            EchoCaller.Called -= OnEchoCalled;
        }

        void OnSwitched(Sense s)
        {
            landing = true;
            popT = -1f;
            princess.Flash();
            prince.Flash();
            (s == Sense.Dark ? prince : princess).Shine();
        }

        void OnEchoCalled(Vector2 at, float strength) => prince.Flash(echoFlash);

        void Update()
        {
            // the leader is the only truth: the badges orbit whenever they disagree with it
            if (DuoController.I && princeShown != PrinceLeads)
            {
                princeShown = !princeShown;
                targetAngle += Mathf.PI;
            }

            float dt = Time.unscaledDeltaTime;
            angle =Mathf.SmoothDamp(angle, targetAngle, ref angleVelocity, orbitSmoothTime, Mathf.Infinity, dt);
            if (landing && Mathf.Abs(targetAngle - angle) < 0.12f)
            {
                landing = false;
                popT = 0f;
            }
            if (popT >= 0)
            {
                popT += dt / Mathf.Max(landPopSeconds, 0.01f);
                if (popT >= 1f) popT = -1f;
            }
            Apply();
        }

        void Apply()
        {
            float leadPrincess = (1f + Mathf.Cos(angle)) * 0.5f; // 1 = princess on the main slot
            float flying = Mathf.Abs(Mathf.Sin(angle));
            float pop = 1f + flying * flightPop + (popT >= 0 ? Mathf.Sin(popT * Mathf.PI) * landPop : 0f);

            Vector2 mid = (mainSlot.anchoredPosition + partnerSlot.anchoredPosition) * 0.5f;
            Vector2 arm = mainSlot.anchoredPosition - mid;
            arm *= 1f + flying * orbitBulge;
            // negative rotation in a y-up canvas = clockwise on screen
            Vector2 toPrincess = Rotate(arm, -angle);

            Place(princess, mid + toPrincess, leadPrincess, pop);
            Place(prince, mid - toPrincess, 1f - leadPrincess, pop);

            (leadPrincess >= 0.5f ? princess : prince).transform.SetAsLastSibling();

            var voice = VoiceInput.I;
            float voiceEnergy = voice && voice.Recording ? Mathf.Clamp01(voice.Level * voiceGain) : 0f;
            float chargeEnergy = echo && echo.Charge >= 0f ? Mathf.Clamp01(echo.Charge / Mathf.Max(echo.largeHold, 0.01f)) : 0f;
            princess.Tick(Mathf.Lerp(partnerActivity, 1f, leadPrincess), voiceEnergy, Color.Lerp(partnerTint, Color.white, leadPrincess));
            prince.Tick(Mathf.Lerp(partnerActivity, 1f, 1f - leadPrincess), chargeEnergy, Color.Lerp(partnerTint, Color.white, 1f - leadPrincess));
        }

        void Place(SenseEmblem e, Vector2 pos, float lead, float pop)
        {
            e.Rect.anchoredPosition = pos;
            float s = Mathf.Lerp(partnerSlot.localScale.x, mainSlot.localScale.x, lead) * pop;
            s *= 1f + partnerNudge * partnerNudgeScale * (1f - lead);
            e.Rect.localScale = new Vector3(s, s, 1f);
        }

        static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        void OnDrawGizmos()
        {
            if (!mainSlot || !partnerSlot) return;
            Gizmos.color = new Color(1f, 0.85f, 0.35f, 0.9f);
            Gizmos.DrawLine(mainSlot.position, partnerSlot.position);
        }
    }
}

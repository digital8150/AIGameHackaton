using UnityEngine;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// Early-stage visual guide on the HUD: when something nearby wants a particular ability and the player hasn't used
    /// it yet, that ability's key chip breathes (grows and warms). When it wants the *other* character, the partner badge
    /// swells and a small "Tab" chip fades in under it.
    ///   prince E  — a hidden plank / fake wall the echo hasn't found yet
    ///   prince V  — a gold object nobody has spoken to yet, in talking range
    ///   princess E — a box nobody has moved yet
    /// Each hint waits a moment (passing by doesn't flicker) and goes quiet for a while after the ability is used.
    /// Layout lives in the HUD prefab (Ability Keys / Switch Hint); this only animates it.
    /// Off by default — chapters that teach turn it on (scene override).
    /// </summary>
    public class GuideNudges : MonoBehaviour
    {
        [Tooltip("Turn the guide on for this chapter (scene override). Later chapters leave the discovery to the player.")]
        public bool guide;

        [Header("Chips")]
        [Tooltip("Prince's E (echo) chip")]
        public RectTransform echoKey;
        [Tooltip("Prince's V (voice) chip")]
        public RectTransform voiceKey;
        [Tooltip("Princess's E (grab) chip")]
        public RectTransform grabKey;
        [Tooltip("Faded in under the partner badge when switching would help (Tab chip + label)")]
        public CanvasGroup switchHint;
        [Tooltip("Partner badge swells with the switch hint")]
        public SenseEmblems emblems;

        [Header("When")]
        [Tooltip("Unfound plank / fake wall this close (m) → echo")]
        [Min(0f)] public float echoRadius = 6f;
        [Tooltip("Unmoved box this close (m) → grab")]
        [Min(0f)] public float boxRadius = 3.5f;
        [Tooltip("Gold object this much further than its talking range still counts (m)")]
        [Min(0f)] public float wordExtra = 0f;
        [Tooltip("Seconds near it before a hint starts")]
        [Min(0f)] public float delay = 0.6f;
        [Tooltip("Seconds near it before the switch hint starts (a little later: try what you have first)")]
        [Min(0f)] public float switchDelay = 1.2f;
        [Tooltip("Quiet seconds after the ability is used")]
        [Min(0f)] public float quietSeconds = 2.5f;

        [Header("Look")]
        [Range(0f, 0.6f)] public float keyScale = 0.18f;
        [Min(0.1f)] public float speed = 4.5f;
        [Tooltip("Tint mixed into a chip at the top of each breath")]
        public Color tint = new Color(1f, 0.86f, 0.5f, 1f);
        [Min(0.05f)] public float fadeSeconds = 0.3f;

        class Chip
        {
            public RectTransform rect;
            public Image image;
            public Color baseColor = Color.white;
            public float since = -1f, k;

            public Chip(RectTransform r)
            {
                rect = r;
                if (r) image = r.GetComponent<Image>();
                if (image) baseColor = image.color;
            }
        }

        Chip echo, voice, grab, swap;
        float lastEcho = -99f, lastVoice = -99f, lastSwitch = -99f;

        void Awake()
        {
            echo = new Chip(echoKey);
            voice = new Chip(voiceKey);
            grab = new Chip(grabKey);
            swap = new Chip(null);
            if (switchHint) switchHint.alpha = 0f;
        }

        void OnEnable()
        {
            EchoCaller.Called += OnEcho;
            GameEvents.Switched += OnSwitched;
        }

        void OnDisable()
        {
            EchoCaller.Called -= OnEcho;
            GameEvents.Switched -= OnSwitched;
        }

        void OnEcho(Vector2 at, float strength) => lastEcho = Time.time;
        void OnSwitched(Sense s) => lastSwitch = Time.time;

        void Update()
        {
            var duo = DuoController.I;
            bool wantEcho = false, wantVoice = false, wantGrab = false, wantSwap = false;
            if (guide && duo && duo.Leader)
            {
                var vi = VoiceInput.I;
                if (vi && (vi.Recording || vi.Transcribing)) lastVoice = Time.time;

                bool prince = duo.Leader == duo.prince;
                Vector3 pos = duo.Leader.transform.position;
                Vector2 p = (Vector2)pos + Vector2.up * 0.5f;
                bool hidden = HiddenPlank.AnyUnfoundWithin(p, echoRadius) || FakeWall.AnyUnfoundWithin(p, echoRadius);
                bool princeWord = WordTarget.AnyUnansweredInReach(pos, true, wordExtra);
                bool princessWord = WordTarget.AnyUnansweredInReach(pos, false, wordExtra);
                bool box = PushBox.AnyUnmovedWithin(p, boxRadius);

                if (prince)
                {
                    wantEcho = hidden && Time.time - lastEcho > quietSeconds;
                    wantVoice = princeWord && Time.time - lastVoice > quietSeconds;
                    wantSwap = (box || princessWord) && !hidden && !princeWord;
                }
                else
                {
                    wantGrab = box && !PushBox.IsBesideAny(duo.Leader);
                    wantSwap = (hidden || princeWord) && !box && !princessWord;
                }
                if (Time.time - lastSwitch < quietSeconds) wantSwap = false;
            }

            Breathe(echo, wantEcho, delay);
            Breathe(voice, wantVoice, delay);
            Breathe(grab, wantGrab, delay);
            float b = Breathe(swap, wantSwap, switchDelay);
            if (emblems) emblems.partnerNudge = b;
            if (switchHint) switchHint.alpha = Mathf.SmoothStep(0f, 1f, swap.k);
        }

        /// <summary>Advance one hint; returns the breath (0..1) and animates its chip if it has one.</summary>
        float Breathe(Chip c, bool want, float wait)
        {
            if (!want) c.since = -1f;
            else if (c.since < 0f) c.since = Time.time;
            bool on = want && Time.time - c.since >= wait;
            c.k = Mathf.MoveTowards(c.k, on ? 1f : 0f, Time.unscaledDeltaTime / fadeSeconds);

            float breath = c.k * (0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * speed));
            if (c.rect) c.rect.localScale = Vector3.one * (1f + keyScale * breath);
            if (c.image) c.image.color = Color.Lerp(c.baseColor, tint, breath);
            return breath;
        }
    }
}

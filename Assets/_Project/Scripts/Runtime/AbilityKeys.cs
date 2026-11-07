using UnityEngine;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// Shows the key hints of whoever leads: the prince's (echo, voice) or the princess's (grab).
    /// The chips and labels live in the HUD prefab (HUD Canvas/Ability Keys); this only cross-fades the two groups.
    /// </summary>
    public class AbilityKeys : MonoBehaviour
    {
        [Tooltip("Shown while the prince (dark sense) leads.")]
        public CanvasGroup princeKeys;
        [Tooltip("Shown while the princess (light sense) leads.")]
        public CanvasGroup princessKeys;
        [Tooltip("Seconds for a full fade in or out.")]
        public float fadeSeconds = 0.18f;

        [Header("Echo nudge")]
        [Tooltip("The prince's E chip. It breathes while something the echo could uncover (a hidden plank or fake wall " +
                 "not yet found) is near him and he hasn't called for a while — tells the player when the echo is worth using.")]
        public RectTransform echoKey;
        [Tooltip("How close (m) an unfound plank / fake wall must be")]
        [Min(0f)] public float nudgeRadius = 6f;
        [Tooltip("Quiet seconds after an echo before the nudge may start again")]
        [Min(0f)] public float nudgeQuietSeconds = 2.5f;
        [Tooltip("Seconds near it before the nudge starts (so passing by doesn't flicker)")]
        [Min(0f)] public float nudgeDelay = 0.6f;
        [Range(0f, 0.6f)] public float nudgeScale = 0.18f;
        [Min(0.1f)] public float nudgeSpeed = 4.5f;
        [Tooltip("Tint mixed into the chip at the top of each breath")]
        public Color nudgeColor = new Color(1f, 0.86f, 0.5f, 1f);

        // Same truth as SenseEmblems.PrinceLeads.
        static bool PrinceLeads => DuoController.I && DuoController.I.Leader && DuoController.I.Leader.sense == Sense.Dark;

        Image echoImage;
        Color echoBase = Color.white;
        float lastCall = -99f, nearSince = -1f, nudge;

        void OnEnable() => EchoCaller.Called += OnCalled;
        void OnDisable() => EchoCaller.Called -= OnCalled;
        void OnCalled(Vector2 at, float strength) => lastCall = Time.time;

        void Start()
        {
            if (echoKey) { echoImage = echoKey.GetComponent<Image>(); if (echoImage) echoBase = echoImage.color; }
            if (!princeKeys || !princessKeys) { Debug.LogError("[AbilityKeys] groups are not assigned - use the HUD prefab.", this); enabled = false; return; }
            bool prince = PrinceLeads;
            princeKeys.alpha = prince ? 1f : 0f;
            princessKeys.alpha = prince ? 0f : 1f;
        }

        void Update()
        {
            bool prince = PrinceLeads;
            float step = fadeSeconds > 0f ? Time.unscaledDeltaTime / fadeSeconds : 1f;
            princeKeys.alpha = Mathf.MoveTowards(princeKeys.alpha, prince ? 1f : 0f, step);
            princessKeys.alpha = Mathf.MoveTowards(princessKeys.alpha, prince ? 0f : 1f, step);
            Nudge(prince);
        }

        void Nudge(bool prince)
        {
            if (!echoKey) return;
            bool want = false;
            if (prince && Time.time - lastCall > nudgeQuietSeconds)
            {
                Vector2 p = (Vector2)DuoController.I.Leader.transform.position + Vector2.up * 0.5f;
                want = HiddenPlank.AnyUnfoundWithin(p, nudgeRadius) || FakeWall.AnyUnfoundWithin(p, nudgeRadius);
            }
            if (!want) nearSince = -1f;
            else if (nearSince < 0f) nearSince = Time.time;
            bool on = want && Time.time - nearSince >= nudgeDelay;
            nudge = Mathf.MoveTowards(nudge, on ? 1f : 0f, Time.unscaledDeltaTime * 3f);

            float breath = nudge * (0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * nudgeSpeed));
            echoKey.localScale = Vector3.one * (1f + nudgeScale * breath);
            if (echoImage) echoImage.color = Color.Lerp(echoBase, nudgeColor, breath);
        }
    }
}

using UnityEngine;

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

        // Same truth as SenseEmblems.PrinceLeads.
        static bool PrinceLeads => DuoController.I && DuoController.I.Leader && DuoController.I.Leader.sense == Sense.Dark;

        void Start()
        {
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
        }
    }
}

using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// The princess cannot speak: when she leads and the voice key (V) is held, a speech bubble over her head shows only
    /// "·· ·" — she tries, nothing comes out. Fires <see cref="GameEvents.SilentCall"/> + a "……" caption once per press.
    /// The prince is unaffected (the recording itself lives in VoiceInput).
    /// </summary>
    [RequireComponent(typeof(Actor))]
    public class SilentSpeech : MonoBehaviour
    {
        [Header("Refs (auto-found under this object if empty)")]
        [Tooltip("Root of the SilentBubble instance (child of the princess). Hidden until she tries to speak.")]
        public Transform bubble;
        public SpriteRenderer bubbleSprite;
        public TextMesh bubbleText;
        public SenseText bubbleSense;

        [Header("Placement")]
        [Tooltip("Bubble centre relative to the princess, in world units (not mirrored when she turns).")]
        public Vector3 offset = new Vector3(0.6f, 2.9f, 0f);
        [Tooltip("Bubble scale when fully popped in (the sprite is ~3.2m wide at 1; 0.5 = ~1.6m).")]
        public float bubbleScale = 0.5f;

        [Header("Text")]
        [Tooltip("Frames of the held bubble, cycled in order.")]
        public string[] frames = { "·", "··", "···", "……" };
        [Tooltip("Seconds per frame.")]
        public float frameTime = 0.28f;
        [Tooltip("Caption shown once per press (what a silence looks like).")]
        public string caption = "……";

        [Header("Motion")]
        public float popTime = 0.16f;
        [Tooltip("Overshoot of the pop-in scale.")]
        public float popOvershoot = 1.18f;
        [Tooltip("Rotation wobble in degrees while held.")]
        public float wobbleDegrees = 3f;
        public float wobbleSpeed = 9f;
        [Tooltip("Vertical bob in metres.")]
        public float bob = 0.04f;
        [Tooltip("Seconds the bubble lingers after release before fading.")]
        public float lingerTime = 0.35f;
        public float fadeTime = 0.3f;
        [Tooltip("Minimum seconds between SilentCall/caption triggers, so tapping V does not spam.")]
        public float pressCooldown = 0.9f;
        [Tooltip("Seconds the bubble stays up when she reaches for nothing (E with nothing in reach), before lingering and fading.")]
        public float popHoldTime = 0.6f;

        Actor actor;
        float shown;          // 0..1 pop progress
        float releaseTime = -99f;
        float lastFire = -99f;
        float holdStart;
        float popUntil = -99f;
        bool wasHeld, wasPressed;

        void Awake()
        {
            actor = GetComponent<Actor>();
            if (!bubble) { var t = transform.Find("Silent Bubble"); if (t) bubble = t; }
            if (bubble)
            {
                if (!bubbleSprite) bubbleSprite = bubble.GetComponent<SpriteRenderer>();
                if (!bubbleText) bubbleText = bubble.GetComponentInChildren<TextMesh>(true);
                if (!bubbleSense && bubbleText) bubbleSense = bubbleText.GetComponent<SenseText>();
                bubble.gameObject.SetActive(false);
            }
        }

        /// <summary>She reaches for nothing (E with nothing in reach): the same silent bubble, for a moment, no caption.</summary>
        public void Pop()
        {
            if (Time.time < popUntil) return;
            popUntil = Time.time + popHoldTime;
            if (Time.time - lastFire >= pressCooldown)
            {
                lastFire = Time.time;
                GameEvents.SilentCall?.Invoke();
            }
        }

        void Update()
        {
            bool pressed = actor && actor.IsLeader && actor.sense != Sense.Dark && GameInput.I != null
                           && GameInput.I.MicHold.IsPressed() && !GameFlow.InputLocked && !GameFlow.ModalOpen;
            bool held = pressed || Time.time < popUntil;
            if (pressed && !wasPressed && Time.time - lastFire >= pressCooldown)
            {
                lastFire = Time.time;
                GameEvents.SilentCall?.Invoke();
                GameEvents.Caption?.Invoke(caption);
            }
            wasPressed = pressed;
            if (held && !wasHeld) holdStart = Time.time;
            if (!held && wasHeld) releaseTime = Time.time;
            wasHeld = held;

            if (!bubble) return;
            float hide = held ? 0f : Mathf.Clamp01((Time.time - releaseTime - lingerTime) / Mathf.Max(0.01f, fadeTime));
            bool visible = held || hide < 1f;
            if (!visible) { shown = 0f; if (bubble.gameObject.activeSelf) bubble.gameObject.SetActive(false); return; }
            if (!bubble.gameObject.activeSelf) bubble.gameObject.SetActive(true);

            shown = held ? Mathf.Clamp01(shown + Time.deltaTime / Mathf.Max(0.01f, popTime)) : shown;
            float k = held ? Time.time - holdStart : Time.time - releaseTime;
            float s = shown < 1f ? Mathf.Lerp(0.2f, popOvershoot, shown) : Mathf.Lerp(popOvershoot, 1f, Mathf.Clamp01((Time.time - holdStart - popTime) / 0.12f));
            if (!held) s = 1f;
            float a = 1f - hide;

            float t = Time.time;
            bubble.position = transform.position + offset + Vector3.up * Mathf.Sin(t * 5f) * bob;
            bubble.rotation = Quaternion.Euler(0, 0, Mathf.Sin(t * wobbleSpeed) * wobbleDegrees);
            bubble.localScale = Vector3.one * (bubbleScale * s) / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
            if (bubbleSprite) { var c = bubbleSprite.color; c.a = a; bubbleSprite.color = c; }
            if (bubbleSense) bubbleSense.lightAlpha = a;
            if (bubbleText && frames != null && frames.Length > 0)
                bubbleText.text = frames[(int)(Mathf.Max(0f, Time.time - holdStart) / Mathf.Max(0.05f, frameTime)) % frames.Length];
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.9f, 0.6f, 0.9f);
            Gizmos.DrawWireCube(transform.position + offset, new Vector3(3.2f, 2.2f, 0f) * bubbleScale);
            Gizmos.DrawLine(transform.position, transform.position + offset);
        }
    }
}

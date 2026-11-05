using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// A plank of light that comes and goes on a rhythm. Only the princess can see it (the material is
    /// light-only), but it holds anyone's weight while it is lit — so the prince must trust her timing.
    /// It flickers just before it fades.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class LightPlatform : MonoBehaviour
    {
        [Header("Rhythm (seconds)")]
        [Tooltip("One full on/off cycle.")]
        [Min(0.1f)] public float period = 2.6f;
        [Tooltip("Lit (solid) part of the cycle.")]
        public float onTime = 1.6f;
        [Tooltip("Offset within the cycle (0..1). Step it along a row of planks to make a wave.")]
        [Range(0, 1)] public float phase;
        [Tooltip("Flicker this long before going out.")]
        public float warnTime = 0.4f;

        [Header("Look")]
        [Tooltip("Alpha of the faint outline while off.")]
        [Range(0, 1)] public float offAlpha = 0.14f;
        public float fadeInTime = 0.12f;
        public SpriteRenderer sr;
        public Renderer glow;

        BoxCollider2D col;

        public bool IsOn { get; private set; }

        void Awake() { col = GetComponent<BoxCollider2D>(); }

        void OnValidate() { onTime = Mathf.Clamp(onTime, 0f, period); warnTime = Mathf.Clamp(warnTime, 0f, onTime); }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var c = col ? col : GetComponent<BoxCollider2D>();
            if (!c) return;
            // brighter outline = lit for a larger share of the cycle
            Gizmos.color = new Color(1f, 0.85f, 0.35f, 0.25f + 0.6f * (onTime / period));
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(c.offset, c.size);
            Gizmos.matrix = Matrix4x4.identity;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, $"φ{phase:0.00}");
        }
#endif

        void Update()
        {
            float t = Mathf.Repeat(Time.time / period + phase, 1f) * period;
            IsOn = t < onTime;
            col.enabled = IsOn;
            float a;
            if (!IsOn) a = offAlpha;                                           // a faint outline of where it will be
            else if (onTime - t < warnTime) a = Mathf.PingPong(Time.time * 16f, 1f) > 0.5f ? 1f : 0.35f;
            else a = Mathf.Clamp01(t / Mathf.Max(0.001f, fadeInTime));                               // quick fade-in
            if (sr) { var c = sr.color; c.a = a; sr.color = c; }
            if (glow) glow.enabled = IsOn;
        }
    }
}

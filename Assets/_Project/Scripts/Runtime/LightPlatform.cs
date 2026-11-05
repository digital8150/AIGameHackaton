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
        public float period = 2.6f;
        public float onTime = 1.6f;
        [Range(0, 1)] public float phase;
        public float warnTime = 0.4f;
        public SpriteRenderer sr;
        public Renderer glow;

        BoxCollider2D col;

        public bool IsOn { get; private set; }

        void Awake() { col = GetComponent<BoxCollider2D>(); }

        void Update()
        {
            float t = Mathf.Repeat(Time.time / period + phase, 1f) * period;
            IsOn = t < onTime;
            col.enabled = IsOn;
            float a;
            if (!IsOn) a = 0.14f;                                           // a faint outline of where it will be
            else if (onTime - t < warnTime) a = Mathf.PingPong(Time.time * 16f, 1f) > 0.5f ? 1f : 0.35f;
            else a = Mathf.Clamp01(t / 0.12f);                               // quick fade-in
            if (sr) { var c = sr.color; c.a = a; sr.color = c; }
            if (glow) glow.enabled = IsOn;
        }
    }
}

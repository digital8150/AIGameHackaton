using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// Standing hint on a gold object: "this one answers to a voice". Slow gold motes rise off the body and a soft glow
    /// pulses behind it, fitted to the parent <see cref="StretchObject"/>'s collider every time its length changes.
    /// What the prince can actually perceive is decided per pixel by the materials' shader (DuskTwilight/FX/SenseAura:
    /// his proximity circle plus wherever an echo is lighting), so nothing leaks from afar; the princess always sees it.
    /// Hidden once the object is solved.
    /// </summary>
    [ExecuteAlways]
    public class WordAura : MonoBehaviour
    {
        [Header("References (blank = found automatically)")]
        public StretchObject target;
        [Tooltip("Rising gold motes (Box shape, resized to the body)")]
        public ParticleSystem motes;
        [Tooltip("Soft pulsing glow behind the body")]
        public SpriteRenderer glow;

        [Header("Fit")]
        [Tooltip("Motes spawn this far (m) around the body on every side")]
        public float moteMargin = 0.35f;
        [Tooltip("Glow extends this far (m) past the body on every side")]
        public Vector2 glowPadding = new Vector2(1.2f, 0.9f);

        [Header("Motes")]
        [Tooltip("Motes per second per metre of the body's longest side")]
        public float motesPerMetre = 4f;
        public float minRate = 6f;
        public float maxRate = 60f;

        [Header("Glow")]
        [Range(0f, 1f)] public float glowAlpha = 0.8f;
        public float pulseSpeed = 1.5f;
        [Range(0f, 1f)] public float pulseDepth = 0.3f;

        [Header("Fade")]
        [Tooltip("Seconds to fade the glow out / in when the object is solved")]
        public float fadeSeconds = 0.5f;

        BoxCollider2D col;
        Vector2 fitted = new Vector2(-1f, -1f);
        float shown = 1f;

        void Reset() => Find();
        void Awake() => Find();

        void Find()
        {
            if (!target) target = GetComponentInParent<StretchObject>();
            if (target && !col) col = target.GetComponent<BoxCollider2D>();
            if (!motes) motes = GetComponentInChildren<ParticleSystem>(true);
            if (!glow) glow = GetComponentInChildren<SpriteRenderer>(true);
        }

        void LateUpdate()
        {
            if (!target) { Find(); if (!target) return; }
            if (!col) col = target.GetComponent<BoxCollider2D>();
            if (!col) return;

            Bounds b = col.bounds;
            transform.position = new Vector3(b.center.x, b.center.y, transform.position.z);
            Vector2 size = b.size;
            if ((size - fitted).sqrMagnitude > 0.0001f) { Fit(size); fitted = size; }

            if (!Application.isPlaying) return;
            float goal = target.Solved ? 0f : 1f;
            shown = Mathf.MoveTowards(shown, goal, Time.deltaTime / Mathf.Max(fadeSeconds, 0.01f));

            if (glow)
            {
                var c = glow.color;
                c.a = glowAlpha * shown * (1f - pulseDepth * 0.5f + pulseDepth * 0.5f * Mathf.Sin(Time.time * pulseSpeed + transform.position.x));
                glow.color = c;
            }
            if (motes)
            {
                if (goal > 0f && !motes.isEmitting) motes.Play();
                else if (goal <= 0f && motes.isEmitting) motes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        void Fit(Vector2 size)
        {
            if (glow) glow.transform.localScale = new Vector3(size.x + glowPadding.x * 2f, size.y + glowPadding.y * 2f, 1f);
            if (!motes) return;
            var shape = motes.shape;
            shape.scale = new Vector3(size.x + moteMargin * 2f, size.y + moteMargin * 2f, 0.1f);
            var emission = motes.emission;
            emission.rateOverTime = Mathf.Clamp(motesPerMetre * Mathf.Max(size.x, size.y), minRate, maxRate);
            var main = motes.main;
            main.maxParticles = Mathf.Max(90, Mathf.CeilToInt(maxRate * main.startLifetime.constantMax * 1.2f));
        }

        void OnDrawGizmosSelected()
        {
            var bc = target ? target.GetComponent<BoxCollider2D>() : GetComponentInParent<BoxCollider2D>();
            if (!bc) return;
            Bounds b = bc.bounds;
            Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.8f);
            Gizmos.DrawWireCube(b.center, new Vector3(b.size.x + moteMargin * 2f, b.size.y + moteMargin * 2f, 0f));
        }
    }
}

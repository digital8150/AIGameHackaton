using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// A little mark (?, !) that pops up over a character's head, bobs, and fades: the princess puzzled at a wall that
    /// won't let her through. Follows <see cref="target"/>; destroys itself when done.
    /// </summary>
    public class PopMark : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0.35f, 2.25f, 0f);
        public float life = 1.8f;
        public float popTime = 0.18f;
        public float bob = 0.08f;
        SpriteRenderer sr; float t0; Vector3 baseScale;

        void Start() { sr = GetComponentInChildren<SpriteRenderer>(); t0 = Time.time; baseScale = transform.localScale; Update(); }

        void Update()
        {
            float t = Time.time - t0;
            if (target) transform.position = target.position + offset + Vector3.up * Mathf.Sin(t * 6f) * bob;
            float pop = Mathf.Clamp01(t / popTime);
            float s = pop < 1f ? Mathf.Lerp(0.2f, 1.15f, pop) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((t - popTime) / 0.12f));
            transform.localScale = baseScale * s;
            if (sr) { var c = sr.color; c.a = 1f - Mathf.Clamp01((t - (life - 0.4f)) / 0.4f); sr.color = c; }
            if (t >= life) Destroy(gameObject);
        }
    }
}

using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// 무거운 상자. Only the princess can move it: walk into it to push, hold Interact (E) and walk away to pull.
    /// The prince just strains against it. It never slides on its own (X is frozen unless she moves it), it falls
    /// into pits and comes back to where it started, and two boxes touching don't move (one at a time).
    /// Also used for the gold log (with <see cref="StretchObject"/> on the same object).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class PushBox : MonoBehaviour, IResettable
    {
        [Header("Moving")]
        [Tooltip("Metres per second while pushed or pulled (기획 제안값 3)")]
        public float moveSpeed = 3f;
        [Tooltip("How close the princess must be to its side to push or pull")]
        public float grabGap = 0.15f;
        [Tooltip("Stop sliding right of this x (log: stops at x 77). 0 = no limit")]
        public float maxX = 0f;
        public float minX = 0f;
        [Tooltip("Log: the maxX stop only holds once this object has been made long (a short log slides on into the pit)")]
        public bool stopOnlyWhenLong;
        StretchObject stretch;

        [Header("Falling")]
        [Tooltip("Below this y the box goes back to where it started")]
        public float respawnBelowY = -2f;
        public string respawnCaption = "";

        Rigidbody2D rb;
        BoxCollider2D col;
        Vector3 home;
        AudioSource sfx;
        public AudioClip scrapeClip;

        /// <summary>Being pushed or pulled this physics step.</summary>
        public bool Moving { get; private set; }

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<BoxCollider2D>();
            home = transform.position;
            stretch = GetComponent<StretchObject>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.mass = 50f;
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false; sfx.loop = true; sfx.volume = 0.35f;
        }

        void FixedUpdate()
        {
            Moving = false;
            float dir = 0f;
            var duo = DuoController.I;
            if (duo && !GameFlow.InputLocked)
            {
                var a = duo.Leader;
                int side = Side(a);   // -1: actor on the left of the box, +1: on the right, 0: not beside it
                if (side != 0 && a.Grounded)
                {
                    int input = a.InputDir;
                    bool pushing = input == -side;              // walking into the box
                    bool pulling = input == side && a.InteractHeld;
                    if (pushing || pulling)
                    {
                        a.MarkPushing();
                        if (a == duo.princess)
                        {
                            dir = pushing ? -side : side;
                            if (Blocked(dir)) dir = 0f;
                            a.SpeedScale = moveSpeed / Mathf.Max(0.1f, a.runSpeed);
                        }
                    }
                }
            }
            bool limit = maxX > 0 && (!stopOnlyWhenLong || !stretch || stretch.Length >= stretch.longLength - 0.01f);
            if (dir != 0f && (!limit || dir < 0 || transform.position.x < maxX) && (minX <= 0 || dir > 0 || transform.position.x > minX))
            {
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                rb.linearVelocity = new Vector2(dir * moveSpeed, rb.linearVelocity.y);
                Moving = true;
            }
            else
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
            }
            if (scrapeClip)
            {
                if (Moving && !sfx.isPlaying) { sfx.clip = scrapeClip; sfx.Play(); }
                else if (!Moving && sfx.isPlaying) sfx.Stop();
            }
            if (transform.position.y < respawnBelowY) ResetState();
        }

        /// <summary>Which side of the box the actor is standing against (0 = neither).</summary>
        int Side(Actor a)
        {
            var b = col.bounds;
            var ac = a.GetComponent<Collider2D>();
            if (!ac) return 0;
            var ab = ac.bounds;
            // feet roughly on the box's floor level, body overlapping its height
            if (ab.min.y > b.max.y - 0.2f || ab.max.y < b.min.y + 0.1f) return 0;
            if (Mathf.Abs(ab.max.x - b.min.x) < grabGap + 0.05f) return -1;
            if (Mathf.Abs(ab.min.x - b.max.x) < grabGap + 0.05f) return 1;
            return 0;
        }

        // another box (or a wall) right against the side we'd move into
        bool Blocked(float dir)
        {
            var b = col.bounds;
            var hits = Physics2D.OverlapBoxAll(new Vector2(dir > 0 ? b.max.x + 0.06f : b.min.x - 0.06f, b.center.y + 0.05f),
                                               new Vector2(0.08f, b.size.y * 0.8f), 0f);
            foreach (var h in hits)
                if (h && h != col && h.attachedRigidbody != rb && !h.isTrigger && h.GetComponentInParent<Actor>() == null)
                    return true;
            return false;
        }

        public void ResetState()
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = home;
            transform.position = home;
            if (!string.IsNullOrEmpty(respawnCaption)) GameEvents.Caption?.Invoke(respawnCaption);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.7f, 0.3f, 0.7f);
            var p = transform.position;
            if (maxX > 0) Gizmos.DrawLine(new Vector3(maxX, p.y - 1), new Vector3(maxX, p.y + 2));
            if (minX > 0) Gizmos.DrawLine(new Vector3(minX, p.y - 1), new Vector3(minX, p.y + 2));
        }
#endif
    }
}

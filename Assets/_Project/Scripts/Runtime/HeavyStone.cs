using System.Collections;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// 무거워져. A gold stone floating in the air. Within <see cref="hearDistance"/> of it the prince says something
    /// like "무거워져" / "떨어져라" / "내려와" (judged by <see cref="WordJudge.JudgeHeavyAsync"/>) and it drops onto
    /// whatever is below (spikes, a pit) and stays there as a stepping stone. Anyone standing where it lands is pushed
    /// aside, never crushed. The object's position is the bottom centre of the stone.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class HeavyStone : WordTarget
    {
        [Header("Shape")]
        public Vector2 size = new Vector2(3f, 1.5f);
        [Tooltip("Body sprite (centre pivot), scaled to the block")]
        public SpriteRenderer body;
        [Tooltip("Picture size relative to the block (the drawing has soft edges)")]
        public Vector2 visualScale = new Vector2(1.08f, 1.15f);

        [Header("Fall")]
        [Tooltip("Downward acceleration (m/s²). 28 ≈ 3.5m in half a second.")]
        public float fallGravity = 28f;
        [Tooltip("Furthest it can drop (m) when nothing is below")]
        public float maxDrop = 20f;
        [Tooltip("Dust where it lands (e.g. FX/BoxFallDust)")]
        public GameObject landFx;
        public float landShake = 0.45f;

        [Header("Speech")]
        [Tooltip("The prince is heard up to this far from the stone's nearest point (기획 8m)")]
        public float hearDistance = 8f;
        public string fallCaption = "쿵";
        public string noneCaption = "……";
        public AudioClip landClip;

        public static string LastJudge { get; private set; } = "";
        public bool Fallen { get; private set; }
        public override bool AcceptsAnyText => true;

        BoxCollider2D col;
        bool judging;

        protected override void Awake()
        {
            base.Awake();
            col = GetComponent<BoxCollider2D>();
            Apply();
        }

        void Apply()
        {
            if (!col) col = GetComponent<BoxCollider2D>();
            col.size = size;
            col.offset = new Vector2(0f, size.y * 0.5f);
            if (body)
            {
                // one picture of a stone, stretched to the block's size
                body.drawMode = SpriteDrawMode.Simple;
                var b = body.sprite ? body.sprite.bounds.size : Vector3.one;
                body.transform.localScale = new Vector3(size.x / Mathf.Max(0.01f, b.x) * visualScale.x, size.y / Mathf.Max(0.01f, b.y) * visualScale.y, 1f);
                body.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            }
        }

        protected override float ReachScore(Vector3 leader)
        {
            if (Fallen || !col) return -1f;
            Vector2 p = leader + Vector3.up * 0.5f;
            float d = Vector2.Distance(p, col.ClosestPoint(p));
            return d <= hearDistance ? d : -1f;
        }

        protected override void OnSpoken(string text)
        {
            if (judging || Fallen) return;
            StartCoroutine(Judge(text));
        }

        IEnumerator Judge(string text)
        {
            judging = true;
            var letters = Letters(text, DuoController.I ? DuoController.I.Leader.transform.position + Vector3.up * 0.3f : transform.position);
            var task = WordJudge.JudgeHeavyAsync(text);
            while (!task.IsCompleted) yield return null;
            var r = task.Result;
            LastJudge = $"\"{text}\" → {(r.stretch == StretchKind.Short ? "무겁게" : "관계없음")} ({r.confidence:P0}, {r.source})";
            judging = false;
            bool heavy = r.stretch == StretchKind.Short;
            Vector3 into = col.bounds.center;
            for (int i = 0; i < letters.Count; i++)
                StartCoroutine(heavy ? Fly(letters[i], into, letterStagger * i, solid: false, vanishOnArrival: true) : Fall(letters[i], 0.1f * i));
            GameEvents.PuzzleAttempt?.Invoke("heavy", heavy);
            if (!heavy) { GameEvents.Caption?.Invoke(noneCaption); yield break; }
            yield return new WaitForSeconds(letterStagger * letters.Count + letterFlyTime * 0.8f);
            yield return Drop();
        }

        /// <summary>How far the stone can fall before it rests on something solid.</summary>
        float FreeDrop()
        {
            var hits = new RaycastHit2D[16];
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
            int n = Physics2D.BoxCast(col.bounds.center, size * new Vector2(0.96f, 0.98f), 0f, Vector2.down, filter, hits, maxDrop);
            float best = maxDrop;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider == col || h.collider.GetComponentInParent<Actor>()) continue;
                best = Mathf.Min(best, h.distance);
            }
            return best;
        }

        IEnumerator Drop()
        {
            Fallen = true;
            float drop = FreeDrop();
            float startY = transform.position.y, targetY = startY - drop;
            SenseController.I?.EmitEcho(col.bounds.center, 0.4f);
            float v = 0f, y = startY;
            while (y > targetY)
            {
                v += fallGravity * Time.deltaTime;
                y = Mathf.Max(targetY, y - v * Time.deltaTime);
                transform.position = new Vector3(transform.position.x, y, transform.position.z);
                ShoveAside();
                yield return null;
            }
            var bottom = new Vector3(transform.position.x, targetY, 0f);
            GameEvents.Caption?.Invoke(fallCaption);
            if (landClip) sfx.PlayLimited(landClip, 0.9f);
            CameraRig.I?.Kick(landShake);
            SenseController.I?.EmitEcho(bottom, 0.9f);
            if (landFx) Destroy(Instantiate(landFx, bottom, Quaternion.identity), 4f);
        }

        /// <summary>Whoever is under the falling stone steps out to the nearer side instead of being crushed.</summary>
        void ShoveAside()
        {
            var duo = DuoController.I;
            if (!duo) return;
            var b = col.bounds;
            foreach (var a in new[] { duo.prince, duo.princess })
            {
                if (!a) continue;
                var ap = a.transform.position;
                if (ap.x < b.min.x - 0.3f || ap.x > b.max.x + 0.3f) continue;
                if (ap.y > b.center.y || ap.y + 1.6f < b.min.y) continue;   // above it (riding) or well below
                float nx = ap.x < b.center.x ? b.min.x - 0.45f : b.max.x + 0.45f;
                var np = new Vector2(nx, ap.y);
                var rb = a.GetComponent<Rigidbody2D>();
                if (rb) rb.position = np;
                a.transform.position = np;
            }
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmos()
        {
            var p = transform.position;
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            Gizmos.DrawWireCube(p + Vector3.up * size.y * 0.5f, size);
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.25f);
            Gizmos.DrawWireSphere(p + Vector3.up * size.y * 0.5f, hearDistance);
        }

        void OnValidate() { if (!Application.isPlaying) Apply(); }
#endif
    }
}

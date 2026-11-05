using System.Collections;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// 무거워져. A gold stone floating in the air. Within <see cref="hearDistance"/> of it the prince says something
    /// like "무거워져" / "떨어져라" / "내려와" (judged by <see cref="WordJudge.JudgeHeavyAsync"/>) and it drops onto
    /// whatever is below and stays there as a stepping stone. Anyone standing where it lands is pushed aside, never
    /// crushed. Once down it keeps resting on whatever holds it: if that goes away (a bridge told to shrink) it falls
    /// again. Optional (5단계): landing on spikes / in a pit sends it back home after a moment; a box on the plate
    /// below keeps it from falling (it only shakes); it can stay hidden inside a fake ceiling until the prince finds it.
    /// The object's position is the bottom centre of the stone.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class HeavyStone : WordTarget, IResettable
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

        [Header("Coming back (5단계)")]
        [Tooltip("Landing on a hazard (spikes) or dropping maxDrop with nothing below sends it home after this many seconds. 0 = it stays wherever it lands (4단계: on the spikes).")]
        public float returnAfter = 0f;
        [Tooltip("A box in the way below (e.g. on the pressure plate) stops it: it only shakes")]
        public bool boxBlocks;
        public string blockedCaption = "덜컹";

        [Header("Hidden (5단계)")]
        [Tooltip("Invisible and deaf while a fake wall / ceiling is right under it; shows up once the prince finds that.")]
        public bool hiddenUntilFound;
        [Tooltip("How far below the stone a fake tile still hides it (m)")]
        public float hideDepth = 3f;

        [Header("Floating look")]
        [Tooltip("While it floats the picture bobs gently (the collider stays put) so it reads as floating, not stuck")]
        public float bobHeight = 0.18f;
        public float bobSpeed = 1.6f;
        [Tooltip("Golden motes drifting around it while it floats (stopped when it falls)")]
        public ParticleSystem floatFx;
        [Tooltip("Soft glow behind it while it floats (pulses)")]
        public SpriteRenderer floatGlow;
        public float glowAlpha = 0.45f;

        [Header("Speech")]
        [Tooltip("The prince is heard up to this far from the stone's nearest point (기획 8m)")]
        public float hearDistance = 8f;
        public string fallCaption = "쿵";
        public string noneCaption = "……";
        public AudioClip landClip;

        public static string LastJudge { get; private set; } = "";
        public bool Fallen { get; private set; }
        public bool Hidden { get; private set; }
        public override bool AcceptsAnyText => true;

        BoxCollider2D col;
        bool judging, dropping;
        Vector3 home;
        Coroutine returning;
        readonly RaycastHit2D[] hits = new RaycastHit2D[16];

        protected override void Awake()
        {
            base.Awake();
            col = GetComponent<BoxCollider2D>();
            home = transform.position;
            Apply();
        }

        void Start()
        {
            if (hiddenUntilFound) SetHidden(FakeAbove());
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

        bool FakeAbove()
        {
            var b = col.bounds;
            for (float y = b.min.y - 0.5f; y > b.min.y - hideDepth; y -= 0.5f)
                for (float x = b.min.x + 0.25f; x < b.max.x; x += 0.5f)
                    if (FakeWall.SolidAt(new Vector2(x, y))) return true;
            return false;
        }

        void SetHidden(bool h)
        {
            Hidden = h;
            if (body) body.enabled = !h;
        }

        protected override void Update()
        {
            base.Update();
            bool floating = !Fallen && !dropping && !Hidden;
            if (body)
            {
                float bob = floating ? Mathf.Sin(Time.time * bobSpeed + home.x) * bobHeight : 0f;
                body.transform.localPosition = new Vector3(0f, size.y * 0.5f + bob, 0f);
            }
            if (floatFx)
            {
                if (floating && !floatFx.isEmitting) floatFx.Play();
                else if (!floating && floatFx.isEmitting) floatFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (floatGlow)
            {
                var c = floatGlow.color;
                float target = floating ? glowAlpha * (0.75f + 0.25f * Mathf.Sin(Time.time * bobSpeed * 1.3f + home.x)) : 0f;
                c.a = Mathf.MoveTowards(c.a, target, Time.deltaTime * 2f);
                floatGlow.color = c;
            }
            if (Hidden && !FakeAbove())
            {
                SetHidden(false);
                SenseController.I?.EmitEcho(col.bounds.center, 0.4f);
            }
        }

        void FixedUpdate()
        {
            // resting on something that went away (a bridge told to shrink): fall again
            if (Fallen && !dropping && returning == null && FreeDrop(out _) > 0.05f) StartCoroutine(Drop());
        }

        protected override float ReachScore(Vector3 leader)
        {
            if (Fallen || Hidden || !col) return -1f;
            Vector2 p = leader + Vector3.up * 0.5f;
            float d = Vector2.Distance(p, col.ClosestPoint(p));
            return d <= hearDistance ? d : -1f;
        }

        protected override bool Understands(string text) => !WordJudge.LooksStretch(text);
        public override System.Collections.Generic.IEnumerable<string> ListenWords() => WordJudge.HeavyListenWords;

        protected override void OnSpoken(string text)
        {
            if (judging || Fallen || Hidden) return;
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
            if (boxBlocks && BoxBelow()) { yield return Shake(); yield break; }
            Fallen = true;
            yield return Drop();
        }

        /// <summary>How far the stone can fall before it rests on something solid (actors don't count).</summary>
        float FreeDrop(out Collider2D under)
        {
            under = null;
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
            int n = Physics2D.BoxCast(col.bounds.center, size * new Vector2(0.96f, 0.98f), 0f, Vector2.down, filter, hits, maxDrop);
            float best = maxDrop;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider == col || h.collider.GetComponentInParent<Actor>()) continue;
                if (h.distance < best) { best = h.distance; under = h.collider; }
            }
            return best;
        }

        bool BoxBelow()
        {
            FreeDrop(out var under);
            return under && under.GetComponentInParent<PushBox>();
        }

        IEnumerator Shake()
        {
            GameEvents.Caption?.Invoke(blockedCaption);
            CameraRig.I?.Kick(0.15f);
            var p = transform.position;
            for (float t = 0; t < 0.5f; t += Time.deltaTime)
            {
                transform.position = p + Vector3.right * Mathf.Sin(t * 60f) * 0.08f * (1f - t / 0.5f);
                yield return null;
            }
            transform.position = p;
        }

        IEnumerator Drop()
        {
            dropping = true;
            float drop = FreeDrop(out _);
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
            var duo = DuoController.I;
            if (duo)
            {
                var fb = col.bounds; var fp = duo.Follower.transform.position;
                if (fp.x > fb.min.x - 0.3f && fp.x < fb.max.x + 0.3f && fp.y < fb.max.y - 0.1f && fp.y + 1.6f > fb.min.y) duo.RegroupFollower();
            }
            GameEvents.Caption?.Invoke(fallCaption);
            if (landClip) sfx.PlayLimited(landClip, 0.9f);
            CameraRig.I?.Kick(landShake);
            SenseController.I?.EmitEcho(bottom, 0.9f);
            if (landFx) Destroy(Instantiate(landFx, bottom, Quaternion.identity), 4f);
            dropping = false;
            if (returnAfter > 0f && (drop >= maxDrop - 0.01f || OnHazard())) returning = StartCoroutine(ReturnHome());
        }

        bool OnHazard()
        {
            var b = col.bounds;
            foreach (var c in Physics2D.OverlapBoxAll(new Vector2(b.center.x, b.min.y), new Vector2(b.size.x * 0.9f, 0.6f), 0f))
                if (c.GetComponent<Hazard>()) return true;
            return false;
        }

        IEnumerator ReturnHome()
        {
            yield return new WaitForSeconds(returnAfter);
            ResetState();
            GameEvents.Caption?.Invoke("스르르");
        }

        public void ResetState()
        {
            StopAllCoroutines();
            judging = dropping = false;
            returning = null;
            Fallen = false;
            transform.position = home;
            if (hiddenUntilFound) SetHidden(FakeAbove());
            SenseController.I?.EmitEcho(col.bounds.center, 0.4f);
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

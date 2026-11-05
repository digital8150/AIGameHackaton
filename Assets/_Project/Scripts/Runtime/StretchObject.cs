using System.Collections;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// 말로 길이 바꾸기. A gold object (bridge, pillar, log) the prince can talk into growing or shrinking: within
    /// <see cref="hearDistance"/> of its nearest point, he says something like "길어져" / "짧아져" (or "엄청 길쭉해져랏")
    /// and the local model (<see cref="WordJudge.JudgeStretchAsync"/>) decides long / short / neither. The length
    /// changes over <see cref="changeTime"/> and stays until he speaks again. The princess can't — she can't hear
    /// her own words. Whoever stands on its far end rides along.
    /// The object's position is the fixed end; the body grows toward <see cref="direction"/>.
    /// </summary>
    public class StretchObject : WordTarget
    {
        public enum Direction { Right, Left, Up }

        [Header("Shape")]
        public Direction direction = Direction.Right;
        [Tooltip("Length when short / long (m)")]
        public float shortLength = 2f;
        public float longLength = 10f;
        public bool startsLong;
        [Tooltip("Thickness across the length (m)")]
        public float thickness = 0.5f;
        [Tooltip("Body sprite (Tiled draw mode, repeats along the length)")]
        public SpriteRenderer body;

        [Header("Speech")]
        [Tooltip("The prince is heard up to this far from the object's nearest point (기획 8m)")]
        public float hearDistance = 8f;
        public float changeTime = 1f;
        public string longCaption = "쭈욱";
        public string shortCaption = "쏙";
        public string noneCaption = "……";

        /// <summary>Last judgement, for the F1 overlay.</summary>
        public static string LastJudge { get; private set; } = "";

        public float Length { get; private set; }
        public override bool AcceptsAnyText => true;

        BoxCollider2D col;
        bool judging;

        protected override void Awake()
        {
            base.Awake();
            col = GetComponent<BoxCollider2D>();
            if (!col) col = gameObject.AddComponent<BoxCollider2D>();
            Length = startsLong ? longLength : shortLength;
            Apply(Length);
        }

        protected override float ReachScore(Vector3 leader)
        {
            if (!col) return -1f;
            float d = Vector2.Distance(leader + Vector3.up * 0.5f, col.ClosestPoint(leader + Vector3.up * 0.5f));
            return d <= hearDistance ? d : -1f;
        }

        protected override void OnSpoken(string text)
        {
            if (judging) return;
            StartCoroutine(Judge(text));
        }

        IEnumerator Judge(string text)
        {
            judging = true;
            var letters = Letters(text, DuoController.I ? DuoController.I.Leader.transform.position + Vector3.up * 0.3f : transform.position);
            var task = WordJudge.JudgeStretchAsync(text);
            while (!task.IsCompleted) yield return null;
            var r = task.Result;
            LastJudge = $"\"{text}\" → {r.stretch} ({r.confidence:P0}, {r.source})";
            judging = false;
            // the words fly into the gold and vanish, or fall if they meant nothing to it
            bool change = r.stretch != StretchKind.None;
            Vector3 into = col ? (Vector3)col.bounds.center : transform.position;
            for (int i = 0; i < letters.Count; i++)
                StartCoroutine(change ? Fly(letters[i], into, letterStagger * i, solid: false, vanishOnArrival: true) : Fall(letters[i], 0.1f * i));
            if (!change)
            {
                GameEvents.Caption?.Invoke(noneCaption);
                GameEvents.PuzzleAttempt?.Invoke("stretch", false);
                yield break;
            }
            yield return new WaitForSeconds(letterStagger * letters.Count + letterFlyTime * 0.8f);
            float target = r.stretch == StretchKind.Long ? longLength : shortLength;
            GameEvents.PuzzleAttempt?.Invoke("stretch", true);
            if (Mathf.Approximately(target, Length)) yield break;
            GameEvents.Caption?.Invoke(r.stretch == StretchKind.Long ? longCaption : shortCaption);
            if (bridgeClip) sfx.PlayOneShot(bridgeClip, 0.8f);
            SenseController.I?.EmitEcho(into, 0.6f);
            float from = Length;
            for (float t = 0; t < 1f; t += Time.deltaTime / Mathf.Max(0.05f, changeTime))
            {
                SetLength(Mathf.Lerp(from, target, Mathf.SmoothStep(0, 1, t)));
                yield return null;
            }
            SetLength(target);
        }

        void SetLength(float len)
        {
            float before = Length;
            // carry whoever stands on the growing end (pillars lift the prince up)
            var duo = DuoController.I;
            if (direction == Direction.Up && duo && col)
            {
                var a = duo.Leader;
                var b = col.bounds;
                var ap = a.transform.position;
                if (ap.x > b.min.x - 0.25f && ap.x < b.max.x + 0.25f && Mathf.Abs(ap.y - b.max.y) < 0.3f)
                {
                    var arb = a.GetComponent<Rigidbody2D>();
                    var np = (Vector2)ap + Vector2.up * (len - before);
                    if (arb) arb.position = np;
                    a.transform.position = np;
                }
            }
            Length = len;
            Apply(len);
        }

        void Apply(float len)
        {
            Vector2 size, offset;
            switch (direction)
            {
                case Direction.Up: size = new Vector2(thickness, len); offset = new Vector2(0, len * 0.5f); break;
                case Direction.Left: size = new Vector2(len, thickness); offset = new Vector2(-len * 0.5f, -thickness * 0.5f); break;
                default: size = new Vector2(len, thickness); offset = new Vector2(len * 0.5f, -thickness * 0.5f); break;
            }
            if (col) { col.size = size; col.offset = offset; }
            if (body)
            {
                body.drawMode = SpriteDrawMode.Tiled;
                body.size = size;
                body.transform.localPosition = offset;
            }
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmos()
        {
            var p = transform.position;
            Vector3 d = direction == Direction.Up ? Vector3.up : direction == Direction.Left ? Vector3.left : Vector3.right;
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            Gizmos.DrawLine(p, p + d * shortLength);
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.35f);
            Gizmos.DrawLine(p + d * shortLength, p + d * longLength);
            Gizmos.DrawWireSphere(p + d * (startsLong ? longLength : shortLength), 0.2f);
        }

        void OnValidate()
        {
            if (!Application.isPlaying)
            {
                col = GetComponent<BoxCollider2D>();
                Length = startsLong ? longLength : shortLength;
                Apply(Length);
            }
        }
#endif
    }
}

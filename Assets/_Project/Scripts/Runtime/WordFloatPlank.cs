using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// 말 무게 발판. The princess says a word; its letters gather into one plank of light at the slot, and once someone
    /// stands on it the plank floats up, stays or sinks by how heavy the thing the word names is (word list first,
    /// then the local language model — <see cref="WordJudge"/>). A new word replaces the old plank.
    /// A carved plank shows a fixed word from the start (teaching by example) and cannot be spoken to.
    /// </summary>
    public class WordFloatPlank : WordTarget, IResettable
    {
        [Header("Plank")]
        [Tooltip("Where the plank forms (centre of its top). Lines in the Scene view show where each weight stops.")]
        public Transform slot;
        public WordLexicon lexicon;
        [Tooltip("Stop height for a light word, relative to the slot")]
        public float lightRise = 4.5f;
        [Tooltip("Stop height for an ordinary word")]
        public float mediumRise = 0f;
        [Tooltip("Stop height for a heavy word (below the water)")]
        public float heavySink = -4f;
        [Tooltip("Metres per second")]
        public float speed = 1.5f;
        [Tooltip("Starts moving once the leader stands on it; otherwise after departDelay")]
        public bool moveWhenRidden = true;
        public float departDelay = 1.5f;
        [Tooltip("Extra width beyond the letters")]
        public float padding = 0.3f;

        [Header("Carved plank (shown from the start, cannot be spoken to)")]
        public string carvedWord = "";
        public WordWeight carvedWeight = WordWeight.None;

        [Header("Captions (what the princess sees instead of the sound)")]
        public string lightCaption = "둥실";
        public string mediumCaption = "출렁";
        public string heavyCaption = "풍덩";
        public string noneCaption = "……";

        /// <summary>Last judgement, for the F1 overlay.</summary>
        public static string LastJudge { get; private set; } = "";

        GameObject plank;
        Rigidbody2D body;
        BoxCollider2D col;
        readonly List<GameObject> loose = new List<GameObject>();
        float targetY, madeAt;
        bool moving, judging;

        bool Carved => !string.IsNullOrEmpty(carvedWord);

        /// <summary>Any word can become a plank: speech goes straight here (not matched against the choices).</summary>
        public override bool AcceptsAnyText => !Carved;

        void Start()
        {
            if (!slot) slot = transform;
            if (Carved)
            {
                Solved = true;   // never the nearest speakable target
                Build(carvedWord, carvedWeight, instant: true);
            }
        }

        /// <summary>Words worth priming the speech recogniser with: the choices plus the word list.</summary>
        public override IEnumerable<string> ListenWords() => PromptWords();
        public IEnumerable<string> PromptWords()
        {
            if (choices != null) foreach (var c in choices) yield return c;
            if (lexicon) foreach (var w in lexicon.Words) yield return w;
        }

        protected override void OnSpoken(string text)
        {
            if (judging || Carved) return;
            StartCoroutine(Judge(text));
        }

        IEnumerator Judge(string text)
        {
            judging = true;
            var letters = Letters(text, ReachCenter);
            loose.AddRange(letters);
            var task = WordJudge.JudgeWeightAsync(text, lexicon);
            float t0 = Time.time;
            while (!task.IsCompleted)
            {
                // the words circle above the slot while the judgement is made
                for (int i = 0; i < letters.Count; i++)
                    if (letters[i]) letters[i].transform.position += Vector3.up * Mathf.Sin((Time.time - t0) * 5f + i) * 0.004f;
                yield return null;
            }
            var r = task.Result;
            LastJudge = $"\"{text}\" → {r.weight} ({r.confidence:P0}, {r.source})";
            judging = false;
            if (r.weight == WordWeight.None)
            {
                if (wrongClip) sfx.PlayLimited(wrongClip, 0.7f);
                GameEvents.Caption?.Invoke(noneCaption);
                GameEvents.PuzzleAttempt?.Invoke("weight", false);
                for (int i = 0; i < letters.Count; i++) StartCoroutine(Fall(letters[i], 0.1f * i));
                foreach (var l in letters) loose.Remove(l);
                yield break;
            }
            GameEvents.PuzzleAttempt?.Invoke("weight", true);
            ClearPlank();
            foreach (var l in letters) loose.Remove(l);
            yield return BuildFrom(letters, r.weight);
        }

        float RiseFor(WordWeight w) => w == WordWeight.Light ? lightRise : w == WordWeight.Heavy ? heavySink : mediumRise;
        string CaptionFor(WordWeight w) => w == WordWeight.Light ? lightCaption : w == WordWeight.Heavy ? heavyCaption : mediumCaption;

        GameObject MakeRoot(int letterCount, Vector3 at)
        {
            var root = new GameObject("Word Plank") { layer = gameObject.layer };
            root.transform.position = at;
            body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            col = root.AddComponent<BoxCollider2D>();
            col.size = new Vector2(letterCount * letterSpawn.x + padding, letterSize.y);
            col.offset = new Vector2(0, plankOffsetY);
            col.enabled = false;
            var eff = root.AddComponent<PlatformEffector2D>();   // one-way like every other platform
            eff.useOneWay = true; eff.surfaceArc = 160f;
            col.usedByEffector = true;
            return root;
        }

        IEnumerator BuildFrom(List<GameObject> letters, WordWeight weight)
        {
            plank = MakeRoot(letters.Count, slot.position);
            float x0 = -(letters.Count - 1) * letterSpawn.x * 0.5f;
            for (int i = 0; i < letters.Count; i++)
                StartCoroutine(Fly(letters[i], slot.position + new Vector3(x0 + i * letterSpawn.x, 0, 0), letterStagger * i, solid: false));
            yield return new WaitForSeconds(letterStagger * letters.Count + letterFlyTime + 0.05f);
            if (!plank) yield break;
            foreach (var l in letters)
            {
                if (!l) continue;
                l.transform.SetParent(plank.transform, true);
                var p = l.transform.Find("Plank");
                if (p) p.gameObject.SetActive(true);
            }
            if (bridgeClip) sfx.PlayLimited(bridgeClip, 0.7f);
            SenseController.I?.EmitEcho(slot.position, 0.4f);
            GameEvents.Caption?.Invoke(CaptionFor(weight));
            col.enabled = true;
            targetY = slot.position.y + RiseFor(weight);
            moving = false;
            madeAt = Time.time;
        }

        void Build(string word, WordWeight weight, bool instant)
        {
            var letters = Letters(word, slot.position);
            plank = MakeRoot(letters.Count, slot.position);
            float x0 = -(letters.Count - 1) * letterSpawn.x * 0.5f;
            for (int i = 0; i < letters.Count; i++)
            {
                letters[i].transform.SetParent(plank.transform, false);
                letters[i].transform.localPosition = new Vector3(x0 + i * letterSpawn.x, 0, 0);
                var p = letters[i].transform.Find("Plank");
                if (p) p.gameObject.SetActive(true);
            }
            col.enabled = true;
            targetY = slot.position.y + RiseFor(weight);
            if (instant) plank.transform.position = new Vector3(slot.position.x, targetY, slot.position.z);
            moving = !instant && !moveWhenRidden;
        }

        bool Ridden()
        {
            var duo = DuoController.I;
            if (!duo || !col || !col.enabled) return false;
            var l = duo.Leader;
            var b = col.bounds;
            float feet = l.transform.position.y;
            return l.Grounded && l.transform.position.x > b.min.x - 0.2f && l.transform.position.x < b.max.x + 0.2f && Mathf.Abs(feet - b.max.y) < 0.3f;
        }

        void FixedUpdate()
        {
            if (!plank || !body) return;
            if (!moving && (Ridden() || (!moveWhenRidden && Time.time - madeAt > departDelay))) moving = true;
            if (!moving) return;
            var p = body.position;
            float y = Mathf.MoveTowards(p.y, targetY, speed * Time.fixedDeltaTime);
            if (!Mathf.Approximately(y, p.y)) body.MovePosition(new Vector2(p.x, y));
        }

        void ClearPlank()
        {
            if (!plank) return;
            var old = plank;
            plank = null; body = null; col = null;
            foreach (var st in old.GetComponentsInChildren<SenseText>()) StartCoroutine(FadeOut(st.gameObject, 0.4f));
            Destroy(old, 0.5f);
            var c = old.GetComponent<Collider2D>();
            if (c) c.enabled = false;
        }

        public void ResetState()
        {
            if (Carved) return;
            StopAllCoroutines();
            judging = false;
            foreach (var l in loose) if (l) Destroy(l);
            loose.Clear();
            if (plank) Destroy(plank);
            plank = null;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (plank) Destroy(plank);
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmos()
        {
            if (!Carved) base.OnDrawGizmos();
            var s = slot ? slot.position : transform.position;
            float w = 2.4f;
            void Line(float dy, Color c) { Gizmos.color = c; Gizmos.DrawLine(s + new Vector3(-w / 2, dy), s + new Vector3(w / 2, dy)); }
            Line(lightRise, new Color(1f, 0.95f, 0.6f));
            Line(mediumRise, new Color(0.9f, 0.8f, 0.6f));
            Line(heavySink, new Color(0.5f, 0.6f, 0.9f));
            Gizmos.color = new Color(1, 1, 1, 0.3f);
            Gizmos.DrawLine(s + new Vector3(0, heavySink), s + new Vector3(0, lightRise));
        }
#endif
    }
}

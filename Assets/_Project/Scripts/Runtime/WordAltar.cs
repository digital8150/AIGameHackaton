using System.Collections;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// 빛글씨 with one right answer. Say it (or pick it) and either
    ///  - Bridge: its letters fly to the slots and harden into planks (해질녘, 호수), or
    ///  - Door: the letters fly into the door and it opens (반대말 문 — say the opposite of what is written).
    /// A wrong word crumbles; in Door mode the door also rattles shut a little tighter.
    /// </summary>
    public class WordAltar : WordTarget
    {
        public enum Outcome { Bridge, Door }

        [Header("Answer")]
        [Tooltip("The word that solves it. One letter per slot in Bridge mode.")]
        public string answer = "해질녘";
        [Tooltip("Caption when a wrong word crumbles.")]
        public string wrongCaption = "……";
        [Tooltip("The carved / reflected / written word the princess reads (may differ from the answer).")]
        public SenseText inscription;
        public Outcome outcome = Outcome.Bridge;

        [Header("Bridge")]
        [Tooltip("Where each letter settles (left → right). Gold boxes in the Scene view.")]
        public Transform[] slots;

        [Header("Door")]
        [Tooltip("Door mode: the door that opens (keeps its collider until it has risen out of the way).")]
        public Transform door;
        [Tooltip("How far the door rises when it opens.")]
        public float doorRise = 4.5f;
        public float doorTime = 1.1f;
        [Tooltip("Wrong word: the door shakes and sinks by this much (shuts tighter).")]
        public float wrongSink = 0.08f;
        public string wrongDoorCaption = "덜컹";
        [Tooltip("Door mode: wrong words that only scatter (e.g. the reflection read as it looks) — the door doesn't rattle")]
        public string[] scatterWords;

        [Header("Also on solve")]
        [Tooltip("Set active when solved, rising up from below (e.g. light planks over a lake). Inactive until then.")]
        public GameObject[] reveal;
        public float revealRise = 1.5f;
        public float revealTime = 0.9f;
        [Tooltip("Seconds between revealed objects")]
        public float revealStagger = 0.15f;

        void Start()
        {
            if (reveal != null) foreach (var g in reveal) if (g) g.SetActive(false);
        }

        IEnumerator Reveal()
        {
            if (reveal == null) yield break;
            for (int i = 0; i < reveal.Length; i++)
            {
                if (!reveal[i]) continue;
                StartCoroutine(Rise(reveal[i].transform));
                yield return new WaitForSeconds(revealStagger);
            }
        }

        IEnumerator Rise(Transform t)
        {
            Vector3 b = t.localPosition, a = b + Vector3.down * revealRise;
            t.localPosition = a;
            t.gameObject.SetActive(true);
            SenseController.I?.EmitEcho(t.position, 0.3f);
            for (float k = 0; k < 1; k += Time.deltaTime / Mathf.Max(0.05f, revealTime))
            {
                t.localPosition = Vector3.Lerp(a, b, Mathf.SmoothStep(0, 1, k));
                yield return null;
            }
            t.localPosition = b;
        }

        protected override void OnSpoken(string word)
        {
            bool ok = word == answer;
            GameEvents.PuzzleAttempt?.Invoke(ok ? "word" : "word-wrong", ok);
            StartCoroutine(ok ? Solve(word) : Crumble(word));
        }

        IEnumerator Solve(string word)
        {
            Solved = true;
            if (bridgeClip) sfx.PlayOneShot(bridgeClip, 0.9f);
            StartCoroutine(Reveal());
            SenseController.I?.EmitEcho(transform.position, 1.2f);
            var letters = Letters(word, transform.position);
            if (outcome == Outcome.Bridge && slots != null && slots.Length > 0)
            {
                for (int i = 0; i < letters.Count; i++)
                    StartCoroutine(Fly(letters[i], slots[Mathf.Min(i, slots.Length - 1)].position, letterStagger * i, solid: true));
                yield return new WaitForSeconds(letterStagger * letters.Count + letterFlyTime + 0.1f);
                CameraRig.I?.Kick(0.12f);
            }
            else if (outcome == Outcome.Door && door)
            {
                for (int i = 0; i < letters.Count; i++)
                    StartCoroutine(Fly(letters[i], door.position + Vector3.up * 1.5f, letterStagger * i, solid: false, vanishOnArrival: true));
                yield return new WaitForSeconds(letterStagger * letters.Count + letterFlyTime);
                if (inscription) inscription.gameObject.SetActive(false);
                CameraRig.I?.Kick(0.2f);
                SenseController.I?.EmitEcho(door.position, 1.4f);
                // the door lifts and dissolves into light (no slab left hanging in the air)
                var rends = door.GetComponentsInChildren<SpriteRenderer>();
                var cols = door.GetComponentsInChildren<Collider2D>();
                Vector3 a = door.localPosition, b = a + Vector3.up * doorRise;
                for (float t = 0; t < 1; t += Time.deltaTime / Mathf.Max(0.05f, doorTime))
                {
                    float e = Mathf.SmoothStep(0, 1, t);
                    door.localPosition = Vector3.Lerp(a, b, e);
                    foreach (var r in rends) { var c = r.color; c.a = 1f - e; r.color = c; }
                    if (t > 0.35f) foreach (var c in cols) c.enabled = false;
                    yield return null;
                }
                door.gameObject.SetActive(false);
            }
        }

        IEnumerator Crumble(string word)
        {
            if (wrongClip) sfx.PlayOneShot(wrongClip, 0.8f);
            var letters = Letters(word, transform.position);
            for (int i = 0; i < letters.Count; i++) StartCoroutine(Fall(letters[i], 0.1f * i));
            bool scatter = scatterWords != null && System.Array.IndexOf(scatterWords, word) >= 0;
            if (outcome == Outcome.Door && door && !scatter)
            {
                GameEvents.Caption?.Invoke(wrongDoorCaption);
                CameraRig.I?.Kick(0.15f);
                Vector3 home = door.localPosition + Vector3.down * wrongSink;
                for (float t = 0; t < 0.35f; t += Time.deltaTime)
                {
                    door.localPosition = home + new Vector3(Mathf.Sin(t * 70f) * 0.06f * (1f - t / 0.35f), 0, 0);
                    yield return null;
                }
                door.localPosition = home;
            }
            else GameEvents.Caption?.Invoke(wrongCaption);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!string.IsNullOrEmpty(answer) && choices != null && System.Array.IndexOf(choices, answer) < 0)
                Debug.LogWarning($"[WordAltar] '{answer}' is not among the choices — the panel cannot solve it.", this);
            if (outcome == Outcome.Bridge && slots != null && !string.IsNullOrEmpty(answer) && slots.Length > 0 && slots.Length < answer.Length)
                Debug.LogWarning($"[WordAltar] {answer.Length} letters but {slots.Length} slots.", this);
        }

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            Gizmos.color = new Color(1f, 0.85f, 0.35f, 0.8f);
            if (slots != null)
                foreach (var s in slots)
                    if (s) Gizmos.DrawWireCube(s.position + new Vector3(0, plankOffsetY), letterSize);
            if (door)
            {
                Gizmos.DrawLine(transform.position, door.position);
                Gizmos.DrawLine(door.position, door.position + Vector3.up * doorRise);
            }
            if (reveal != null)
                foreach (var g in reveal) if (g) Gizmos.DrawLine(transform.position, g.transform.position);
        }
#endif
    }
}

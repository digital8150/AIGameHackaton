using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// 거꾸로 켜지는 계단. Say the word ("깜빡깜빡") and its letters become stair steps one after another — each step
    /// stays lit for a while, then flickers out, so you climb by following the order. If the order runs top → bottom
    /// you can't climb it; shout the reverse word ("반대로") to flip the order (and play it again).
    /// </summary>
    public class WordStair : WordTarget, IResettable
    {
        [Header("Steps")]
        [Tooltip("Steps bottom → top (one letter lands on each). Gold boxes in the Scene view.")]
        public Transform[] steps;
        [Tooltip("The word that lights the steps — one letter per step")]
        public string word = "깜빡깜빡";
        [Tooltip("Saying this flips the order, then lights the steps")]
        public string reverseWord = "반대로";
        [Tooltip("Steps light top → bottom (can't be climbed until reversed)")]
        public bool topFirst;

        [Header("Rhythm (seconds)")]
        [Tooltip("Between one step lighting and the next")]
        public float stepInterval = 0.8f;
        [Tooltip("How long each step stays solid")]
        public float stepHold = 2.4f;
        [Tooltip("Flicker this long before it goes out")]
        public float warnTime = 0.6f;

        bool startTopFirst;
        readonly List<GameObject> live = new List<GameObject>();

        protected override void Awake() { base.Awake(); startTopFirst = topFirst; }

        protected override void OnSpoken(string text)
        {
            if (text == reverseWord)
            {
                topFirst = !topFirst;
                GameEvents.PuzzleAttempt?.Invoke("stair-reverse", true);
                StartCoroutine(Reverse(text));
            }
            else if (text == word)
            {
                GameEvents.PuzzleAttempt?.Invoke("stair", true);
                Play();
            }
            else
            {
                GameEvents.PuzzleAttempt?.Invoke("stair", false);
                if (wrongClip) sfx.PlayLimited(wrongClip, 0.8f);
                var ls = Letters(text, ReachCenter);
                for (int i = 0; i < ls.Count; i++) StartCoroutine(Fall(ls[i], 0.1f * i));
            }
        }

        IEnumerator Reverse(string text)
        {
            // the reverse word fades, then the word plays in the new order
            Clear();
            var ls = Letters(text, ReachCenter);
            for (int i = 0; i < ls.Count; i++) StartCoroutine(FadeOut(ls[i], 0.6f));
            CameraRig.I?.Kick(0.1f);
            SenseController.I?.EmitEcho(ReachCenter, 0.6f);
            yield return new WaitForSeconds(0.6f);
            Play();
        }

        void Play()
        {
            Clear();
            if (bridgeClip) sfx.PlayLimited(bridgeClip, 0.7f);
            var letters = Letters(word, ReachCenter);
            int n = Mathf.Min(letters.Count, steps.Length);
            for (int i = 0; i < letters.Count; i++)
            {
                if (i >= n) { Destroy(letters[i]); continue; }
                int step = topFirst ? n - 1 - i : i;
                live.Add(letters[i]);
                StartCoroutine(Step(letters[i], steps[step].position, stepInterval * i));
            }
        }

        IEnumerator Step(GameObject letter, Vector3 target, float delay)
        {
            yield return Fly(letter, target, delay, solid: true);
            if (!letter) yield break;
            var st = letter.GetComponent<SenseText>();
            var plank = letter.transform.Find("Plank");
            yield return new WaitForSeconds(Mathf.Max(0, stepHold - warnTime));
            for (float t = 0; t < warnTime; t += Time.deltaTime)
            {
                if (!letter) yield break;
                bool lit = Mathf.PingPong(Time.time * 14f, 1f) > 0.5f;
                if (st) st.lightAlpha = lit ? 1f : 0.35f;
                yield return null;
            }
            if (!letter) yield break;
            var col = letter.GetComponent<BoxCollider2D>();
            if (col) col.enabled = false;
            if (plank) plank.gameObject.SetActive(false);
            live.Remove(letter);
            yield return FadeOut(letter, 0.25f);
        }

        void Clear()
        {
            StopAllCoroutines();
            foreach (var l in live) if (l) Destroy(l);
            live.Clear();
        }

        public void ResetState()
        {
            Clear();
            topFirst = startTopFirst;
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            if (steps == null) return;
            Gizmos.color = new Color(1f, 0.7f, 0.3f, 0.9f);
            Vector3 prev = Vector3.zero; bool has = false;
            for (int i = 0; i < steps.Length; i++)
            {
                if (!steps[i]) continue;
                var p = steps[i].position;
                Gizmos.DrawWireCube(p + new Vector3(0, plankOffsetY), letterSize);
                if (has) Gizmos.DrawLine(prev, p);
                prev = p; has = true;
            }
            // marker over the step that lights first
            int first = topFirst ? steps.Length - 1 : 0;
            if (steps.Length > 0 && steps[first]) Gizmos.DrawWireSphere(steps[first].position + Vector3.up * 0.7f, 0.25f);
        }
#endif
    }
}

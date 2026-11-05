using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// A row of empty slots over a gap with a "말해 봐!" bubble. Whatever the princess says fills the slots one
    /// letter each, and every filled slot is a plank of light (빈칸 다리, 깜빡깜빡 다리).
    ///  - Accumulate: words add up until every slot is filled; longer words just spill the extra letters.
    ///  - Rebuild: each word replaces the planks — "깜빡" makes 2, "깜빡깜빡" makes 4.
    /// Optionally the planks blink, and speaking here can wipe other bridges (the way back disappears).
    /// </summary>
    public class WordFillBridge : WordTarget
    {
        public enum FillMode { Accumulate, Rebuild }

        [Header("Slots")]
        [Tooltip("Empty slots left → right. A child named 'Empty' (dashed outline) is shown while the slot is empty.")]
        public Transform[] slots;
        public FillMode mode = FillMode.Accumulate;
        [Tooltip("Speaking here clears these bridges' planks (e.g. the way you came).")]
        public WordFillBridge[] clearOnSpeak;

        [Header("Bubble")]
        [Tooltip("'말해 봐!' bubble; hidden once every slot is filled.")]
        public GameObject bubble;

        [Header("Blinking planks")]
        public bool blink;
        [Min(0.2f)] public float blinkPeriod = 3.2f;
        [Tooltip("Solid part of the cycle")]
        public float blinkOn = 2.2f;
        [Tooltip("Flicker this long before going out")]
        public float blinkWarn = 0.7f;
        [Range(0, 1)] public float offAlpha = 0.15f;

        public override bool AcceptsAnyText => true;

        class Plank { public GameObject go; public BoxCollider2D col; public SenseText text; public Renderer[] rends; }
        readonly List<Plank> planks = new List<Plank>();
        readonly Dictionary<int, Plank> bySlot = new Dictionary<int, Plank>();

        public int Filled => bySlot.Count;

        void Start() => RefreshMarkers();

        protected override void OnSpoken(string text)
        {
            GameEvents.PuzzleAttempt?.Invoke("fill", true);
            if (clearOnSpeak != null) foreach (var b in clearOnSpeak) if (b && b != this) b.ClearPlanks();
            if (mode == FillMode.Rebuild) ClearPlanks();
            StartCoroutine(Fill(text));
        }

        IEnumerator Fill(string text)
        {
            if (bridgeClip) sfx.PlayOneShot(bridgeClip, 0.7f);
            var letters = Letters(text, ReachCenter);
            int next = 0;
            for (int i = 0; i < letters.Count; i++)
            {
                while (next < slots.Length && bySlot.ContainsKey(next)) next++;
                if (next >= slots.Length) { StartCoroutine(Fall(letters[i], 0.1f * i)); continue; }  // spilled letters
                int slot = next++;
                var p = new Plank { go = letters[i] };
                bySlot[slot] = p;
                planks.Add(p);
                StartCoroutine(Land(p, slot, letterStagger * i));
            }
            yield return new WaitForSeconds(letterStagger * letters.Count + letterFlyTime + 0.1f);
            RefreshMarkers();
            if (mode == FillMode.Accumulate && bySlot.Count >= slots.Length)
            {
                Solved = true;
                CameraRig.I?.Kick(0.12f);
            }
        }

        IEnumerator Land(Plank p, int slot, float delay)
        {
            yield return Fly(p.go, slots[slot].position, delay, solid: true);
            if (!p.go) yield break;
            p.col = p.go.GetComponent<BoxCollider2D>();
            p.text = p.go.GetComponent<SenseText>();
            p.rends = p.go.GetComponentsInChildren<Renderer>(true);
            RefreshMarkers();
        }

        /// <summary>Remove every plank (they fade out). Used by Rebuild and by another bridge's clearOnSpeak.</summary>
        public void ClearPlanks()
        {
            foreach (var p in planks) if (p.go) StartCoroutine(FadeOut(p.go, 0.35f));
            planks.Clear();
            bySlot.Clear();
            Solved = false;
            RefreshMarkers();
        }

        void RefreshMarkers()
        {
            if (slots != null)
                for (int i = 0; i < slots.Length; i++)
                {
                    if (!slots[i]) continue;
                    var empty = slots[i].Find("Empty");
                    if (empty) empty.gameObject.SetActive(!bySlot.ContainsKey(i));
                }
            if (bubble) bubble.SetActive(mode == FillMode.Rebuild || bySlot.Count < (slots != null ? slots.Length : 0));
        }

        protected override void Update()
        {
            base.Update();
            if (!blink) return;
            float t = Mathf.Repeat(Time.time, blinkPeriod);
            bool on = t < blinkOn;
            float a = !on ? offAlpha : (blinkOn - t < blinkWarn ? (Mathf.PingPong(Time.time * 14f, 1f) > 0.5f ? 1f : 0.35f) : 1f);
            foreach (var p in planks)
            {
                if (!p.go || !p.col) continue;
                p.col.enabled = on;
                if (p.text) p.text.lightAlpha = a;
                var plank = p.go.transform.Find("Plank");
                if (plank) plank.gameObject.SetActive(on || a > offAlpha);
            }
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            Gizmos.color = new Color(1f, 0.7f, 0.3f, 0.9f);
            if (slots == null) return;
            foreach (var s in slots)
                if (s) Gizmos.DrawWireCube(s.position + new Vector3(0, plankOffsetY), letterSize);
            if (clearOnSpeak != null)
                foreach (var b in clearOnSpeak) if (b) Gizmos.DrawLine(ReachCenter, b.transform.position);
        }
#endif
    }
}

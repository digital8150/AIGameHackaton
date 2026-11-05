using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// Section checklist for chapter 0, in the game's pixel style: slides in from the right under the
    /// top-right notice, key chips before each objective, a diamond that fills when it is done.
    /// </summary>
    public class TutorialView : MonoBehaviour
    {
        public TutorialDirector director;
        public Sprite checkEmpty, checkDone;

        [Header("Layout (the panel itself lives in the Tutorial Panel prefab)")]
        [Tooltip("Panel X (right-anchored, pivot right) when hidden (off-screen right) and shown.")]
        public Vector2 slideX = new Vector2(530f, -12f);
        [Tooltip("Space under the last line. The side and top insets come from the elements' positions in the prefab.")]
        public float padding = 14f;
        public float rowHeight = 34f;
        [Tooltip("Gap between the last objective and the tip.")]
        public float tipGap = 8f;
        [Tooltip("Gap between key chips.")]
        public float keySpacing = 4f;
        public float slideInSeconds = 0.35f, slideOutSeconds = 0.3f, slideOutDelay = 0.55f;
        [Range(0, 1)] public float doneLabelAlpha = 0.55f;
        public string stepFormat = "◆ {0} / {1}";

        [Header("Elements")]
        public RectTransform root;
        public Text step, title, tip;
        [Tooltip("Inactive row: Check (Image), Key (Image + Text, cloned per key), Label, Count.")]
        public RectTransform rowTemplate;

        readonly List<Row> rows = new List<Row>();
        Coroutine anim;

        class Row
        {
            public RectTransform rect;
            public CanvasGroup group;
            public Image check;
            public Text label, count;
            public bool done;
        }

        void Awake()
        {
            if (!root || !rowTemplate) { Debug.LogError("[TutorialView] panel is not assigned - use the Tutorial Panel prefab.", this); return; }
            rowTemplate.gameObject.SetActive(false);
            root.anchoredPosition = new Vector2(slideX.x, root.anchoredPosition.y);
        }

        void OnEnable()
        {
            director.SectionStarted += OnSectionStarted;
            director.ObjectiveChanged += OnObjectiveChanged;
            director.SectionCompleted += OnSectionCompleted;
        }

        void OnDisable()
        {
            director.SectionStarted -= OnSectionStarted;
            director.ObjectiveChanged -= OnObjectiveChanged;
            director.SectionCompleted -= OnSectionCompleted;
        }

        static void Place(RectTransform r, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        // ------------------------------------------------------------------ events
        void OnSectionStarted(int index, int total, TutorialSection section)
        {
            foreach (var r in rows) Destroy(r.rect.gameObject);
            rows.Clear();

            step.text = string.Format(stepFormat, index + 1, total);
            title.text = section.title;
            float w = root.sizeDelta.x;
            float y = rowTemplate.anchoredPosition.y;
            foreach (var o in section.objectives)
            {
                rows.Add(MakeRow(o, y));
                y -= rowHeight;
            }
            bool hasTip = !string.IsNullOrEmpty(section.tip);
            tip.text = hasTip ? section.tip : "";
            if (hasTip)
            {
                y -= tipGap;
                // lines up with the rows; the same inset on the far side
                float inset = rowTemplate.anchoredPosition.x;
                Place(tip.rectTransform, new Vector2(inset, y), new Vector2(w - inset * 2, 20));
                tip.rectTransform.sizeDelta = new Vector2(w - inset * 2, tip.preferredHeight);
                y -= tip.preferredHeight;
            }
            root.sizeDelta = new Vector2(w, -y + padding);

            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(SlideIn());
        }

        Row MakeRow(TutorialObjective o, float y)
        {
            var rect = Instantiate(rowTemplate, root);
            rect.name = "Row";
            rect.gameObject.SetActive(true);
            rect.anchoredPosition = new Vector2(rowTemplate.anchoredPosition.x, y);
            var row = new Row
            {
                rect = rect,
                group = rect.GetComponent<CanvasGroup>(),
                check = rect.Find("Check").GetComponent<Image>(),
                label = rect.Find("Label").GetComponent<Text>(),
                count = rect.Find("Count").GetComponent<Text>(),
            };
            row.check.sprite = checkEmpty;
            var keyTemplate = (RectTransform)rect.Find("Key");
            float x = keyTemplate.anchoredPosition.x;
            if (o.keys != null)
                foreach (var k in o.keys)
                {
                    var cap = Instantiate(keyTemplate, rect);
                    cap.GetComponentInChildren<Text>().text = k;
                    float kw = Mathf.Max(keyTemplate.sizeDelta.x, k.Length * 10 + 14);
                    cap.sizeDelta = new Vector2(kw, keyTemplate.sizeDelta.y);
                    cap.anchoredPosition = new Vector2(x, keyTemplate.anchoredPosition.y);
                    x += kw + keySpacing;
                }
            Destroy(keyTemplate.gameObject);
            var lr = row.label.rectTransform;
            lr.anchoredPosition = new Vector2(x + 4, lr.anchoredPosition.y);
            row.label.text = o.text;
            row.count.gameObject.SetActive(o.showCount);
            row.group.alpha = 0;
            return row;
        }

        void OnObjectiveChanged(int index, int progress, int target, bool done)
        {
            if (index >= rows.Count) return;
            var r = rows[index];
            r.count.text = $"{progress}/{target}";
            if (done && !r.done)
            {
                r.done = true;
                r.count.gameObject.SetActive(false);
                r.check.sprite = checkDone;
                StartCoroutine(Tick(r));
            }
        }

        void OnSectionCompleted(int index)
        {
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(SlideOut());
        }

        // ------------------------------------------------------------------ animation
        static float EaseOut(float t) { t = 1 - t; return 1 - t * t * t; }

        IEnumerator SlideIn()
        {
            for (float t = 0; t < 1; t += Time.deltaTime / slideInSeconds)
            {
                root.anchoredPosition = new Vector2(Mathf.Lerp(slideX.x, slideX.y, EaseOut(t)), root.anchoredPosition.y);
                for (int i = 0; i < rows.Count; i++)
                    rows[i].group.alpha = Mathf.Clamp01((t * slideInSeconds - 0.12f - 0.06f * i) / 0.15f);
                yield return null;
            }
            root.anchoredPosition = new Vector2(slideX.y, root.anchoredPosition.y);
            // finish row fade-ins that start after the slide
            for (float t = 0; t < 0.4f; t += Time.deltaTime)
            {
                for (int i = 0; i < rows.Count; i++)
                    rows[i].group.alpha = Mathf.Max(rows[i].group.alpha, Mathf.Clamp01((slideInSeconds + t - 0.12f - 0.06f * i) / 0.15f));
                yield return null;
            }
            foreach (var r in rows) r.group.alpha = 1;
        }

        IEnumerator SlideOut()
        {
            yield return new WaitForSeconds(slideOutDelay);
            float from = root.anchoredPosition.x;
            for (float t = 0; t < 1; t += Time.deltaTime / slideOutSeconds)
            {
                root.anchoredPosition = new Vector2(Mathf.Lerp(from, slideX.x, t * t), root.anchoredPosition.y);
                yield return null;
            }
            root.anchoredPosition = new Vector2(slideX.x, root.anchoredPosition.y);
        }

        IEnumerator Tick(Row r)
        {
            for (float t = 0; t < 1; t += Time.deltaTime / 0.25f)
            {
                float s = 1f + Mathf.Sin(t * Mathf.PI) * 0.45f;
                r.check.rectTransform.localScale = new Vector3(s, s, 1);
                r.rect.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.04f);
                var c = r.label.color; c.a = Mathf.Lerp(1f, doneLabelAlpha, t); r.label.color = c;
                yield return null;
            }
            r.check.rectTransform.localScale = Vector3.one;
            r.rect.localScale = Vector3.one;
        }
    }
}

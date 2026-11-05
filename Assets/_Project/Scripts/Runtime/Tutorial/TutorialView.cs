using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// Section checklist for chapter 0, in the game's pixel style: slides in from the left under the
    /// medallion, key chips before each objective, a diamond that fills when it is done.
    /// </summary>
    public class TutorialView : MonoBehaviour
    {
        public TutorialDirector director;
        public Font font, boldFont;
        public Sprite panel, keycap, checkEmpty, checkDone;

        static readonly Color Gold = new Color(1f, 0.86f, 0.52f);
        static readonly Color Cream = new Color(1f, 0.95f, 0.86f);
        static readonly Color Muted = new Color(0.86f, 0.80f, 0.92f);
        static readonly Color KeyInk = new Color(0.20f, 0.12f, 0.20f);
        const float Width = 360f, Pad = 14f, RowH = 34f, HiddenX = -400f, ShownX = 20f;

        RectTransform root;
        Image bg;
        Text step, title, tip;
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

        void Awake() { Build(); }

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

        void Build()
        {
            var canvasGo = new GameObject("Tutorial Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90; // under the HUD fade
            canvas.pixelPerfect = true;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            scaler.matchWidthOrHeight = 1f;

            bg = new GameObject("Panel", typeof(Image)).GetComponent<Image>();
            bg.transform.SetParent(canvasGo.transform, false);
            bg.sprite = panel;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 100f / 18f / 2f;
            bg.raycastTarget = false;
            root = bg.rectTransform;
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0, 1);
            root.anchoredPosition = new Vector2(HiddenX, -112);
            root.sizeDelta = new Vector2(Width, 120);

            step = Label(root, 16, Gold, false, TextAnchor.UpperLeft);
            Place(step.rectTransform, new Vector2(Pad, -Pad + 2), new Vector2(Width - Pad * 2, 20));
            title = Label(root, 26, Cream, true, TextAnchor.UpperLeft);
            Place(title.rectTransform, new Vector2(Pad, -Pad - 18), new Vector2(Width - Pad * 2, 32));
            tip = Label(root, 16, Muted, false, TextAnchor.UpperLeft);
            tip.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        Text Label(Transform parent, int size, Color c, bool bold, TextAnchor align)
        {
            var t = new GameObject("Text", typeof(Text)).GetComponent<Text>();
            t.transform.SetParent(parent, false);
            t.font = bold && boldFont ? boldFont : font;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0.08f, 0.03f, 0.10f, 0.9f);
            sh.effectDistance = new Vector2(2, -2);
            return t;
        }

        static void Place(RectTransform r, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        Image Img(Transform parent, Sprite s, Vector2 pos, Vector2 size)
        {
            var i = new GameObject("Image", typeof(Image)).GetComponent<Image>();
            i.transform.SetParent(parent, false);
            i.sprite = s;
            i.raycastTarget = false;
            Place(i.rectTransform, pos, size);
            return i;
        }

        // ------------------------------------------------------------------ events
        void OnSectionStarted(int index, int total, TutorialSection section)
        {
            foreach (var r in rows) Destroy(r.rect.gameObject);
            rows.Clear();

            step.text = $"◆ {index + 1} / {total}";
            title.text = section.title;
            float y = -Pad - 58;
            foreach (var o in section.objectives)
            {
                rows.Add(MakeRow(o, y));
                y -= RowH;
            }
            bool hasTip = !string.IsNullOrEmpty(section.tip);
            tip.text = hasTip ? section.tip : "";
            if (hasTip)
            {
                y -= 6;
                Place(tip.rectTransform, new Vector2(Pad, y), new Vector2(Width - Pad * 2, 20));
                tip.rectTransform.sizeDelta = new Vector2(Width - Pad * 2, tip.preferredHeight);
                y -= tip.preferredHeight;
            }
            root.sizeDelta = new Vector2(Width, -y + Pad);

            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(SlideIn());
        }

        Row MakeRow(TutorialObjective o, float y)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(root, false);
            var rect = (RectTransform)go.transform;
            Place(rect, new Vector2(Pad, y), new Vector2(Width - Pad * 2, RowH));
            var row = new Row { rect = rect, group = go.GetComponent<CanvasGroup>() };
            row.check = Img(rect, checkEmpty, new Vector2(0, -7), new Vector2(18, 18));
            float x = 26;
            if (o.keys != null)
                foreach (var k in o.keys)
                {
                    float w = Mathf.Max(24, k.Length * 10 + 14);
                    var cap = Img(rect, keycap, new Vector2(x, -3), new Vector2(w, 26));
                    cap.type = Image.Type.Sliced;
                    cap.pixelsPerUnitMultiplier = 100f / 18f / 2f;
                    var kt = Label(cap.transform, 16, KeyInk, true, TextAnchor.MiddleCenter);
                    kt.GetComponent<Shadow>().enabled = false;
                    var kr = kt.rectTransform;
                    kr.anchorMin = Vector2.zero; kr.anchorMax = Vector2.one; kr.offsetMin = new Vector2(0, 3); kr.offsetMax = Vector2.zero;
                    kt.text = k;
                    x += w + 4;
                }
            row.label = Label(rect, 20, Cream, false, TextAnchor.MiddleLeft);
            Place(row.label.rectTransform, new Vector2(x + 4, -2), new Vector2(Width - x, 28));
            row.label.text = o.text;
            row.count = Label(rect, 16, Gold, false, TextAnchor.MiddleRight);
            Place(row.count.rectTransform, new Vector2(Width - Pad * 2 - 60, -2), new Vector2(60, 28));
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
            for (float t = 0; t < 1; t += Time.deltaTime / 0.35f)
            {
                root.anchoredPosition = new Vector2(Mathf.Lerp(HiddenX, ShownX, EaseOut(t)), root.anchoredPosition.y);
                for (int i = 0; i < rows.Count; i++)
                    rows[i].group.alpha = Mathf.Clamp01((t * 0.35f - 0.12f - 0.06f * i) / 0.15f);
                yield return null;
            }
            root.anchoredPosition = new Vector2(ShownX, root.anchoredPosition.y);
            // finish row fade-ins that start after the slide
            for (float t = 0; t < 0.4f; t += Time.deltaTime)
            {
                for (int i = 0; i < rows.Count; i++)
                    rows[i].group.alpha = Mathf.Max(rows[i].group.alpha, Mathf.Clamp01((0.35f + t - 0.12f - 0.06f * i) / 0.15f));
                yield return null;
            }
            foreach (var r in rows) r.group.alpha = 1;
        }

        IEnumerator SlideOut()
        {
            yield return new WaitForSeconds(0.55f);
            float from = root.anchoredPosition.x;
            for (float t = 0; t < 1; t += Time.deltaTime / 0.3f)
            {
                root.anchoredPosition = new Vector2(Mathf.Lerp(from, HiddenX, t * t), root.anchoredPosition.y);
                yield return null;
            }
            root.anchoredPosition = new Vector2(HiddenX, root.anchoredPosition.y);
        }

        IEnumerator Tick(Row r)
        {
            for (float t = 0; t < 1; t += Time.deltaTime / 0.25f)
            {
                float s = 1f + Mathf.Sin(t * Mathf.PI) * 0.45f;
                r.check.rectTransform.localScale = new Vector3(s, s, 1);
                r.rect.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.04f);
                var c = r.label.color; c.a = Mathf.Lerp(1f, 0.55f, t); r.label.color = c;
                yield return null;
            }
            r.check.rectTransform.localScale = Vector3.one;
            r.rect.localScale = Vector3.one;
        }
    }
}

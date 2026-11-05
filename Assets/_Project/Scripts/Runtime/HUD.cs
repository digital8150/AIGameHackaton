using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// Player-facing HUD. Says as little as possible: who you are sensing through (a medallion),
    /// what the world sounds like when you cannot hear it (captions), and the twilight voice's
    /// occasional whisper. Developer information lives behind F1 only.
    /// Pixel UI is authored at 480x270 and drawn 2x on a 960x540 canvas.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public static HUD I { get; private set; }

        public Font font;
        public Font boldFont;
        public Sprite princeIcon, princessIcon;
        public Sprite medallionLight, medallionDark, panel;
        public EchoCaller echo;
        public bool debugOverlay = false;

        static readonly Color Gold = new Color(1f, 0.86f, 0.52f);
        static readonly Color Ice = new Color(0.80f, 0.92f, 1f);
        static readonly Color Cream = new Color(1f, 0.95f, 0.86f);
        static readonly Color ShadowInk = new Color(0.10f, 0.04f, 0.12f, 0.9f);

        RectTransform medallionRoot, partnerRoot;
        Image medallion, portrait, partnerMedallion, partnerPortrait, fade, clearWash;
        Text nameCard, micNote, narration, caption, notice, debug, titleSmall, titleBig, titleLine, clearTitle, clearLine, clearStats, clearContinue;
        CanvasGroup nameGroup, narrationGroup, captionGroup, noticeGroup, titleGroup, clearGroup, debugGroup;
        Coroutine nameCo, narrationCo, captionCo, noticeCo, fadeCo;
        bool lastDark;
        float pop;

        void Awake()
        {
            I = this;
            Build();
        }

        void Start()
        {
            GameEvents.Hint += ShowNarration;
            GameEvents.Notice += ShowNotice;
            GameEvents.Caption += ShowCaption;
            GameEvents.Cleared += ShowClear;
            GameEvents.Switched += OnSwitched;
            lastDark = SenseController.I && SenseController.I.IsDark;
            RefreshMedallion(lastDark);
        }

        // ------------------------------------------------------------------ build
        void Build()
        {
            var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = true;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            scaler.matchWidthOrHeight = 1f;
            var root = canvasGo.transform;

            // --- medallion: the sense you are living in, with your partner tucked beside it
            medallionRoot = Rect(root, "Medallion", new Vector2(0, 1), new Vector2(22, -20), new Vector2(72, 72));
            medallion = Img(medallionRoot, "Ring", Vector2.zero, new Vector2(72, 72));
            portrait = Img(medallionRoot, "Portrait", new Vector2(0, -2), new Vector2(48, 48));

            partnerRoot = Rect(root, "Partner", new Vector2(0, 1), new Vector2(76, -66), new Vector2(36, 36));
            partnerMedallion = Img(partnerRoot, "Ring", Vector2.zero, new Vector2(36, 36));
            partnerPortrait = Img(partnerRoot, "Portrait", new Vector2(0, -1), new Vector2(24, 24));

            micNote = Label(root, "Mic", 22, TextAnchor.MiddleCenter, Ice, false);
            Place(micNote.rectTransform, new Vector2(0, 1), new Vector2(102, -18), new Vector2(30, 30));

            // name card that slides out after a switch
            nameCard = Label(root, "Name", 22, TextAnchor.MiddleLeft, Gold, true);
            Place(nameCard.rectTransform, new Vector2(0, 1), new Vector2(104, -44), new Vector2(320, 30));
            nameGroup = nameCard.gameObject.AddComponent<CanvasGroup>();
            nameGroup.alpha = 0;

            // --- the twilight voice (narration) and sound captions, bottom centre, no boxes
            narration = Label(root, "Narration", 22, TextAnchor.MiddleCenter, Cream, false);
            Place(narration.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 44), new Vector2(800, 32));
            narrationGroup = narration.gameObject.AddComponent<CanvasGroup>();
            narrationGroup.alpha = 0;

            caption = Label(root, "Caption", 22, TextAnchor.MiddleCenter, Ice, true);
            Place(caption.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 96), new Vector2(600, 30));
            captionGroup = caption.gameObject.AddComponent<CanvasGroup>();
            captionGroup.alpha = 0;

            // --- small chip when the game quietly adjusts itself for the player
            var chip = Sliced(root, "Notice", new Vector2(1, 1), new Vector2(-20, -22), new Vector2(210, 34));
            noticeGroup = chip.gameObject.AddComponent<CanvasGroup>();
            noticeGroup.alpha = 0;
            notice = Label(chip.transform, "Text", 18, TextAnchor.MiddleCenter, Gold, false);
            Stretch(notice.rectTransform, new Vector2(8, 0), new Vector2(-8, 0));

            // --- chapter title card
            var title = Rect(root, "Title Card", new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(700, 200));
            titleGroup = title.gameObject.AddComponent<CanvasGroup>();
            titleGroup.alpha = 0;
            titleSmall = Label(title, "Chapter", 22, TextAnchor.MiddleCenter, Gold, false);
            Place(titleSmall.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 56), new Vector2(600, 30));
            titleBig = Label(title, "Name", 66, TextAnchor.MiddleCenter, Cream, true);
            Place(titleBig.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(600, 80));
            titleLine = Label(title, "Line", 22, TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.7f), false);
            Place(titleLine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(600, 30));

            // --- clear
            clearWash = Img(root, "Clear Wash", Vector2.zero, Vector2.zero);
            Stretch(clearWash.rectTransform, Vector2.zero, Vector2.zero);
            clearWash.color = new Color(1f, 0.72f, 0.45f, 0f);
            var clear = Rect(root, "Clear", new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(760, 240));
            clearGroup = clear.gameObject.AddComponent<CanvasGroup>();
            clearGroup.alpha = 0;
            clearTitle = Label(clear, "Title", 66, TextAnchor.MiddleCenter, Cream, true);
            Place(clearTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(740, 80));
            clearLine = Label(clear, "Line", 22, TextAnchor.MiddleCenter, Cream, false);
            Place(clearLine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -8), new Vector2(740, 30));
            clearStats = Label(clear, "Stats", 22, TextAnchor.MiddleCenter, Gold, false);
            Place(clearStats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -64), new Vector2(740, 30));
            clearContinue = Label(clear, "Continue", 18, TextAnchor.MiddleCenter, Cream, false);
            Place(clearContinue.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -116), new Vector2(740, 26));
            clearContinue.text = "";

            // --- developer overlay (F1), hidden by default
            var dbg = Img(root, "Dev Overlay", Vector2.zero, new Vector2(420, 170));
            Place(dbg.rectTransform, new Vector2(1, 0), new Vector2(-12, 12), new Vector2(420, 170));
            dbg.color = new Color(0, 0, 0, 0.7f);
            debugGroup = dbg.gameObject.AddComponent<CanvasGroup>();
            debug = Label(dbg.transform, "Text", 13, TextAnchor.LowerLeft, new Color(0.75f, 1f, 0.85f), false);
            Stretch(debug.rectTransform, new Vector2(8, 6), new Vector2(-8, -6));
            debug.horizontalOverflow = HorizontalWrapMode.Wrap;

            // --- fade
            fade = Img(root, "Fade", Vector2.zero, Vector2.zero);
            Stretch(fade.rectTransform, Vector2.zero, Vector2.zero);
            fade.color = new Color(0.03f, 0.02f, 0.06f, 1f);
        }

        RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            Place(r, anchor, pos, size);
            return r;
        }

        Image Img(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var img = new GameObject(name, typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.rectTransform.anchoredPosition = pos;
            img.rectTransform.sizeDelta = size;
            img.raycastTarget = false;
            return img;
        }

        Image Sliced(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var img = Img(parent, name, pos, size);
            Place(img.rectTransform, anchor, pos, size);
            img.sprite = panel;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 100f / 18f / 2f; // 1 sprite pixel = 2 canvas units
            return img;
        }

        Text Label(Transform parent, string name, int size, TextAnchor align, Color c, bool bold)
        {
            var t = new GameObject(name, typeof(Text)).GetComponent<Text>();
            t.transform.SetParent(parent, false);
            t.font = bold && boldFont ? boldFont : font;
            t.fontSize = size;
            t.alignment = align;
            t.color = c;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = ShadowInk;
            sh.effectDistance = new Vector2(2, -2);
            return t;
        }

        static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = anchor;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        static void Stretch(RectTransform r, Vector2 min, Vector2 max)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = min; r.offsetMax = max;
        }

        // ------------------------------------------------------------------ runtime
        void Update()
        {
            if (GameInput.I && GameInput.I.DebugOverlay.WasPressedThisFrame()) debugOverlay = !debugOverlay;
            debugGroup.alpha = debugOverlay ? 1 : 0;
            if (debugOverlay) debug.text = DebugText();

            // medallion pop on switch
            pop = Mathf.MoveTowards(pop, 0, Time.deltaTime * 4f);
            float s = 1f + Mathf.Sin(pop * Mathf.PI) * 0.18f;
            medallionRoot.localScale = new Vector3(s, s, 1);

            // breathing mic note while the player is using their voice
            var voice = VoiceInput.I;
            bool micOn = voice && voice.Recording;
            bool thinking = voice && voice.Transcribing;
            float lvl = micOn ? Mathf.Clamp01(voice.Level * 25f) : thinking ? 0.25f + 0.25f * Mathf.Sin(Time.time * 8f) : 0f;
            micNote.text = micOn || thinking ? "♪" : "";
            micNote.rectTransform.localScale = Vector3.one * (1f + lvl * 0.6f);
            micNote.color = new Color(Ice.r, Ice.g, Ice.b, 0.5f + lvl * 0.5f);
        }

        void OnSwitched(Sense s)
        {
            bool dark = s == Sense.Dark;
            RefreshMedallion(dark);
            pop = 1f;
            nameCard.text = dark ? "트와일라잇 프린스" : "더스크 프린세스";
            nameCard.color = dark ? Ice : Gold;
            if (nameCo != null) StopCoroutine(nameCo);
            nameCo = StartCoroutine(Pop(nameGroup, 1.2f, 6f, 2f));
        }

        void RefreshMedallion(bool dark)
        {
            medallion.sprite = dark ? medallionDark : medallionLight;
            portrait.sprite = dark ? princeIcon : princessIcon;
            partnerMedallion.sprite = dark ? medallionLight : medallionDark;
            partnerPortrait.sprite = dark ? princessIcon : princeIcon;
            partnerMedallion.color = partnerPortrait.color = new Color(1, 1, 1, 0.75f);
        }

        string DebugText()
        {
            var sb = new System.Text.StringBuilder();
            var dir = AccessibilityDirector.I;
            var flow = GameFlow.I;
            sb.AppendLine($"[DEV] director {(dir && dir.Enabled ? "on" : "off")} (F2) · Jev {(JevClient.HasKey ? JevClient.LastStatus : "no key → rules")}");
            var vc = VoiceInput.I ? VoiceInput.I.GetComponent<VoiceCommands>() : null;
            if (vc && vc.LastDebug.Length > 0) sb.AppendLine("voice " + vc.LastDebug);
            if (SenseController.I) sb.AppendLine($"sense {(SenseController.I.IsDark ? "dark/prince" : "light/princess")} · echo x{SenseController.I.DirectorBoost:0.00}");
            if (flow && DuoController.I) sb.AppendLine($"zone {flow.CurrentZone} · falls {flow.Deaths} · switches {DuoController.I.SwitchCount}");
            if (dir) foreach (var l in dir.Log) sb.AppendLine("<color=#ffe08a>" + l + "</color>");
            return sb.ToString();
        }

        IEnumerator Pop(CanvasGroup g, float hold, float inSpeed = 5f, float outSpeed = 2.5f)
        {
            for (float t = g.alpha; t < 1; t += Time.deltaTime * inSpeed) { g.alpha = t; yield return null; }
            g.alpha = 1;
            yield return new WaitForSeconds(hold);
            for (float t = 1; t > 0; t -= Time.deltaTime * outSpeed) { g.alpha = t; yield return null; }
            g.alpha = 0;
        }

        public void ShowNarration(string msg)
        {
            narration.text = msg;
            if (narrationCo != null) StopCoroutine(narrationCo);
            narrationCo = StartCoroutine(Pop(narrationGroup, 3.2f, 2.5f, 1.2f));
        }

        public void ShowCaption(string msg)
        {
            caption.text = msg;
            if (captionCo != null) StopCoroutine(captionCo);
            captionCo = StartCoroutine(Pop(captionGroup, 1.1f, 10f, 3f));
        }

        public void ShowNotice(string msg)
        {
            notice.text = msg;
            if (noticeCo != null) StopCoroutine(noticeCo);
            noticeCo = StartCoroutine(Pop(noticeGroup, 2.8f, 5f, 1.5f));
        }

        public void ShowTitle(string chapter, string name, string line)
        {
            titleSmall.text = chapter;
            titleBig.text = name;
            titleLine.text = line;
            StartCoroutine(Pop(titleGroup, 2.2f, 1.2f, 0.8f));
        }

        public void Fade(float from, float to, float seconds)
        {
            if (fadeCo != null) StopCoroutine(fadeCo);
            fadeCo = StartCoroutine(FadeRoutine(from, to, seconds));
        }

        IEnumerator FadeRoutine(float a, float b, float s)
        {
            var c = fade.color;
            for (float t = 0; t < 1; t += Time.deltaTime / s) { c.a = Mathf.Lerp(a, b, t); fade.color = c; yield return null; }
            c.a = b; fade.color = c;
        }

        void ShowClear()
        {
            var flow = GameFlow.I;
            float time = Time.time - flow.StartTime;
            clearTitle.text = flow.clearTitle;
            clearLine.text = flow.clearLine;
            clearStats.text = $"★ {Mathf.FloorToInt(time / 60f)}:{time % 60f:00}     ◆ {DuoController.I.SwitchCount}     ♥ {flow.Deaths}";
            StartCoroutine(ClearRoutine());
        }

        public void ShowContinue(bool hasNext)
        {
            StartCoroutine(ContinueBlink(hasNext ? "▶  Space" : "— 끝 —"));
        }

        IEnumerator ContinueBlink(string text)
        {
            clearContinue.text = text;
            while (true)
            {
                var c = clearContinue.color;
                c.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * 2.2f));
                clearContinue.color = c;
                yield return null;
            }
        }

        IEnumerator ClearRoutine()
        {
            for (float t = 0; t < 1; t += Time.deltaTime / 1.5f)
            {
                clearWash.color = new Color(1f, 0.72f, 0.45f, t * 0.35f);
                yield return null;
            }
            yield return Pop(clearGroup, 9999f, 1.5f, 1f);
        }
    }
}

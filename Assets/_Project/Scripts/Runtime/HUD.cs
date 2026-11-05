using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// Player-facing HUD. Says as little as possible: who you are sensing through (the emblems, see SenseEmblems),
    /// what the world sounds like when you cannot hear it (captions), and the twilight voice's
    /// occasional whisper. Developer information lives behind F1 only.
    /// Pixel UI is authored at 480x270 and drawn 2x on a 960x540 canvas.
    /// The layout lives in the HUD prefab (HUD Canvas child) — move/restyle elements there; this script only animates them.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public static HUD I { get; private set; }

        /// <summary>Fade-in speed, hold seconds, fade-out speed of a pop-up element.</summary>
        [System.Serializable]
        public struct PopTiming
        {
            public float fadeIn, hold, fadeOut;
            public PopTiming(float fadeIn, float hold, float fadeOut) { this.fadeIn = fadeIn; this.hold = hold; this.fadeOut = fadeOut; }
        }

        [Header("Sprites")]
        public EchoCaller echo;
        [Tooltip("Developer overlay (F1). Off for players.")]
        public bool debugOverlay = false;

        [Header("Words")]
        public string princeName = "트와일라잇 프린스";
        public string princessName = "더스크 프린세스";
        public string continueNext = "▶  Space";
        public string continueEnd = "— 끝 —";
        [Tooltip("{0} minutes, {1} seconds, {2} switches, {3} falls")]
        public string clearStatsFormat = "★ {0}:{1:00}     ◆ {2}     ♥ {3}";

        [Header("Colours set at runtime")]
        public Color princeColor = new Color(0.80f, 0.92f, 1f);
        public Color princessColor = new Color(1f, 0.86f, 0.52f);
        public Color clearWashColor = new Color(1f, 0.72f, 0.45f, 0.35f);

        [Header("Timing")]
        public PopTiming nameCardTiming = new PopTiming(6f, 1.2f, 2f);
        public PopTiming narrationTiming = new PopTiming(2.5f, 3.2f, 1.2f);
        public PopTiming captionTiming = new PopTiming(10f, 1.1f, 3f);
        public PopTiming noticeTiming = new PopTiming(5f, 2.8f, 1.5f);
        public PopTiming titleTiming = new PopTiming(1.2f, 2.2f, 0.8f);
        public float clearWashSeconds = 1.5f;

        [Header("Elements (inside HUD Canvas)")]
        public Image fade, clearWash;
        public Text nameCard, micNote, narration, caption, notice, debug, titleSmall, titleBig, titleLine, clearTitle, clearLine, clearStats, clearContinue;
        public CanvasGroup nameGroup, narrationGroup, captionGroup, noticeGroup, titleGroup, clearGroup, debugGroup;

        Coroutine nameCo, narrationCo, captionCo, noticeCo, fadeCo;

        void Awake()
        {
            I = this;
            if (!fade) Debug.LogError("[HUD] elements are not assigned — use the HUD prefab.", this);
            fade.color = new Color(fade.color.r, fade.color.g, fade.color.b, 1f);
            foreach (var g in new[] { nameGroup, narrationGroup, captionGroup, noticeGroup, titleGroup, clearGroup }) g.alpha = 0;
            clearContinue.text = "";
            var w = clearWashColor; w.a = 0; clearWash.color = w;
        }

        void Start()
        {
            GameEvents.Hint += ShowNarration;
            GameEvents.Notice += ShowNotice;
            GameEvents.Caption += ShowCaption;
            GameEvents.Cleared += ShowClear;
            GameEvents.Switched += OnSwitched;
        }

        // ------------------------------------------------------------------ runtime
        void Update()
        {
            if (GameInput.I && GameInput.I.DebugOverlay.WasPressedThisFrame()) debugOverlay = !debugOverlay;
            debugGroup.alpha = debugOverlay ? 1 : 0;
            if (debugOverlay) debug.text = DebugText();

            // breathing mic note while the player is using their voice
            var voice = VoiceInput.I;
            bool micOn = voice && voice.Recording;
            bool thinking = voice && voice.Transcribing;
            float lvl = micOn ? Mathf.Clamp01(voice.Level * 25f) : thinking ? 0.25f + 0.25f * Mathf.Sin(Time.time * 8f) : 0f;
            micNote.text = micOn || thinking ? "♪" : "";
            micNote.rectTransform.localScale = Vector3.one * (1f + lvl * 0.6f);
            micNote.color = new Color(princeColor.r, princeColor.g, princeColor.b, 0.5f + lvl * 0.5f);
        }

        void OnSwitched(Sense s)
        {
            bool dark = s == Sense.Dark;
            nameCard.text = dark ? princeName : princessName;
            nameCard.color = dark ? princeColor : princessColor;
            if (nameCo != null) StopCoroutine(nameCo);
            nameCo = StartCoroutine(Pop(nameGroup, nameCardTiming));
        }

        string DebugText()
        {
            var sb = new System.Text.StringBuilder();
            var dir = AccessibilityDirector.I;
            var flow = GameFlow.I;
            sb.AppendLine($"[DEV] director {(dir && dir.Enabled ? "on" : "off")} (F2) · Jev {(JevClient.HasKey ? JevClient.LastStatus : "no key → rules")}");
            var vc = VoiceInput.I ? VoiceInput.I.GetComponent<VoiceCommands>() : null;
            if (vc && vc.LastDebug.Length > 0) sb.AppendLine("voice " + vc.LastDebug);
            sb.AppendLine($"[DEV] local AI {(LocalLLM.I ? LocalLLM.I.Status : "off")}{(WordFloatPlank.LastJudge.Length > 0 ? " · " + WordFloatPlank.LastJudge : "")}");
            if (SenseController.I) sb.AppendLine($"sense {(SenseController.I.IsDark ? "dark/prince" : "light/princess")} · echo x{SenseController.I.DirectorBoost:0.00}");
            if (flow && DuoController.I) sb.AppendLine($"zone {flow.CurrentZone} · falls {flow.Deaths} · switches {DuoController.I.SwitchCount}");
            if (dir) foreach (var l in dir.Log) sb.AppendLine("<color=#ffe08a>" + l + "</color>");
            return sb.ToString();
        }

        IEnumerator Pop(CanvasGroup g, PopTiming p) => Pop(g, p.hold, p.fadeIn, p.fadeOut);

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
            narrationCo = StartCoroutine(Pop(narrationGroup, narrationTiming));
        }

        public void ShowCaption(string msg)
        {
            caption.text = msg;
            if (captionCo != null) StopCoroutine(captionCo);
            captionCo = StartCoroutine(Pop(captionGroup, captionTiming));
        }

        public void ShowNotice(string msg)
        {
            notice.text = msg;
            if (noticeCo != null) StopCoroutine(noticeCo);
            noticeCo = StartCoroutine(Pop(noticeGroup, noticeTiming));
        }

        public void ShowTitle(string chapter, string name, string line)
        {
            titleSmall.text = chapter;
            titleBig.text = name;
            titleLine.text = line;
            StartCoroutine(Pop(titleGroup, titleTiming));
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
            clearStats.text = string.Format(clearStatsFormat, Mathf.FloorToInt(time / 60f), Mathf.FloorToInt(time % 60f), DuoController.I.SwitchCount, flow.Deaths);
            StartCoroutine(ClearRoutine());
        }

        public void ShowContinue(bool hasNext)
        {
            StartCoroutine(ContinueBlink(hasNext ? continueNext : continueEnd));
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
            for (float t = 0; t < 1; t += Time.deltaTime / Mathf.Max(0.01f, clearWashSeconds))
            {
                var w = clearWashColor; w.a = t * clearWashColor.a; clearWash.color = w;
                yield return null;
            }
            yield return Pop(clearGroup, 9999f, 1.5f, 1f);
        }
    }
}

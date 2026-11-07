using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// Shown once per run when the player starts from the title: (1) the voice and word puzzles run on a small AI on
    /// this computer — what this machine has, whether each model is loaded, and that every voice puzzle can also be
    /// solved without it; (2) a voice check — hold V, say the word, see what was heard.
    /// The models load in the background while the player reads, so the first words in the game don't wait.
    /// Layout lives in the TitleScreen prefab (AI Check); this only fills in values and animates.
    /// </summary>
    public class AiCheckPanel : MonoBehaviour
    {
        /// <summary>The check was finished (or skipped) this run — starting again goes straight in.</summary>
        public static bool Done { get; private set; }

        [Header("Panel")]
        public CanvasGroup group;
        [Range(0.05f, 2f)] public float fadeTime = 0.35f;
        public GameObject infoPage;
        public GameObject voicePage;

        [Header("Page 1 — this computer")]
        public Text gpuValue;
        public Text memoryValue;
        public Text micValue;
        public Text whisperValue;
        public Text llmValue;
        [Tooltip("One line summing up whether this machine runs the AI comfortably")]
        public Text verdict;
        public Button nextButton;
        [Tooltip("Skips the voice check and starts")]
        public Button skipButton;
        [Tooltip("Model pages (opened in the browser); same order as linkUrls")]
        public Button[] linkButtons;
        public string[] linkUrls =
        {
            "https://github.com/openai/whisper",
            "https://huggingface.co/kakaocorp/kanana-1.5-2.1b-instruct-2505",
        };

        [Header("Page 1 — thresholds")]
        [Tooltip("Graphics memory (MB) for both models on the GPU without slowing down")]
        public int goodVramMB = 4096;
        [Tooltip("Below this the models still run, but may be slow")]
        public int minVramMB = 2048;
        [Tooltip("System memory (MB)")]
        public int minRamMB = 8192;

        [Header("Page 2 — voice check")]
        [Tooltip("The word the player is asked to say")]
        public string testWord = "길어져";
        public Text promptText;
        [Tooltip("Ink fill scaled in x by the mic level while V is held (pivot on its left edge)")]
        public RectTransform levelFill;
        [Range(1f, 60f)] public float levelGain = 18f;
        public Text heardText;
        public Button startButton;
        public Button voiceSkipButton;

        [Header("Wording")]
        public string okMark = "◆";
        public string waitMark = "…";
        public string missMark = "—";
        public Color okColor = new Color(0.36f, 0.27f, 0.12f);
        public Color warnColor = new Color(0.55f, 0.2f, 0.12f);
        [TextArea] public string verdictGood = "이 컴퓨터라면 목소리와 말을 바로 알아들을 수 있어요.";
        [TextArea] public string verdictSlow = "AI가 조금 느리게 대답할 수 있어요. 그래도 모든 퍼즐은 풀 수 있어요.";
        [TextArea] public string verdictNoAi = "AI를 쓸 수 없는 환경이에요. 말 대신 고르기로 모든 퍼즐을 풀 수 있어요.";
        [TextArea] public string promptFormat = "V를 누른 채 「{0}」라고 말해 보세요.";
        public string listeningText = "듣고 있어요…";
        public string thinkingText = "알아듣는 중…";
        public string loadingText = "음성 인식을 준비하고 있어요…";
        public string noMicText = "마이크를 찾지 못했어요. 목소리 없이도 끝까지 갈 수 있어요.";
        public string tooShortText = "조금 더 길게 눌러 주세요.";
        public string emptyText = "잘 들리지 않았어요. 조금 더 크게 말해 볼까요?";
        [Tooltip("{0} = what was heard")]
        public string heardMatchFormat = "「{0}」 — 잘 들려요!";
        public string heardOtherFormat = "「{0}」(으)로 들렸어요. 한 번 더 해 볼까요?";

        Action onDone;
        bool open;
        float k;
        GameObject returnTo;

        public bool IsOpen => open;

        void Awake()
        {
            if (group) { group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false; }
            if (nextButton) nextButton.onClick.AddListener(ShowVoice);
            if (skipButton) skipButton.onClick.AddListener(Finish);
            if (startButton) startButton.onClick.AddListener(Finish);
            if (voiceSkipButton) voiceSkipButton.onClick.AddListener(Finish);
            if (linkButtons != null)
                for (int i = 0; i < linkButtons.Length; i++)
                {
                    int k = i;
                    if (linkButtons[i] && linkUrls != null && k < linkUrls.Length)
                        linkButtons[i].onClick.AddListener(() => Application.OpenURL(linkUrls[k]));
                }
        }

        void OnEnable()
        {
            var v = VoiceInput.I;
            if (!v) return;
            v.Heard += OnHeard;
            v.TooShort += OnTooShort;
        }

        void OnDisable()
        {
            var v = VoiceInput.I;
            if (!v) return;
            v.Heard -= OnHeard;
            v.TooShort -= OnTooShort;
        }

        /// <summary>Open the check; <paramref name="done"/> runs when the player moves on (start or skip).</summary>
        public void Open(Action done, GameObject returnFocus = null)
        {
            onDone = done;
            returnTo = returnFocus;
            open = true;
            // subscribe now if the voice object appeared after this panel was enabled
            OnDisable(); OnEnable();
            if (infoPage) infoPage.SetActive(true);
            if (voicePage) voicePage.SetActive(false);
            FillStatic();
            Select(nextButton);
        }

        void ShowVoice()
        {
            if (infoPage) infoPage.SetActive(false);
            if (voicePage) voicePage.SetActive(true);
            if (promptText) promptText.text = string.Format(promptFormat, testWord);
            SetHeard(VoiceInput.I && VoiceInput.I.Available ? "" : noMicText, warnColor);
            Select(startButton);
        }

        void Finish()
        {
            if (!open) return;
            open = false;
            Done = true;
            var cb = onDone; onDone = null;
            cb?.Invoke();
        }

        void Update()
        {
            k = Mathf.MoveTowards(k, open ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime));
            if (group)
            {
                group.alpha = Mathf.SmoothStep(0f, 1f, k);
                group.interactable = open;
                group.blocksRaycasts = open || k > 0.01f;
            }
            if (!open) return;

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Finish();

            if (infoPage && infoPage.activeSelf) FillLive();
            if (voicePage && voicePage.activeSelf) VoiceTick();
        }

        // ---------------------------------------------------------------- page 1
        void FillStatic()
        {
            int vram = SystemInfo.graphicsMemorySize, ram = SystemInfo.systemMemorySize;
            if (gpuValue)
            {
                gpuValue.text = $"{(vram >= minVramMB ? okMark : missMark)} {SystemInfo.graphicsDeviceName} · {Gb(vram)}";
                gpuValue.color = vram >= minVramMB ? okColor : warnColor;
            }
            if (memoryValue)
            {
                memoryValue.text = $"{(ram >= minRamMB ? okMark : missMark)} {Gb(ram)}";
                memoryValue.color = ram >= minRamMB ? okColor : warnColor;
            }
            if (micValue)
            {
                bool mic = Microphone.devices.Length > 0;
                micValue.text = mic ? $"{okMark} {GameSettings.ResolveMicDevice() ?? Microphone.devices[0]}" : $"{missMark} 없음";
                micValue.color = mic ? okColor : warnColor;
            }
            FillLive();
        }

        void FillLive()
        {
            var v = VoiceInput.I;
            var llm = LocalLLM.I;
            if (whisperValue)
            {
                bool ok = v && v.ModelReady, wait = v && v.ModelLoading;
                whisperValue.text = ok ? $"{okMark} 준비됨" : wait ? $"{waitMark} 불러오는 중" : $"{missMark} 없음";
                whisperValue.color = ok || wait ? okColor : warnColor;
            }
            bool llmOk = llm && llm.Ready && llm.Status == "ready";
            bool llmWait = llm && !llmOk && (llm.Status == "loading" || llm.Status == "warming up" || llm.Status == "off");
            if (llmValue)
            {
                llmValue.text = llmOk ? $"{okMark} 준비됨" : llmWait ? $"{waitMark} 불러오는 중" : $"{missMark} 쓸 수 없음";
                llmValue.color = llmOk || llmWait ? okColor : warnColor;
            }
            if (verdict)
            {
                bool noAi = !(v && (v.ModelReady || v.ModelLoading)) && !(llmOk || llmWait);
                bool slow = SystemInfo.graphicsMemorySize < goodVramMB || SystemInfo.systemMemorySize < minRamMB
                            || !(llmOk || llmWait) || !(v && (v.ModelReady || v.ModelLoading));
                verdict.text = noAi ? verdictNoAi : slow ? verdictSlow : verdictGood;
                verdict.color = noAi || SystemInfo.graphicsMemorySize < minVramMB ? warnColor : okColor;
            }
        }

        static string Gb(int mb) => mb <= 0 ? "?" : $"{mb / 1024f:0.#}GB";

        // ---------------------------------------------------------------- page 2
        void VoiceTick()
        {
            var v = VoiceInput.I;
            float lvl = v && v.Recording ? Mathf.Clamp01(v.Level * levelGain) : 0f;
            if (levelFill)
            {
                float x = Mathf.Lerp(levelFill.localScale.x, lvl, Time.unscaledDeltaTime * 18f);
                levelFill.localScale = new Vector3(x, 1f, 1f);
            }
            if (!v || !v.Available) return;
            if (v.Recording) SetHeard(listeningText, okColor);
            else if (v.Transcribing) SetHeard(v.ModelReady ? thinkingText : loadingText, okColor);
        }

        void OnHeard(string text)
        {
            if (!open || !voicePage || !voicePage.activeSelf) return;
            if (string.IsNullOrEmpty(text)) { SetHeard(emptyText, warnColor); return; }
            bool match = text.Replace(" ", "").Contains(testWord) || WordJudge.NearPiece(text.Replace(" ", ""), new[] { testWord });
            SetHeard(string.Format(match ? heardMatchFormat : heardOtherFormat, text), match ? okColor : warnColor);
            if (match) Select(startButton);
        }

        void OnTooShort()
        {
            if (open && voicePage && voicePage.activeSelf) SetHeard(tooShortText, warnColor);
        }

        void SetHeard(string s, Color c)
        {
            if (!heardText) return;
            heardText.text = s;
            heardText.color = c;
        }

        static void Select(Button b)
        {
            if (b && EventSystem.current) EventSystem.current.SetSelectedGameObject(b.gameObject);
        }
    }
}

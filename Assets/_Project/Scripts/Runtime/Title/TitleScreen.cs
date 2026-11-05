using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// Title screen director: intro sequence (sky fades up → portraits burn in → logo inks in → menu rises),
    /// the three menu actions (start / settings / quit), and the hand-off fade into the first stage.
    /// All elements are referenced from the Title prefab; this only animates them.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        [Header("Layers")]
        public TitleLayer backdrop;
        public TitleLayer embers;
        public TitleLayer princess;
        public TitleLayer prince;
        public TitleLogo logo;

        [Header("Menu")]
        public CanvasGroup menu;
        public Button startButton;
        public Button settingsButton;
        public Button quitButton;
        public SettingsPanel settings;
        [Tooltip("Pixels the menu rises while fading in.")]
        public float menuRise = 24f;

        [Header("Stage select")]
        [Tooltip("Menu button that opens the stage list (단계 선택)")]
        public Button stageSelectButton;
        [Tooltip("The stage list shown in place of the menu")]
        public CanvasGroup stageMenu;
        [Tooltip("Stage buttons, in order; each loads the scene of the same index in stageScenes")]
        public Button[] stageButtons;
        public string[] stageScenes = { "Stage1_FirstTwilight", "Stage2_SunsetLake", "Stage3_EchoCave", "Stage4_TwilightGate", "Stage5_TwilightBridge" };
        public Button stageBackButton;

        [Header("Scene change")]
        [Tooltip("Shared black cover (Prefabs/UI/SceneTransition): fade to black → load → hold → fade in.")]
        public SceneTransition transition;

        [Header("Timing (seconds)")]
        [Range(0f, 5f)] public float backdropTime = 2.4f;
        [Range(0f, 5f)] public float portraitDelay = 0.8f;
        [Range(0.1f, 5f)] public float portraitTime = 2.2f;
        [Tooltip("Gap between the princess and the prince appearing.")]
        [Range(0f, 2f)] public float portraitStagger = 0.35f;
        [Range(0f, 5f)] public float logoDelay = 1.8f;
        [Range(0f, 5f)] public float menuDelay = 3.6f;
        [Range(0.1f, 3f)] public float menuTime = 0.9f;
        [Tooltip("Menu fade-out / music fade-out when 게임 시작 is pressed.")]
        [Range(0.1f, 5f)] public float startFadeTime = 0.8f;
        [Tooltip("Any key / click during the intro skips to the finished screen.")]
        public bool skippable = true;

        [Header("Flow")]
        [Tooltip("Scene loaded by 게임 시작.")]
        public string firstScene = "Stage1_FirstTwilight";

        [Header("Music")]
        public AudioSource music;
        [Range(0.1f, 5f)] public float musicFadeIn = 3f;

        bool introDone, leaving;
        RectTransform menuRt;
        Vector2 menuPos;
        Coroutine intro;

        void Start()
        {
            GameEvents.Reset();
            GameFlow.ModalOpen = false;

            if (startButton) startButton.onClick.AddListener(StartGame);
            if (settingsButton) settingsButton.onClick.AddListener(OpenSettings);
            if (quitButton) quitButton.onClick.AddListener(Quit);
            if (settings) settings.Closed += () => SetMenuInteractable(true);
            if (stageSelectButton) stageSelectButton.onClick.AddListener(() => ShowStages(true));
            if (stageBackButton) stageBackButton.onClick.AddListener(() => ShowStages(false));
            if (stageButtons != null)
                for (int i = 0; i < stageButtons.Length; i++)
                {
                    int k = i;
                    if (stageButtons[i]) stageButtons[i].onClick.AddListener(() => StartAt(k < stageScenes.Length ? stageScenes[k] : firstScene));
                }
            SetStages(false);

            menuRt = menu ? (RectTransform)menu.transform : null;
            if (menuRt) menuPos = menuRt.anchoredPosition;

            intro = StartCoroutine(Intro());
        }

        IEnumerator Intro()
        {
            foreach (var l in Layers) if (l) l.Hide();
            if (logo) logo.Hide();
            SetMenu(0f);
            SetMenuInteractable(false);
            if (music) { music.volume = 0f; music.Play(); StartCoroutine(FadeMusic(1f, musicFadeIn)); }

            if (backdrop) backdrop.Reveal(backdropTime);
            if (embers) embers.Reveal(backdropTime * 1.5f, backdropTime * 0.5f);
            if (princess) princess.Reveal(portraitTime, portraitDelay);
            if (prince) prince.Reveal(portraitTime, portraitDelay + portraitStagger);

            float t = 0f;
            bool logoStarted = false;
            while (t < menuDelay)
            {
                t += Time.unscaledDeltaTime;
                if (!logoStarted && t >= logoDelay) { if (logo) logo.Reveal(); logoStarted = true; }
                yield return null;
            }
            for (float k = 0f; k < 1f; k += Time.unscaledDeltaTime / menuTime)
            {
                SetMenu(k);
                yield return null;
            }
            FinishIntro();
        }

        TitleLayer[] Layers => new[] { backdrop, embers, princess, prince };

        void FinishIntro()
        {
            if (introDone) return;
            introDone = true;
            SetMenu(1f);
            SetMenuInteractable(true);
            if (startButton && EventSystem.current) EventSystem.current.SetSelectedGameObject(startButton.gameObject);
        }

        void SkipIntro()
        {
            if (intro != null) StopCoroutine(intro);
            foreach (var l in Layers) if (l && !l.Revealed) l.Reveal(0.5f);
            if (logo) logo.Reveal();
            FinishIntro();
        }

        void Update()
        {
            if (stagesOpen && !leaving)
            {
                var k2 = UnityEngine.InputSystem.Keyboard.current; var p2 = UnityEngine.InputSystem.Gamepad.current;
                if ((k2 != null && k2.escapeKey.wasPressedThisFrame) || (p2 != null && p2.buttonEast.wasPressedThisFrame)) ShowStages(false);
            }
            if (introDone || !skippable) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            bool any = (kb != null && kb.anyKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame)
                       || (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame));
            if (any && Time.timeSinceLevelLoad > 0.3f) SkipIntro();
        }

        void SetMenu(float k)
        {
            if (!menu) return;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(k), 3f);
            menu.alpha = e;
            if (menuRt) menuRt.anchoredPosition = menuPos - new Vector2(0, menuRise * (1f - e));
        }

        void SetMenuInteractable(bool on)
        {
            if (!menu) return;
            menu.interactable = on && !leaving;
            menu.blocksRaycasts = on && !leaving;
        }

        bool stagesOpen;

        void SetStages(bool on)
        {
            stagesOpen = on;
            if (!stageMenu) return;
            stageMenu.alpha = on ? 1f : 0f;
            stageMenu.interactable = on && !leaving;
            stageMenu.blocksRaycasts = on && !leaving;
        }

        /// <summary>단계 선택: the stage list takes the menu's place (and back).</summary>
        void ShowStages(bool on)
        {
            if (leaving || !stageMenu) return;
            SetStages(on);
            if (menu) { menu.alpha = on ? 0f : 1f; }
            SetMenuInteractable(!on);
            var select = on ? (stageButtons != null && stageButtons.Length > 0 ? stageButtons[0] : stageBackButton) : stageSelectButton;
            if (select && EventSystem.current) EventSystem.current.SetSelectedGameObject(select.gameObject);
        }

        void StartAt(string scene)
        {
            if (leaving) return;
            leaving = true;
            SetMenuInteractable(false);
            if (stageMenu) { stageMenu.interactable = false; stageMenu.blocksRaycasts = false; }
            StartCoroutine(Leave(scene));
        }

        void OpenSettings()
        {
            if (leaving || !settings) return;
            SetMenuInteractable(false);
            settings.Open(settingsButton ? settingsButton.gameObject : null);
        }

        void StartGame()
        {
            if (leaving) return;
            leaving = true;
            SetMenuInteractable(false);
            StartCoroutine(Leave(firstScene));
        }

        IEnumerator Leave(string scene)
        {
            if (music) StartCoroutine(FadeMusic(0f, startFadeTime));
            SceneTransition.Load(scene, transition);
            var shown = stageMenu && stageMenu.alpha > 0.5f ? stageMenu : menu;   // fade whichever list is up
            for (float k = 0f; k < 1f; k += Time.unscaledDeltaTime / startFadeTime)
            {
                if (shown) shown.alpha = 1f - k;
                yield return null;
            }
            if (shown) shown.alpha = 0f;
        }

        IEnumerator FadeMusic(float to, float time)
        {
            if (!music) yield break;
            float from = music.volume;
            for (float k = 0f; k < 1f; k += Time.unscaledDeltaTime / time)
            {
                music.volume = Mathf.Lerp(from, to, k);
                yield return null;
            }
            music.volume = to;
        }

        void Quit()
        {
            if (leaving) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

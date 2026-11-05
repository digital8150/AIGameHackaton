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

        [Header("Fade")]
        [Tooltip("Full-screen image used for the fade to the first stage.")]
        public Graphic fader;
        public Color fadeColor = new Color(1f, 0.86f, 0.62f, 1f);

        [Header("Timing (seconds)")]
        [Range(0f, 5f)] public float backdropTime = 2.4f;
        [Range(0f, 5f)] public float portraitDelay = 0.8f;
        [Range(0.1f, 5f)] public float portraitTime = 2.2f;
        [Tooltip("Gap between the princess and the prince appearing.")]
        [Range(0f, 2f)] public float portraitStagger = 0.35f;
        [Range(0f, 5f)] public float logoDelay = 1.8f;
        [Range(0f, 5f)] public float menuDelay = 3.6f;
        [Range(0.1f, 3f)] public float menuTime = 0.9f;
        [Range(0.1f, 5f)] public float startFadeTime = 1.4f;
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

            menuRt = menu ? (RectTransform)menu.transform : null;
            if (menuRt) menuPos = menuRt.anchoredPosition;
            if (fader) { fader.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f); fader.raycastTarget = false; }

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
            StartCoroutine(Leave());
        }

        IEnumerator Leave()
        {
            var op = SceneManager.LoadSceneAsync(firstScene);
            if (op != null) op.allowSceneActivation = false;
            if (music) StartCoroutine(FadeMusic(0f, startFadeTime));
            if (fader) fader.raycastTarget = true;
            for (float k = 0f; k < 1f; k += Time.unscaledDeltaTime / startFadeTime)
            {
                float e = k * k;
                if (fader) fader.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, e);
                if (menu) menu.alpha = 1f - Mathf.Clamp01(k * 2f);
                yield return null;
            }
            if (fader) fader.color = fadeColor;
            if (op == null) { Debug.LogError($"[Title] Scene '{firstScene}' is not in the build settings."); leaving = false; yield break; }
            op.allowSceneActivation = true;
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

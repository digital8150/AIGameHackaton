using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// In-game Esc menu: a memo note slides onto the screen (continue / settings / back to title) and the stage pauses
    /// (time stops, world sounds pause — the music keeps playing, input is locked through GameFlow.ModalOpen).
    /// Layout lives in Prefabs/UI/PauseMenu; this only opens, closes and animates it.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public CanvasGroup group;
        [Tooltip("The note sheet that slides in.")]
        public RectTransform note;

        [Header("Buttons")]
        public Button resume;
        public Button settingsButton;
        public Button toTitle;
        [Tooltip("Shared settings page (Prefabs/UI/SettingsPanel instance).")]
        public SettingsPanel settings;

        [Header("Flow")]
        public string titleScene = "Title";
        [Tooltip("Black cover for going back to the title. Empty = the stage's GameFlow.transition.")]
        public SceneTransition transition;

        [Header("Motion")]
        [Range(0.05f, 1f)] public float fadeTime = 0.22f;
        [Tooltip("Where the note starts from (px, relative to its resting place).")]
        public Vector2 openOffset = new Vector2(0f, -50f);
        [Range(-15f, 15f)] public float openTilt = 5f;
        [Range(-5f, 5f)] public float restTilt = 1.2f;

        [Header("Page turn")]
        [Tooltip("Open/close by turning a storybook page (Prefabs/UI/PageTurn). Empty = the stage's GameFlow.pageTurn, else slide.")]
        public PageTurn pageTurn;
        [Range(0.2f, 2f)] public float flipTime = 0.7f;
        [Range(-60f, 60f)] public float flipAngle = -12f;

        [Header("Sound (slide fallback only — the page turn has its own)")]
        public AudioSource sfx;
        public AudioClip pageClip;
        [Range(0f, 1f)] public float pageVolume = 0.6f;

        public bool IsOpen { get; private set; }

        float openK;
        Vector2 restPos;
        int closedFrame = -1;
        bool leaving;
        bool wasModal;   // another panel (word altar) closing on this same Esc must not open the menu
        bool flipPending;   // a page turn is running; the visible state switches when the page is captured

        void Awake()
        {
            if (!group) group = GetComponent<CanvasGroup>();
            if (note) restPos = note.anchoredPosition;
            if (resume) resume.onClick.AddListener(Close);
            if (settingsButton) settingsButton.onClick.AddListener(OpenSettings);
            if (toTitle) toTitle.onClick.AddListener(ToTitle);
            if (settings) settings.Closed += () =>
            {
                closedFrame = Time.frameCount;
                SetButtons(true);
            };
            if (sfx) sfx.ignoreListenerPause = true;
            // stages have no EventSystem of their own; the menu needs one for mouse / keyboard / pad navigation
            if (!FindAnyObjectByType<EventSystem>())
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Apply(0f);
            group.blocksRaycasts = group.interactable = false;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool esc = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
            bool back = pad != null && pad.buttonEast.wasPressedThisFrame;

            if (!leaving && !PageTurn.Busy && Time.frameCount != closedFrame && !(settings && settings.IsOpen))
            {
                if (IsOpen && (esc || back)) Close();
                else if (!IsOpen && esc && !wasModal && CanOpen()) Open();
            }

            wasModal = GameFlow.ModalOpen && !IsOpen;
            if (!flipPending)
            {
                openK = Mathf.MoveTowards(openK, IsOpen ? 1f : 0f, Time.unscaledDeltaTime / fadeTime);
                Apply(openK);
            }
        }

        // not during fades (stage intro, respawn) or another modal panel
        static bool CanOpen() => !GameFlow.InputLocked && !SceneTransition.Busy;

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            GameFlow.ModalOpen = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            group.blocksRaycasts = true;
            SetButtons(true);
            if (resume && EventSystem.current) EventSystem.current.SetSelectedGameObject(resume.gameObject);
            if (TryFlip(1f)) flipPending = true; else Play();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            closedFrame = Time.frameCount;
            group.blocksRaycasts = group.interactable = false;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            Resume();
            bool withSettings = settings && settings.IsOpen;
            if (TryFlip(0f, withSettings)) flipPending = true;
            else { if (withSettings) settings.Close(false); Play(); }
        }

        void Resume()
        {
            GameFlow.ModalOpen = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        void OpenSettings()
        {
            if (!settings) return;
            SetButtons(false);
            settings.Open(settingsButton ? settingsButton.gameObject : null);
        }

        void ToTitle()
        {
            if (leaving) return;
            leaving = true;
            group.interactable = false;
            Resume();
            var t = transition ? transition : (GameFlow.I ? GameFlow.I.transition : null);
            SceneTransition.Load(titleScene, t);
        }

        void SetButtons(bool on) => group.interactable = on && IsOpen;

        void Apply(float k)
        {
            float e = k * k * (3f - 2f * k);
            group.alpha = e;
            if (!note) return;
            float b = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);
            note.anchoredPosition = restPos + openOffset * (1f - b);
            note.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(openTilt, restTilt, b));
        }

        bool TryFlip(float target, bool closeSettings = false)
        {
            var prefab = pageTurn ? pageTurn : (GameFlow.I ? GameFlow.I.pageTurn : null);
            var t = PageTurn.I ? PageTurn.I : PageTurn.Ensure(prefab);
            if (!t || !isActiveAndEnabled) return false;
            return t.Flip(() =>
            {
                flipPending = false;
                if (closeSettings) settings.Close(false);
                openK = target;
                Apply(target);
            }, flipTime, flipAngle);
        }

        void Play()
        {
            if (sfx && pageClip && isActiveAndEnabled) sfx.PlayOneShot(pageClip, pageVolume);
        }

        void OnDisable()
        {
            if (IsOpen) { IsOpen = false; Resume(); }
        }
    }
}

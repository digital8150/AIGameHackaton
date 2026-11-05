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
        [Tooltip("Back to the last checkpoint (boxes from there on go back too) — for when a box got stuck")]
        public Button toCheckpoint;
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

        [Header("Storybook")]
        [Tooltip("The note is a small book that opens (StorybookBook on its spread). Empty = the note slides in.")]
        public StorybookBook book;
        [Range(0.3f, 3f)] public float bookOpenTime = 0.9f;
        [Range(0.2f, 3f)] public float bookCloseTime = 0.6f;
        [Tooltip("Seconds for the page to turn between this menu and the settings pages (same book, needs the book's leaf faces and the same page size).")]
        [Range(0.2f, 2f)] public float pageTurnTime = 0.7f;

        [Header("Sound")]
        public AudioSource sfx;
        public AudioClip pageClip;
        [Range(0f, 1f)] public float pageVolume = 0.6f;

        public bool IsOpen { get; private set; }

        float openK;
        Vector2 restPos;
        int closedFrame = -1;
        bool leaving;
        bool wasModal;   // another panel (word altar) closing on this same Esc must not open the menu
        bool settingsInBook;
        float turnK;     // 0 = menu pages, 1 = settings pages

        void Awake()
        {
            if (!group) group = GetComponent<CanvasGroup>();
            if (note) restPos = note.anchoredPosition;
            if (resume) resume.onClick.AddListener(Close);
            if (settingsButton) settingsButton.onClick.AddListener(OpenSettings);
            if (toTitle) toTitle.onClick.AddListener(ToTitle);
            if (toCheckpoint) toCheckpoint.onClick.AddListener(() => { Close(); GameFlow.I?.RestartFromCheckpoint(); });
            if (settings) settings.Closed += () =>
            {
                closedFrame = Time.frameCount;
                SetButtons(true);
            };
            settingsInBook = settings && settings.book && book && book.leafFront && book.leafBack;
            if (settingsInBook) MoveSettingsIntoBook();
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

            if (!leaving && Time.frameCount != closedFrame && !(settings && settings.IsOpen) && turnK <= 0f)
            {
                if (IsOpen && (esc || back)) Close();
                else if (!IsOpen && esc && !wasModal && CanOpen()) Open();
            }

            wasModal = GameFlow.ModalOpen && !IsOpen;
            float time = book ? (IsOpen ? bookOpenTime : bookCloseTime) : fadeTime;
            openK = Mathf.MoveTowards(openK, IsOpen ? 1f : 0f, Time.unscaledDeltaTime / time);
            Apply(openK);
            if (settingsInBook) ApplyTurn();
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
            Play();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            closedFrame = Time.frameCount;
            group.blocksRaycasts = group.interactable = false;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            Resume();
            if (settings && settings.IsOpen) settings.Close(false);
            turnK = 0f;
            Play();
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
            if (!settingsInBook) SetButtons(false);
            settings.Open(settingsButton ? settingsButton.gameObject : null);
        }

        // back to front: what the leaf uncovers, the old left page it lands on, then each face with the content it carries
        void MoveSettingsIntoBook()
        {
            settings.EmbedPagesIn(book.transform);
            int i = 0;
            settings.RightPage.transform.SetSiblingIndex(i++);
            book.leftPage.transform.SetSiblingIndex(i++);
            book.leafFront.transform.SetSiblingIndex(i++);
            book.rightPage.transform.SetSiblingIndex(i++);
            book.leafBack.transform.SetSiblingIndex(i++);
            settings.LeftPage.transform.SetSiblingIndex(i);
            ApplyTurn();
        }

        // The leaf is blank paper; page content sits on it, squeezed towards the spine (page pivots are on the spine).
        void ApplyTurn()
        {
            turnK = Mathf.MoveTowards(turnK, settings.IsOpen ? 1f : 0f, Time.unscaledDeltaTime / pageTurnTime);
            float angle = 180f * turnK * turnK * (3f - 2f * turnK);
            float squeeze = Mathf.Abs(Mathf.Cos(angle * Mathf.Deg2Rad));
            bool atMenu = turnK <= 0f, atSettings = turnK >= 1f;
            book.SetLeaf(angle);

            if (!atMenu)   // at rest the book's opening (Set) owns the menu pages' alpha
            {
                book.leftPage.alpha = atSettings ? 0f : 1f;
                book.rightPage.alpha = angle < 90f ? 1f : 0f;
            }
            book.rightPage.transform.localScale = new Vector3(angle < 90f ? squeeze : 1f, 1f, 1f);
            SetPageInput(book.leftPage, atMenu);
            SetPageInput(book.rightPage, atMenu);

            settings.RightPage.alpha = atMenu ? 0f : 1f;
            settings.LeftPage.alpha = angle >= 90f ? 1f : 0f;
            settings.LeftPage.transform.localScale = new Vector3(angle >= 90f ? squeeze : 1f, 1f, 1f);
            SetPageInput(settings.RightPage, atSettings);
            SetPageInput(settings.LeftPage, atSettings);
        }

        static void SetPageInput(CanvasGroup page, bool on) => page.blocksRaycasts = page.interactable = on;

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
            if (book)
            {
                float d = Mathf.Clamp01(k / 0.15f);
                group.alpha = d * d * (3f - 2f * d);
                book.Set(k);
                return;
            }
            float e = k * k * (3f - 2f * k);
            group.alpha = e;
            if (!note) return;
            float b = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);
            note.anchoredPosition = restPos + openOffset * (1f - b);
            note.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(openTilt, restTilt, b));
        }

        void Play()
        {
            if (sfx && pageClip && isActiveAndEnabled) sfx.PlayLimited(pageClip, pageVolume);
        }

        void OnDisable()
        {
            if (IsOpen) { IsOpen = false; Resume(); }
        }
    }
}

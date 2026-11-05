using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// 황혼의 그림책 — a cutscene told as pages of a worn picture book. Each page is an illustration (shown through the
    /// worn-book page shader) with a line or two of narration on top. Jump / interact / click turns the page with the same
    /// curl as the stage transition; the last page turns straight into the next scene. Esc / Start skips.
    /// Layout lives in the prefab (Prefabs/UI/Storybook); this only animates.
    /// </summary>
    public class StorybookCutscene : MonoBehaviour
    {
        [Serializable]
        public class Page
        {
            public Texture2D picture;
            [TextArea(2, 4)] public string text;
        }

        public Page[] pages;

        [Header("Layout (prefab)")]
        [Tooltip("The page lying underneath (the next one while a page turns)")]
        public RawImage under;
        [Tooltip("The page being turned away")]
        public RawImage over;
        public Text caption;
        public CanvasGroup captionGroup;
        [Tooltip("Small ▶ that blinks when the page can be turned")]
        public CanvasGroup nextHint;
        [Tooltip("Black cover for the first fade-in")]
        public CanvasGroup cover;

        [Header("Timing (seconds)")]
        public float fadeIn = 1.4f;
        public float textDelay = 0.25f;
        public float textFade = 0.7f;
        [Tooltip("Minimum time a page stays before it can be turned")]
        public float minShow = 0.8f;
        public float turnTime = 1.3f;

        [Header("Curl")]
        [Range(-60f, 60f)] public float angle = -18f;
        [Range(0.02f, 0.4f)] public float curlRadius = 0.12f;

        [Header("Sound")]
        public AudioSource sfx;
        public AudioClip turnClip;
        [Range(0, 1)] public float turnVolume = 0.8f;
        public AudioSource music;
        public float musicFade = 2f;

        [Header("Next")]
        public string nextScene = "Stage1_FirstTwilight";
        [Tooltip("Page turn into the first stage (Prefabs/UI/PageTurn)")]
        public PageTurn pageTurn;
        public SceneTransition fallbackTransition;

        static readonly int IdProgress = Shader.PropertyToID("_Progress");
        static readonly int IdAngle = Shader.PropertyToID("_Angle");
        static readonly int IdRadius = Shader.PropertyToID("_Radius");
        static readonly int IdAge = Shader.PropertyToID("_Age");

        Material underMat, overMat;
        bool leaving;
        float musicVolume;

        void Awake()
        {
            underMat = new Material(under.material); under.material = underMat;
            overMat = new Material(over.material); over.material = overMat;
            foreach (var m in new[] { underMat, overMat })
            {
                m.SetFloat(IdAge, 1f);
                m.SetFloat(IdProgress, 0f);
                m.SetFloat(IdAngle, angle);
                m.SetFloat(IdRadius, curlRadius);
            }
            over.enabled = false;
            captionGroup.alpha = 0f;
            if (nextHint) nextHint.alpha = 0f;
            if (cover) cover.alpha = 1f;
            if (music) { musicVolume = music.volume; music.volume = 0f; }
        }

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            if (pages == null || pages.Length == 0) { Leave(); yield break; }
            if (music) { music.Play(); StartCoroutine(Fade(v => music.volume = v, 0f, musicVolume, musicFade)); }
            under.texture = pages[0].picture;
            if (cover) yield return Fade(v => cover.alpha = v, 1f, 0f, fadeIn);

            for (int i = 0; i < pages.Length; i++)
            {
                // narration
                caption.text = pages[i].text;
                yield return Wait(textDelay);
                yield return Fade(v => captionGroup.alpha = v, 0f, 1f, textFade);
                yield return Wait(minShow);

                // wait for the reader
                float t0 = Time.unscaledTime;
                while (!Advance())
                {
                    if (leaving) yield break;
                    if (nextHint) nextHint.alpha = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin((Time.unscaledTime - t0) * 2.4f));
                    yield return null;
                }
                if (nextHint) nextHint.alpha = 0f;
                yield return Fade(v => captionGroup.alpha = v, 1f, 0f, 0.25f);

                if (i == pages.Length - 1) { Leave(); yield break; }

                // turn: the current page curls away over the next one
                over.texture = pages[i].picture;
                overMat.SetFloat(IdProgress, 0f);
                over.enabled = true;
                under.texture = pages[i + 1].picture;
                if (sfx && turnClip) sfx.PlayOneShot(turnClip, turnVolume);
                for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / turnTime)
                {
                    overMat.SetFloat(IdProgress, k);
                    yield return null;
                }
                over.enabled = false;
            }
        }

        void Update()
        {
            if (leaving) return;
            var kb = Keyboard.current; var pad = Gamepad.current;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame))
            {
                StopAllCoroutines();
                Leave();
            }
        }

        bool requested;
        /// <summary>Turn the page as if the reader pressed a key (voice, tests).</summary>
        public void RequestNext() => requested = true;

        bool Advance()
        {
            if (requested) { requested = false; return true; }
            var kb = Keyboard.current; var pad = Gamepad.current; var mouse = Mouse.current;
            return (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame))
                || (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame))
                || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        }

        void Leave()
        {
            if (leaving) return;
            leaving = true;
            over.enabled = false;
            if (nextHint) nextHint.alpha = 0f;
            if (music) StartCoroutine(Fade(v => music.volume = v, music.volume, 0f, 1.2f));
            // the book is already worn: the last page turns straight into the first stage
            if (!PageTurn.Load(nextScene, pageTurn, ageFirst: false)) SceneTransition.Load(nextScene, fallbackTransition);
        }

        static IEnumerator Wait(float s) { float u = Time.unscaledTime + s; while (Time.unscaledTime < u) yield return null; }

        static IEnumerator Fade(Action<float> set, float a, float b, float time)
        {
            for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / Mathf.Max(0.01f, time))
            {
                set(Mathf.Lerp(a, b, k * k * (3f - 2f * k)));
                yield return null;
            }
            set(b);
        }
    }
}

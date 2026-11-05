using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// Stage → stage as turning a page of a picture book: the last frame of the old stage is captured onto a
    /// full-screen page, the next stage loads behind it, and the page curls up from a corner and peels away
    /// (shader DuskTwilight/UI/PageTurn). Layout lives in the prefab (Prefabs/UI/PageTurn); this only animates.
    /// One instance survives the load; spawned from the prefab on first use.
    /// </summary>
    public class PageTurn : MonoBehaviour
    {
        public static PageTurn I { get; private set; }
        public static bool Busy => I && I.busy;
        /// <summary>True while a page is covering a freshly loaded stage — its intro should not fade in from black.</summary>
        public static bool Arriving { get; private set; }

        [Tooltip("Top-most canvas holding the page")]
        public Canvas canvas;
        [Tooltip("Full-screen page (material: M_PageTurn)")]
        public RawImage page;

        [Header("Timing (seconds, unscaled)")]
        [Range(0f, 2f)] public float holdBefore = 0.35f;
        [Range(0, 30)] public int settleFrames = 6;
        [Range(0.3f, 4f)] public float turnTime = 1.6f;
        [Tooltip("The last frame fades into a worn storybook page (sepia, frayed edges, spine) before it turns. 0 = no worn look")]
        [Range(0f, 3f)] public float ageTime = 0.8f;

        [Header("Curl")]
        [Tooltip("Angle of the fold (deg). 0 = straight vertical fold moving right → left")]
        [Range(-60f, 60f)] public float angle = -18f;
        [Tooltip("Radius of the curl, as a share of the screen height")]
        [Range(0.02f, 0.4f)] public float curlRadius = 0.11f;

        [Header("Sound")]
        public AudioClip turnClip;
        [Range(0, 1)] public float turnVolume = 0.8f;

        static readonly int IdProgress = Shader.PropertyToID("_Progress");
        static readonly int IdAngle = Shader.PropertyToID("_Angle");
        static readonly int IdRadius = Shader.PropertyToID("_Radius");
        static readonly int IdAge = Shader.PropertyToID("_Age");

        bool busy;
        Material mat;
        AudioSource sfx;

        public static PageTurn Ensure(PageTurn prefab)
        {
            if (I) return I;
            return prefab ? Instantiate(prefab) : null;
        }

        /// <summary>Turn the page to <paramref name="scene"/>; false if there is no page prefab (caller falls back).</summary>
        public static bool Load(string scene, PageTurn prefab, bool ageFirst = true)
        {
            var t = Ensure(prefab);
            if (!t) return false;
            t.Go(scene, ageFirst);
            return true;
        }

        void Awake()
        {
            if (I && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
            mat = new Material(page.material);
            page.material = mat;
            canvas.enabled = false;
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.ignoreListenerVolume = true;   // the princess's silent sense must not mute the page
        }

        void OnDestroy() { if (I == this) { I = null; Arriving = false; } }

        /// <param name="ageFirst">Fade the frame into a worn page first (off when the screen already is one, e.g. the storybook).</param>
        public void Go(string scene, bool ageFirst = true)
        {
            if (busy) return;
            StartCoroutine(Run(scene, ageFirst));
        }

        /// <summary>
        /// Turn the page in place, without a scene change: the current frame is captured onto the page,
        /// <paramref name="swap"/> changes what is underneath (e.g. opens a menu), then the page peels away.
        /// Runs on unscaled time, so it also works while the game is paused. False if a turn is already running.
        /// </summary>
        public bool Flip(System.Action swap, float time = 0.9f, float flipAngle = -12f, float age = 0f)
        {
            if (busy) return false;
            StartCoroutine(RunFlip(swap, time, flipAngle, age));
            return true;
        }

        IEnumerator RunFlip(System.Action swap, float time, float flipAngle, float age)
        {
            busy = true;
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            page.texture = tex;
            mat.SetFloat(IdProgress, 0f);
            mat.SetFloat(IdAngle, flipAngle);
            mat.SetFloat(IdRadius, curlRadius);
            mat.SetFloat(IdAge, age);
            canvas.enabled = true;
            swap?.Invoke();
            yield return null;   // let the new state lay itself out behind the page
            yield return null;
            if (turnClip) sfx.PlayOneShot(turnClip, turnVolume);
            for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / Mathf.Max(time, 0.05f))
            {
                mat.SetFloat(IdProgress, k);
                yield return null;
            }
            mat.SetFloat(IdProgress, 1f);
            canvas.enabled = false;
            page.texture = null;
            Destroy(tex);
            busy = false;
        }

        IEnumerator Run(string scene, bool ageFirst)
        {
            float ageTime = ageFirst ? this.ageTime : 0f;
            busy = true;
            // 1. capture the old stage's last frame (with its clear card) as the page
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            page.texture = tex;
            mat.SetFloat(IdProgress, 0f);
            mat.SetFloat(IdAngle, angle);
            mat.SetFloat(IdRadius, curlRadius);
            mat.SetFloat(IdAge, 0f);
            canvas.enabled = true;
            Arriving = true;
            float ageStart = Time.unscaledTime;

            // 2. load the next stage behind the page
            var op = SceneManager.LoadSceneAsync(scene);
            if (op == null)
            {
                Debug.LogError($"[PageTurn] Scene '{scene}' is not in the build settings.");
                canvas.enabled = false; Arriving = false; busy = false; Destroy(tex);
                yield break;
            }
            // the stage becomes a page of an old picture book while the next one loads behind it
            while (!op.isDone || Time.unscaledTime - ageStart < ageTime)
            {
                float k = ageTime > 0 ? Mathf.Clamp01((Time.unscaledTime - ageStart) / ageTime) : 0f;
                mat.SetFloat(IdAge, ageTime > 0 ? k * k * (3f - 2f * k) : 0f);
                yield return null;
            }
            mat.SetFloat(IdAge, ageTime > 0 ? 1f : 0f);
            for (int i = 0; i < settleFrames; i++) yield return null;
            float until = Time.unscaledTime + holdBefore;
            while (Time.unscaledTime < until) yield return null;

            // 3. turn
            if (turnClip) sfx.PlayOneShot(turnClip, turnVolume);
            for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / turnTime)
            {
                mat.SetFloat(IdProgress, k);
                yield return null;
            }
            mat.SetFloat(IdProgress, 1f);
            canvas.enabled = false;
            page.texture = null;
            Destroy(tex);
            Arriving = false;
            busy = false;
        }
    }
}

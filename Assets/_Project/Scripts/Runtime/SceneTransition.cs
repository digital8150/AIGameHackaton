using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DuskTwilight
{
    /// <summary>
    /// Shared black cover for every scene change: fade to black → load (async, activation held until covered)
    /// → hold a moment so the new scene's first-frame stutter happens behind the cover → fade back in.
    /// One instance survives scene loads; callers pass the prefab so it can be spawned on first use
    /// (e.g. when a stage is played directly from the editor).
    /// </summary>
    public class SceneTransition : MonoBehaviour
    {
        public static SceneTransition I { get; private set; }
        /// <summary>True from the start of the fade-out until the cover has lifted.</summary>
        public static bool Busy => I && I.busy;

        [Tooltip("Full-screen cover on a top-most canvas (black).")]
        public CanvasGroup cover;

        [Header("Timing (seconds, unscaled)")]
        [Range(0.05f, 3f)] public float fadeOut = 0.7f;
        [Tooltip("Wait after the new scene is active, before lifting the cover (hides load hitches).")]
        [Range(0f, 3f)] public float hold = 0.5f;
        [Tooltip("Frames to wait after activation in addition to the hold (first frames are the heaviest).")]
        [Range(0, 30)] public int settleFrames = 5;
        [Range(0.05f, 3f)] public float fadeIn = 0.9f;

        bool busy;

        /// <summary>The live instance, spawned from <paramref name="prefab"/> if there is none yet.</summary>
        public static SceneTransition Ensure(SceneTransition prefab)
        {
            if (I) return I;
            if (!prefab) return null;
            return Instantiate(prefab);
        }

        /// <summary>Change scene behind the cover. Falls back to a plain load without a prefab.</summary>
        public static void Load(string scene, SceneTransition prefab)
        {
            var t = Ensure(prefab);
            if (t) t.Go(scene);
            else SceneManager.LoadScene(scene);
        }

        void Awake()
        {
            if (I && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
            SetCover(0f);
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        public void Go(string scene)
        {
            if (busy) return;
            StartCoroutine(Run(scene));
        }

        IEnumerator Run(string scene)
        {
            busy = true;
            var op = SceneManager.LoadSceneAsync(scene);
            if (op == null)
            {
                Debug.LogError($"[SceneTransition] Scene '{scene}' is not in the build settings.");
                busy = false;
                yield break;
            }
            op.allowSceneActivation = false;

            yield return Fade(cover.alpha, 1f, fadeOut);

            while (op.progress < 0.9f) yield return null;
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            for (int i = 0; i < settleFrames; i++) yield return null;
            float until = Time.unscaledTime + hold;
            while (Time.unscaledTime < until) yield return null;

            yield return Fade(1f, 0f, fadeIn);
            busy = false;
        }

        IEnumerator Fade(float from, float to, float time)
        {
            for (float k = 0f; k < 1f; k += Time.unscaledDeltaTime / time)
            {
                SetCover(Mathf.Lerp(from, to, k * k * (3f - 2f * k)));
                yield return null;
            }
            SetCover(to);
        }

        void SetCover(float a)
        {
            if (!cover) return;
            cover.alpha = a;
            cover.blocksRaycasts = a > 0.01f;
        }
    }
}

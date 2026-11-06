using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// 읽기 쉬운 글꼴. The in-game UI is written in a handwritten font; with this setting every UI Text that uses it
    /// (captions, choice panels, the guide, menus, the storybooks) switches to a plain book font, and back when it is
    /// turned off. Made at start-up and kept across scenes; re-applied whenever a scene loads or the setting changes.
    /// Clones made from a switched template (choice options, guide rows) inherit the switched font.
    /// </summary>
    public class ReadableFontSwitcher : MonoBehaviour
    {
        static ReadableFontSwitcher instance;
        const string HandwrittenName = "온글잎 류류체";
        Font readable;
        [Tooltip("The plain font draws bigger than the handwritten one at the same size")]
        public float sizeRatio = 0.76f;
        readonly Dictionary<Text, (Font font, int size)> original = new Dictionary<Text, (Font, int)>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance) return;
            var go = new GameObject("Readable Font (읽기 쉬운 글꼴)");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ReadableFontSwitcher>();
        }

        void Awake()
        {
            readable = Resources.Load<Font>("Fonts/ReadableFont");
            SceneManager.sceneLoaded += OnScene;
            GameSettings.Changed += Apply;
            Apply();
        }

        void OnDestroy() { SceneManager.sceneLoaded -= OnScene; GameSettings.Changed -= Apply; }
        void OnScene(Scene s, LoadSceneMode m) => Apply();

        void Apply()
        {
            if (!readable) return;
            bool on = GameSettings.ReadableFont;
            foreach (var t in FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!t) continue;
                if (on && t.font && t.font != readable && t.font.name == HandwrittenName)
                {
                    original[t] = (t.font, t.fontSize);
                    t.font = readable;
                    t.fontSize = Mathf.Max(8, Mathf.RoundToInt(t.fontSize * sizeRatio));
                }
                else if (!on && original.TryGetValue(t, out var o)) { t.font = o.font; t.fontSize = o.size; }
            }
            if (!on) original.Clear();
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DuskTwilight
{
    /// <summary>
    /// 색약 모드. A global post-process volume (made at start-up, kept across scenes) with a channel mixer that applies a
    /// daltonisation for the chosen type (적색약 / 녹색약 / 청황색약): the colour difference that eye loses is shifted into
    /// channels it still tells apart. Follows <see cref="GameSettings.ColorAssistMode"/>.
    /// </summary>
    public class ColorAssistFilter : MonoBehaviour
    {
        static ColorAssistFilter instance;
        Volume volume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance) return;
            var go = new GameObject("Color Assist (색약 모드)");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ColorAssistFilter>();
        }

        void Awake()
        {
            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            mix = profile.Add<ChannelMixer>(true);
            volume.sharedProfile = profile;
            Apply();
        }

        void OnEnable() { GameSettings.Changed += Apply; }
        void OnDisable() { GameSettings.Changed -= Apply; }
        ChannelMixer mix;

        // daltonisation, I + shift·(I − simulation), rows = R', G', B' out of (R, G, B) in, in percent
        static readonly int[][,] Matrices =
        {
            null,
            new int[,] { { 100, 0, 0 }, { -25, 125, 0 }, { 30, -55, 124 } },   // 적색약 (protan)
            new int[,] { { 100, 0, 0 }, { -44, 144, 0 }, { 26, -56, 130 } },   // 녹색약 (deutan)
            new int[,] { { 100, -33, 33 }, { 0, 67, 33 }, { 0, 0, 100 } },     // 청황색약 (tritan): blue-yellow error moved into red/green
        };

        void Apply()
        {
            if (!volume || mix == null) return;
            int mode = GameSettings.ColorAssistMode;
            volume.weight = mode > 0 ? 1f : 0f;
            if (mode <= 0) return;
            var m = Matrices[mode];
            mix.redOutRedIn.Override(m[0, 0]); mix.redOutGreenIn.Override(m[0, 1]); mix.redOutBlueIn.Override(m[0, 2]);
            mix.greenOutRedIn.Override(m[1, 0]); mix.greenOutGreenIn.Override(m[1, 1]); mix.greenOutBlueIn.Override(m[1, 2]);
            mix.blueOutRedIn.Override(m[2, 0]); mix.blueOutGreenIn.Override(m[2, 1]); mix.blueOutBlueIn.Override(m[2, 2]);
        }
    }
}

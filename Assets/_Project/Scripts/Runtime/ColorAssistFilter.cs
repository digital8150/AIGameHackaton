using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DuskTwilight
{
    /// <summary>
    /// 색약 모드. A global post-process volume (made at start-up, kept across scenes) with a channel mixer that applies a
    /// daltonisation for red-green colour blindness (deuteranopia, the most common): the red-green difference a
    /// colour-blind eye loses is shifted into brightness and blue, so things that differed only by red/green differ
    /// visibly again. On/off follows <see cref="GameSettings.ColorAssist"/>.
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
            var mix = profile.Add<ChannelMixer>(true);
            // I + shift·(I - deuteranope simulation): R' = R; G' = -0.44R + 1.44G; B' = 0.26R - 0.56G + 1.3B
            mix.redOutRedIn.Override(100f); mix.redOutGreenIn.Override(0f); mix.redOutBlueIn.Override(0f);
            mix.greenOutRedIn.Override(-44f); mix.greenOutGreenIn.Override(144f); mix.greenOutBlueIn.Override(0f);
            mix.blueOutRedIn.Override(26f); mix.blueOutGreenIn.Override(-56f); mix.blueOutBlueIn.Override(130f);
            volume.sharedProfile = profile;
            Apply();
        }

        void OnEnable() { GameSettings.Changed += Apply; }
        void OnDisable() { GameSettings.Changed -= Apply; }
        void Apply() { if (volume) volume.weight = GameSettings.ColorAssist ? 1f : 0f; }
    }
}

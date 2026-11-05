using System;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// Player settings persisted in PlayerPrefs: volumes (0..1), display mode/resolution, microphone device.
    /// AudioRouter listens to <see cref="Changed"/>; VoiceInput reads <see cref="MicDevice"/>.
    /// </summary>
    public static class GameSettings
    {
        const string KMaster = "settings.master", KMusic = "settings.music", KSfx = "settings.sfx";
        const string KFull = "settings.fullscreen", KW = "settings.width", KH = "settings.height", KMic = "settings.mic";

        public static event Action Changed;

        public static float MasterVolume { get => PlayerPrefs.GetFloat(KMaster, 0.9f); set => SetF(KMaster, value); }
        public static float MusicVolume { get => PlayerPrefs.GetFloat(KMusic, 0.7f); set => SetF(KMusic, value); }
        public static float SfxVolume { get => PlayerPrefs.GetFloat(KSfx, 0.85f); set => SetF(KSfx, value); }

        /// <summary>Empty = first available device.</summary>
        public static string MicDevice
        {
            get => PlayerPrefs.GetString(KMic, "");
            set { PlayerPrefs.SetString(KMic, value ?? ""); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        /// <summary>The device VoiceInput should open: the saved one if it is still plugged in.</summary>
        public static string ResolveMicDevice()
        {
            var devices = Microphone.devices;
            if (devices.Length == 0) return null;
            string want = MicDevice;
            foreach (var d in devices) if (d == want) return d;
            return devices[0];
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(KFull, Screen.fullScreen ? 1 : 0) == 1;
            set { PlayerPrefs.SetInt(KFull, value ? 1 : 0); PlayerPrefs.Save(); ApplyDisplay(); }
        }

        public static Vector2Int Resolution
        {
            get => new Vector2Int(PlayerPrefs.GetInt(KW, Screen.currentResolution.width), PlayerPrefs.GetInt(KH, Screen.currentResolution.height));
            set { PlayerPrefs.SetInt(KW, value.x); PlayerPrefs.SetInt(KH, value.y); PlayerPrefs.Save(); ApplyDisplay(); }
        }

        public static void ApplyDisplay()
        {
#if !UNITY_EDITOR
            var r = Resolution;
            Screen.SetResolution(r.x, r.y, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
            Changed?.Invoke();
        }

        /// <summary>0..1 slider value → mixer decibels (perceptual, -80 dB at 0).</summary>
        public static float ToDecibels(float v) => v <= 0.0001f ? -80f : Mathf.Log10(v) * 20f;

        static void SetF(string key, float v)
        {
            PlayerPrefs.SetFloat(key, Mathf.Clamp01(v));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}

using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using Whisper;

namespace DuskTwilight
{
    /// <summary>
    /// Push-to-talk microphone (hold V / LB). While held: live loudness (the prince's voice echo).
    /// On release: the clip is transcribed locally by Whisper (whisper.cpp, ggml-base, Korean) — no network
    /// (기획서: "Whisper는 로컬에서 돌린다 … 행사장 네트워크에 의존하지 않는다").
    /// Lives across chapters so the model loads once.
    /// </summary>
    [RequireComponent(typeof(WhisperManager))]
    public class VoiceInput : MonoBehaviour
    {
        public static VoiceInput I { get; private set; }

        public int sampleRate = 16000;
        public float maxSeconds = 6f;
        public float minSeconds = 0.3f;

        public float Level { get; private set; }
        public bool Recording { get; private set; }
        public bool Transcribing { get; private set; }
        public string LastTranscript { get; private set; } = "";
        public float LastLatency { get; private set; }
        public bool Available => Microphone.devices.Length > 0;

        public event Action<string> Transcribed;

        WhisperManager whisper;
        AudioClip clip;
        string device;
        float started;
        readonly float[] window = new float[512];

        void Awake()
        {
            if (I && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
            whisper = GetComponent<WhisperManager>();
        }

        void Update()
        {
            var input = GameInput.I;
            bool hold = input != null && input.MicHold.IsPressed() && !GameFlow.ModalOpen;
            if (hold && !Recording && !Transcribing && Available) Begin();
            if (!Recording) return;
            MeasureLevel();
            if (!hold || Time.time - started > maxSeconds) End();
        }

        void Begin()
        {
            device = GameSettings.ResolveMicDevice();
            clip = Microphone.Start(device, false, Mathf.CeilToInt(maxSeconds) + 1, sampleRate);
            started = Time.time;
            Recording = true;
        }

        void MeasureLevel()
        {
            int pos = Microphone.GetPosition(device) - window.Length;
            if (pos < 0) return;
            clip.GetData(window, pos);
            float sum = 0;
            for (int i = 0; i < window.Length; i++) sum += window[i] * window[i];
            Level = Mathf.Sqrt(sum / window.Length);
        }

        void End()
        {
            int samples = Microphone.GetPosition(device);
            Microphone.End(device);
            Recording = false;
            Level = 0;
            if (samples < minSeconds * sampleRate) return;
            var data = new float[samples * clip.channels];
            clip.GetData(data, 0);
            Transcribe(data, clip.channels);
        }

        async void Transcribe(float[] data, int channels)
        {
            Transcribing = true;
            float t0 = Time.realtimeSinceStartup;
            string text = "";
            try
            {
                if (!whisper.IsLoaded && !whisper.IsLoading) await whisper.InitModel();
                while (whisper.IsLoading) await Task.Yield();
                var res = await whisper.GetTextAsync(data, sampleRate, channels);
                text = Clean(res?.Result);
            }
            catch (Exception e) { Debug.LogWarning("[Voice] Whisper failed: " + e.Message); }
            LastLatency = Time.realtimeSinceStartup - t0;
            LastTranscript = text;
            Transcribing = false;
            if (!string.IsNullOrEmpty(text)) Transcribed?.Invoke(text);
        }

        /// <summary>Strips Whisper's non-speech tags ([BLANK_AUDIO], (음악)) and punctuation.</summary>
        public static string Clean(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            s = Regex.Replace(s, @"\[[^\]]*\]|\([^\)]*\)|\*[^\*]*\*", "");
            s = Regex.Replace(s, @"[\.,!?…~""'“”‘’·]", " ");
            return Regex.Replace(s, @"\s+", " ").Trim();
        }

        void OnDisable()
        {
            if (Recording) { Microphone.End(device); Recording = false; }
        }
    }
}

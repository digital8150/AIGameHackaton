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

        [Header("Accuracy")]
        [Tooltip("Tell Whisper which words to expect: the words of every word puzzle in the scene (nearest first) + commands. " +
                 "Short Korean words are recognised far more reliably with this.")]
        public bool biasToSceneWords = true;
        [Tooltip("Always in the prompt (voice commands)")]
        public string commandWords = "바꿔, 점프, 힌트, 열어";
        [Tooltip("Cut silence before and after the voice (RMS below this)")]
        [Range(0f, 0.1f)] public float silenceLevel = 0.012f;
        [Tooltip("Silence kept around the voice after trimming (s)")]
        public float keepPadding = 0.25f;
        [Tooltip("Whisper misses very short clips: pad the voice with silence up to this length (s)")]
        public float minClipSeconds = 1.2f;
        [Tooltip("Raise quiet recordings to this peak (0 = off)")]
        [Range(0f, 1f)] public float normalizePeak = 0.8f;

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
            // the bigger model is fetched by Tools/download_assets.bat; a fresh clone without it falls back to base
            if (whisper && !System.IO.File.Exists(System.IO.Path.Combine(Application.streamingAssetsPath, whisper.ModelPath)))
            {
                var fallback = "Whisper/ggml-base.bin";
                if (System.IO.File.Exists(System.IO.Path.Combine(Application.streamingAssetsPath, fallback)))
                {
                    Debug.LogWarning($"[Voice] {whisper.ModelPath} missing (run Tools/download_assets.bat) — using {fallback}");
                    whisper.ModelPath = fallback;
                }
            }
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
                if (biasToSceneWords) whisper.initialPrompt = BuildPrompt();
                var mono = Prepare(data, channels);
                var res = await whisper.GetTextAsync(mono, sampleRate, 1);
                text = Clean(res?.Result);
            }
            catch (Exception e) { Debug.LogWarning("[Voice] Whisper failed: " + e.Message); }
            LastLatency = Time.realtimeSinceStartup - t0;
            LastTranscript = text;
            Transcribing = false;
            if (!string.IsNullOrEmpty(text)) Transcribed?.Invoke(text);
        }

        /// <summary>
        /// Prompt that primes Whisper with the words the player is likely to say: the nearest word puzzle's
        /// choices first, then every other puzzle in the scene, then the voice commands.
        /// </summary>
        string BuildPrompt()
        {
            var words = new System.Collections.Generic.List<string>();
            void Add(string[] ws) { if (ws != null) foreach (var w in ws) if (!string.IsNullOrEmpty(w) && !words.Contains(w)) words.Add(w); }
            void AddTarget(WordTarget t)
            {
                if (!t) return;
                Add(new System.Collections.Generic.List<string>(t.ListenWords()).ToArray());
            }
            AddTarget(WordTarget.Nearest);
            foreach (var t in FindObjectsByType<WordTarget>(FindObjectsSortMode.None)) AddTarget(t);
            Add(commandWords.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries));
            return string.Join(", ", words) + ".";
        }

        /// <summary>Mono, silence trimmed, quiet voices raised, padded so Whisper gets at least minClipSeconds.</summary>
        float[] Prepare(float[] data, int channels)
        {
            int n = data.Length / Mathf.Max(1, channels);
            var mono = new float[n];
            for (int i = 0; i < n; i++)
            {
                float sum = 0;
                for (int c = 0; c < channels; c++) sum += data[i * channels + c];
                mono[i] = sum / channels;
            }
            // trim silence (20 ms windows)
            int win = Mathf.Max(1, sampleRate / 50), first = -1, last = -1;
            for (int w = 0; w + win <= n; w += win)
            {
                float e = 0;
                for (int i = w; i < w + win; i++) e += mono[i] * mono[i];
                if (Mathf.Sqrt(e / win) > silenceLevel) { if (first < 0) first = w; last = w + win; }
            }
            if (first < 0) { first = 0; last = n; }   // nothing loud: let Whisper decide
            int pad = Mathf.RoundToInt(keepPadding * sampleRate);
            first = Mathf.Max(0, first - pad); last = Mathf.Min(n, last + pad);
            int len = last - first;
            // normalise
            float peak = 0;
            for (int i = first; i < last; i++) peak = Mathf.Max(peak, Mathf.Abs(mono[i]));
            float gain = normalizePeak > 0 && peak > 1e-4f ? Mathf.Min(8f, normalizePeak / peak) : 1f;
            // centre the voice in at least minClipSeconds of audio
            int total = Mathf.Max(len, Mathf.RoundToInt(minClipSeconds * sampleRate));
            var outBuf = new float[total];
            int off = (total - len) / 2;
            for (int i = 0; i < len; i++) outBuf[off + i] = Mathf.Clamp(mono[first + i] * gain, -1f, 1f);
            return outBuf;
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

using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// Peak limiter on the AudioListener (the whole mix, after the mixer): when many sounds stack up the output is
    /// pulled down to the ceiling instead of getting louder / clipping. Instant attack, smooth release.
    /// AudioRouter adds it to the active listener and feeds its settings; it can also be placed by hand.
    /// </summary>
    [RequireComponent(typeof(AudioListener))]
    public class MasterLimiter : MonoBehaviour
    {
        [Tooltip("Output ceiling (dBFS).")]
        [Range(-12f, 0f)] public float ceilingDb = -1.5f;
        [Tooltip("Time for the gain to recover after a peak (ms).")]
        [Range(10f, 1000f)] public float releaseMs = 150f;

        /// <summary>Current gain reduction in dB (0 = untouched). For debugging.</summary>
        public float ReductionDb => -20f * Mathf.Log10(Mathf.Max(1e-5f, gain));
        public float PeakReductionDb { get; private set; }

        volatile float ceiling = 0.84f, release = 0.9997f;
        float gain = 1f;
        int sampleRate;

        void OnEnable()
        {
            sampleRate = AudioSettings.outputSampleRate;
            Apply();
        }

        void OnValidate() => Apply();

        public void Apply()
        {
            ceiling = Mathf.Pow(10f, ceilingDb / 20f);
            int sr = sampleRate > 0 ? sampleRate : 48000;
            release = Mathf.Exp(-1f / (Mathf.Max(1f, releaseMs) * 0.001f * sr));
        }

        public void ResetPeak() => PeakReductionDb = 0f;

        void OnAudioFilterRead(float[] data, int channels)
        {
            float c = ceiling, r = release, g = gain, minG = 1f;
            for (int i = 0; i < data.Length; i += channels)
            {
                float peak = 0f;
                for (int ch = 0; ch < channels; ch++)
                {
                    float a = Mathf.Abs(data[i + ch]);
                    if (a > peak) peak = a;
                }
                float target = peak * g > c ? c / peak : 1f;
                g = target < g ? target : 1f - (1f - g) * r;   // attack now, release smoothly
                if (peak * g > c) g = c / peak;
                for (int ch = 0; ch < channels; ch++) data[i + ch] *= g;
                if (g < minG) minG = g;
            }
            gain = g;
            float red = -20f * Mathf.Log10(Mathf.Max(1e-5f, minG));
            if (red > PeakReductionDb) PeakReductionDb = red;
        }
    }
}

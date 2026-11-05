using UnityEngine;

namespace DuskTwilight
{
    /// <summary>Procedural low wind (filtered brown noise with slow gusts). Part of the prince's rich soundscape.</summary>
    [RequireComponent(typeof(AudioSource))]
    public class AmbientWind : MonoBehaviour
    {
        public float volume = 0.16f;
        float brown, gust, gustTarget, gustTimer;
        int sampleRate;
        System.Random rng = new System.Random(7);

        void Start()
        {
            sampleRate = AudioSettings.outputSampleRate;
            var src = GetComponent<AudioSource>();
            src.playOnAwake = true;
            src.loop = true;
            src.spatialBlend = 0;
            src.volume = 1;
            src.Play();
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (sampleRate == 0) return;
            for (int i = 0; i < data.Length; i += channels)
            {
                float white = (float)(rng.NextDouble() * 2 - 1);
                brown = (brown + 0.02f * white) / 1.02f;
                if (--gustTimer <= 0) { gustTimer = sampleRate * (1.5f + (float)rng.NextDouble() * 3f); gustTarget = 0.4f + (float)rng.NextDouble() * 0.9f; }
                gust += (gustTarget - gust) * 0.00002f;
                float s = brown * 3.5f * gust * volume;
                for (int c = 0; c < channels; c++) data[i + c] = s;
            }
        }
    }
}

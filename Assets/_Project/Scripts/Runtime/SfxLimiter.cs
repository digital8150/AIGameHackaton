using System.Collections.Generic;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// One-shot SFX with overlap control: the same clip fired in quick succession (hovering across buttons, many steps,
    /// chained bells) is skipped inside a minimum gap, attenuated per overlapping copy and capped in count.
    /// Settings live on <see cref="AudioRouter"/> (Overlap header); the peak ceiling is <see cref="MasterLimiter"/>.
    /// </summary>
    public static class SfxLimiter
    {
        static readonly Dictionary<AudioClip, List<float>> recent = new Dictionary<AudioClip, List<float>>();

        /// <summary>PlayOneShot through the overlap rules. Returns false when the play was dropped.</summary>
        public static bool PlayLimited(this AudioSource src, AudioClip clip, float volume = 1f)
        {
            if (!src || !clip) return false;
            var cfg = AudioRouter.I;
            float minGap = cfg ? cfg.sameClipMinGap : 0.05f;
            int maxStack = cfg ? cfg.sameClipMaxStack : 3;
            float atten = cfg ? cfg.stackAttenuation : 0.7f;
            float window = Mathf.Min(clip.length, cfg ? cfg.stackWindow : 0.4f);

            float now = Time.unscaledTime;   // UI plays while the game is paused
            if (!recent.TryGetValue(clip, out var times)) recent[clip] = times = new List<float>(4);
            times.RemoveAll(t => now - t > window || t > now);   // t > now: time restarted (play mode)

            if (times.Count > 0 && now - times[times.Count - 1] < minGap) return false;
            if (times.Count >= maxStack) return false;

            src.PlayOneShot(clip, volume * Mathf.Pow(atten, times.Count));
            times.Add(now);
            return true;
        }
    }
}

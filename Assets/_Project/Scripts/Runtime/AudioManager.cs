using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace DuskTwilight
{
    public enum MusicTransition
    {
        /// <summary>Equal-power crossfade between the two decks.</summary>
        Crossfade = 0,
        /// <summary>The old track dissolves into reverb/echo (dry drops, wet swells, highs close) while the new one rises.</summary>
        ReverbTail = 1,
        /// <summary>Hard cut (no fade).</summary>
        Cut = 2,
    }

    /// <summary>
    /// Background music. One persistent instance (DontDestroyOnLoad) with two decks (child AudioSources carrying
    /// low-pass / echo / reverb filters). Every stage scene holds an instance of the AudioManager prefab: the first one
    /// becomes the player, later ones hand their scene track to it and destroy themselves, so the same track keeps
    /// playing across stage loads and retries. A scene without an AudioManager fades the music out.
    ///
    /// While perceiving through the deaf Dusk Princess (Light sense) the music is muffled: low-passed, quieter,
    /// slightly detuned, washed in reverb — sound felt rather than heard. The blind prince hears it clearly.
    /// Follows the sense wipe.
    /// The princess's silent world mutes only the SFX group (SenseController.WorldVolume → AudioRouter); music keeps playing.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        [Header("This scene's music")]
        [Tooltip("Track for the scene this instance sits in. Empty = fade the music out.")]
        public AudioClip sceneTrack;
        [Tooltip("How the music moves to this scene's track.")]
        public MusicTransition transition = MusicTransition.Crossfade;
        [Tooltip("Seconds the transition into this scene's track takes.")]
        [Range(0f, 10f)] public float transitionTime = 3f;
        [Tooltip("Volume of this scene's track (before the Music mixer group).")]
        [Range(0f, 1f)] public float trackVolume = 0.8f;
        [Tooltip("Restart the track if it is already playing when this scene loads.")]
        public bool restartIfSame;

        [Header("Decks")]
        [Tooltip("Two child AudioSources with AudioLowPassFilter, AudioEchoFilter and AudioReverbFilter.")]
        public AudioSource deckA;
        public AudioSource deckB;
        public AudioMixerGroup musicGroup;
        [Tooltip("When a scene loads without an AudioManager, fade the music out over this many seconds (0 = keep playing).")]
        [Range(0f, 10f)] public float fadeOutWhenNoManager = 3f;
        [Tooltip("How the music ends when a scene has no music.")]
        public MusicTransition stopTransition = MusicTransition.ReverbTail;

        [Header("Reverb tail (ReverbTail transition)")]
        [Tooltip("Low-pass the outgoing track closes down to (Hz).")]
        [Range(200f, 22000f)] public float tailCutoff = 900f;
        [Tooltip("Reverb send the outgoing track swells to (0-1).")]
        [Range(0f, 1f)] public float tailReverb = 1f;
        [Tooltip("Echo wet mix the outgoing track swells to (0-1).")]
        [Range(0f, 1f)] public float tailEcho = 0.6f;
        [Tooltip("How much of the dry signal is removed at the end of the tail (0-1).")]
        [Range(0f, 1f)] public float tailDryCut = 0.9f;
        [Tooltip("Share of the transition the old track keeps sounding as a wash (the rest is its fade-out).")]
        [Range(0.1f, 1f)] public float tailHold = 0.65f;

        [Header("Deaf princess (Light sense) — muffled mix")]
        [Tooltip("Low-pass cutoff while in the princess's senses (Hz) — music as if through cotton / water.")]
        [Range(200f, 22000f)] public float muffledCutoff = 320f;
        [Tooltip("Low-pass resonance (Q).")]
        [Range(1f, 4f)] public float muffledResonance = 1.3f;
        [Tooltip("Reverb send (0-1) — a distant, felt-not-heard room.")]
        [Range(0f, 1f)] public float muffledReverb = 0.5f;
        [Tooltip("Echo wet mix (0-1).")]
        [Range(0f, 1f)] public float muffledEcho = 0.12f;
        [Tooltip("Pitch (also slows the track slightly — a heavier, submerged feel).")]
        [Range(0.85f, 1.05f)] public float muffledPitch = 0.98f;
        [Tooltip("Volume multiplier.")]
        [Range(0f, 1.5f)] public float muffledVolume = 0.75f;
        [Tooltip("Seconds to ease fully into / out of the muffled mix.")]
        [Range(0.05f, 3f)] public float muffleEase = 0.8f;
        [Tooltip("Follow the sense wipe at the listener (otherwise only the current sense).")]
        public bool followWipe = true;

        [Header("Filter settings")]
        [Tooltip("Echo delay (ms). Fixed at start — changing it live clicks.")]
        [Range(10f, 1500f)] public float echoDelay = 420f;
        [Range(0f, 1f)] public float echoDecay = 0.45f;
        [Tooltip("Reverb decay time (s).")]
        [Range(0.1f, 20f)] public float reverbDecay = 3.4f;

        /// <summary>0 = clear (prince hears), 1 = fully muffled (princess is deaf).</summary>
        public float Muffle { get; private set; }
        public AudioClip Current => decks[active].src ? decks[active].src.clip : null;

        class Deck
        {
            public AudioSource src;
            public AudioLowPassFilter lp;
            public AudioEchoFilter echo;
            public AudioReverbFilter reverb;
            public float gain;          // crossfade gain 0-1
            public float tail;          // reverb-tail amount 0-1
            public float volume = 1f;   // track volume
        }

        readonly Deck[] decks = { new Deck(), new Deck() };
        int active;
        Coroutine fade;
        bool claimedThisLoad;

        void Awake()
        {
            if (I && I != this)
            {
                I.Adopt(this);
                Destroy(gameObject);
                return;
            }
            I = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            Setup(decks[0], deckA);
            Setup(decks[1], deckB);
            SceneManager.sceneLoaded += OnSceneLoaded;
            Adopt(this);
        }

        void OnDestroy()
        {
            if (I != this) return;
            I = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void Setup(Deck d, AudioSource src)
        {
            d.src = src;
            if (!src) return;
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;
            src.ignoreListenerVolume = true; // the princess's silence mutes the world, not the score
            src.ignoreListenerPause = true;
            if (musicGroup) src.outputAudioMixerGroup = musicGroup;
            src.volume = 0f;
            d.lp = src.GetComponent<AudioLowPassFilter>();
            d.echo = src.GetComponent<AudioEchoFilter>();
            d.reverb = src.GetComponent<AudioReverbFilter>();
            if (d.echo) { d.echo.delay = echoDelay; d.echo.decayRatio = echoDecay; d.echo.wetMix = 0f; d.echo.dryMix = 1f; }
            if (d.reverb)
            {
                d.reverb.reverbPreset = AudioReverbPreset.User;
                d.reverb.decayTime = reverbDecay;
                d.reverb.dryLevel = 0f;
                d.reverb.room = -10000f;
                d.reverb.reverbLevel = 0f;
            }
        }

        /// <summary>A scene's AudioManager arrived: take over its track.</summary>
        void Adopt(AudioManager scene)
        {
            claimedThisLoad = true;
            Play(scene.sceneTrack, scene.transition, scene.transitionTime, scene.trackVolume, scene.restartIfSame);
        }

        void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive) return;
            if (!claimedThisLoad && fadeOutWhenNoManager > 0f) Stop(fadeOutWhenNoManager);
            claimedThisLoad = false;
        }

        // ---------- public API ----------

        public static void PlayMusic(AudioClip clip, MusicTransition how = MusicTransition.Crossfade, float seconds = 3f, float volume = 0.8f)
        {
            if (I) I.Play(clip, how, seconds, volume, false);
        }

        public static void StopMusic(float seconds = 2.5f, MusicTransition how = MusicTransition.ReverbTail)
        {
            if (I) I.Play(null, how, seconds, 0f, false);
        }

        public void Stop(float seconds) => Play(null, stopTransition, seconds, 0f, false);

        public void Play(AudioClip clip, MusicTransition how, float seconds, float volume, bool restart)
        {
            var cur = decks[active];
            if (clip && cur.src && cur.src.clip == clip && !restart) // clip stays set until the deck is stopped
            {
                cur.volume = volume; // same track keeps going
                if (fade != null) { StopCoroutine(fade); fade = StartCoroutine(Transition(decks[1 - active], cur, how, seconds)); }
                return;
            }
            if (!clip && (!cur.src || !cur.src.clip)) return;

            if (fade != null) StopCoroutine(fade);
            var outgoing = cur;
            Deck incoming = null;
            if (clip)
            {
                active = 1 - active;
                incoming = decks[active];
                if (!incoming.src) return;
                incoming.src.Stop();
                incoming.src.clip = clip;
                incoming.volume = volume;
                incoming.tail = 0f;
                incoming.gain = 0f;
                incoming.src.Play();
            }
            fade = StartCoroutine(Transition(outgoing, incoming, how, seconds));
        }

        System.Collections.IEnumerator Transition(Deck o, Deck n, MusicTransition how, float seconds)
        {
            float o0 = o.gain, t0 = o.tail, n0 = n != null ? n.gain : 0f; // resume from where an interrupted fade left off
            if (how == MusicTransition.Cut || seconds <= 0f)
            {
                o.gain = 0f; o.tail = 0f;
                if (n != null) n.gain = 1f;
            }
            else
            {
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / seconds)
                {
                    if (how == MusicTransition.ReverbTail)
                    {
                        // the old track washes out first, then fades; the new one rises under the wash
                        o.tail = Mathf.Max(t0, Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.05f, tailHold * 0.6f)));
                        float fadeT = Mathf.InverseLerp(tailHold * 0.4f, 1f, t);
                        o.gain = o0 * Mathf.Cos(fadeT * Mathf.PI * 0.5f);
                        if (n != null) n.gain = Mathf.Max(n0, Mathf.Sin(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 1f, t)) * Mathf.PI * 0.5f));
                    }
                    else
                    {
                        o.gain = o0 * Mathf.Cos(t * Mathf.PI * 0.5f);
                        if (n != null) n.gain = Mathf.Max(n0, Mathf.Sin(t * Mathf.PI * 0.5f));
                    }
                    yield return null;
                }
                o.gain = 0f;
                if (n != null) n.gain = 1f;
            }
            if (o.src) { o.src.Stop(); o.src.clip = null; }
            o.tail = 0f;
            fade = null;
        }

        // ---------- per-frame mix ----------

        void Update()
        {
            // muffled = the princess's (Light) share at the listener; the prince hears the score clearly
            float target = 0f;
            var sc = SenseController.I;
            if (sc)
                target = 1f - (followWipe && sc.Listener ? sc.SenseAt(sc.Listener.position) : (sc.IsDark ? 1f : 0f));
            Muffle = Mathf.MoveTowards(Muffle, Mathf.Clamp01(target), Time.unscaledDeltaTime / muffleEase);
            float b = Mathf.SmoothStep(0f, 1f, Muffle);

            foreach (var d in decks) Apply(d, b);
        }

        void Apply(Deck d, float b)
        {
            if (!d.src) return;
            float t = d.tail;
            d.src.volume = d.volume * d.gain * Mathf.Lerp(1f, muffledVolume, b);
            d.src.pitch = Mathf.Lerp(1f, muffledPitch, b);

            if (d.lp)
            {
                float c = Mathf.Min(LogLerp(22000f, muffledCutoff, b), LogLerp(22000f, tailCutoff, t));
                d.lp.cutoffFrequency = c;
                d.lp.lowpassResonanceQ = Mathf.Lerp(1f, muffledResonance, b);
            }
            float dry = 1f - tailDryCut * t;
            if (d.echo)
            {
                d.echo.wetMix = Mathf.Max(muffledEcho * b, tailEcho * t);
                d.echo.dryMix = dry;
            }
            if (d.reverb)
            {
                d.reverb.room = ToMillibel(Mathf.Max(muffledReverb * b, tailReverb * t));
                d.reverb.dryLevel = ToMillibel(dry);
            }
        }

        static float LogLerp(float a, float b, float t) => Mathf.Exp(Mathf.Lerp(Mathf.Log(a), Mathf.Log(b), Mathf.Clamp01(t)));

        static float ToMillibel(float amp) => amp <= 0.0001f ? -10000f : Mathf.Max(-10000f, 2000f * Mathf.Log10(amp));
    }
}

using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace DuskTwilight
{
    /// <summary>
    /// Applies GameSettings volumes to the mixer and routes every AudioSource that has no output group to SFX
    /// (gameplay scripts create their sources at runtime, so this sweeps periodically).
    /// Music sources are assigned to the Music group in the inspector.
    /// </summary>
    public class AudioRouter : MonoBehaviour
    {
        [Tooltip("Mixer with exposed volume parameters.")]
        public AudioMixer mixer;
        public AudioMixerGroup sfxGroup;
        [Header("Exposed parameter names")]
        public string masterParam = "MasterVolume";
        public string musicParam = "MusicVolume";
        public string sfxParam = "SfxVolume";
        [Tooltip("Seconds between sweeps for new unrouted AudioSources.")]
        [Range(0.1f, 5f)] public float sweepInterval = 0.5f;

        [Header("Overlap (SfxLimiter.PlayLimited)")]
        [Tooltip("The same clip fired again within this many seconds is dropped (fast hover across buttons).")]
        [Range(0f, 0.3f)] public float sameClipMinGap = 0.06f;
        [Tooltip("At most this many copies of one clip ring at once (within Stack Window).")]
        [Range(1, 8)] public int sameClipMaxStack = 3;
        [Tooltip("Volume multiplier per copy already ringing (0.7 ≈ -3 dB each).")]
        [Range(0.2f, 1f)] public float stackAttenuation = 0.7f;
        [Tooltip("How long a copy counts as ringing (capped by the clip length).")]
        [Range(0.05f, 2f)] public float stackWindow = 0.4f;

        [Header("Master limiter (added to the active AudioListener)")]
        public bool masterLimiter = true;
        [Range(-12f, 0f)] public float limiterCeilingDb = -1.5f;
        [Range(10f, 1000f)] public float limiterReleaseMs = 150f;

        public static AudioRouter I { get; private set; }

        float nextSweep;
        float appliedWorld = 1f;

        void OnEnable()
        {
            I = this;
            GameSettings.Changed += Apply;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            GameSettings.Changed -= Apply;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (I == this) I = null;
        }

        void Start()
        {
            Apply();
            Sweep();
        }

        void OnSceneLoaded(Scene s, LoadSceneMode m) => Sweep();

        void Update()
        {
            // the princess's silent world: SFX follow the sense at the listener (music keeps playing)
            float world = SenseController.I ? SenseController.I.WorldVolume : 1f;
            if (mixer && !Mathf.Approximately(world, appliedWorld))
            {
                appliedWorld = world;
                mixer.SetFloat(sfxParam, SfxDecibels());
            }
            if (Time.unscaledTime < nextSweep) return;
            nextSweep = Time.unscaledTime + sweepInterval;
            Sweep();
        }

        public void Apply()
        {
            if (!mixer) return;
            mixer.SetFloat(masterParam, GameSettings.ToDecibels(GameSettings.MasterVolume));
            mixer.SetFloat(musicParam, GameSettings.ToDecibels(GameSettings.MusicVolume));
            mixer.SetFloat(sfxParam, SfxDecibels());
        }

        float SfxDecibels() => Mathf.Max(-80f, GameSettings.ToDecibels(GameSettings.SfxVolume) + GameSettings.ToDecibels(appliedWorld));

        void Sweep()
        {
            EnsureLimiter();
            if (!sfxGroup) return;
            foreach (var src in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!src.outputAudioMixerGroup) src.outputAudioMixerGroup = sfxGroup;
        }

        void EnsureLimiter()
        {
            if (!masterLimiter) return;
            var listener = FindAnyObjectByType<AudioListener>();
            if (!listener) return;
            var lim = listener.GetComponent<MasterLimiter>() ?? listener.gameObject.AddComponent<MasterLimiter>();
            if (!Mathf.Approximately(lim.ceilingDb, limiterCeilingDb) || !Mathf.Approximately(lim.releaseMs, limiterReleaseMs))
            {
                lim.ceilingDb = limiterCeilingDb;
                lim.releaseMs = limiterReleaseMs;
                lim.Apply();
            }
        }
    }
}

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

        float nextSweep;
        float appliedWorld = 1f;

        void OnEnable()
        {
            GameSettings.Changed += Apply;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            GameSettings.Changed -= Apply;
            SceneManager.sceneLoaded -= OnSceneLoaded;
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
            if (!sfxGroup) return;
            foreach (var src in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!src.outputAudioMixerGroup) src.outputAudioMixerGroup = sfxGroup;
        }
    }
}

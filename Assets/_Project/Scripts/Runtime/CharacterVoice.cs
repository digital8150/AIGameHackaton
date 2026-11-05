using UnityEngine;
using UnityEngine.Audio;

namespace DuskTwilight
{
    /// <summary>
    /// Short vocal reactions of one of the pair (jump, hard landing, fall, respawn, taking the lead, the princess's
    /// silent call). Only the leader speaks. Voices bypass the SFX group (which the princess's silent world mutes) and
    /// instead follow the music's muffle: in her senses her own voice is felt, not heard — low-passed like the music.
    /// </summary>
    [RequireComponent(typeof(Actor))]
    public class CharacterVoice : MonoBehaviour
    {
        [System.Serializable]
        public class Line
        {
            public AudioClip[] clips;
            [Range(0f, 1.5f)] public float volume = 0.8f;
            [Tooltip("Chance to speak when the event happens (jumps are frequent).")]
            [Range(0f, 1f)] public float chance = 1f;
            [Tooltip("Minimum seconds between two plays of this line.")]
            [Min(0f)] public float cooldown = 0f;
            [System.NonSerialized] public float last = -99f;
        }

        [Header("Lines")]
        public Line jump = new Line { volume = 0.6f, chance = 0.45f, cooldown = 1.2f };
        [Tooltip("Plays when landing faster than Hard Land Speed.")]
        public Line hardLand = new Line { volume = 0.6f, cooldown = 0.8f };
        [Tooltip("Fell below the stage or touched a hazard.")]
        public Line death = new Line { volume = 0.85f };
        [Tooltip("Back at the checkpoint after a fall.")]
        public Line respawn = new Line { volume = 0.7f };
        [Tooltip("This character just took the lead (sense switch).")]
        public Line switchIn = new Line { volume = 0.6f, cooldown = 0.6f };
        [Tooltip("Echo button while this character leads in the light — she tries to call but cannot hear it.")]
        public Line silentCall = new Line { volume = 0.7f, cooldown = 0.8f };

        [Header("Feel")]
        [Tooltip("Downward speed (m/s) at touchdown that counts as a hard landing.")]
        [Min(0f)] public float hardLandSpeed = 13f;
        [Tooltip("Random pitch spread (±).")]
        [Range(0f, 0.2f)] public float pitchJitter = 0.04f;
        [Tooltip("Respawn line waits this long after the screen starts to clear.")]
        [Min(0f)] public float respawnDelay = 0.25f;

        [Header("Output")]
        [Tooltip("Mixer group for voices. Empty = the AudioRouter mixer's Master (not muted by the silent world; scaled by the SFX slider here).")]
        public AudioMixerGroup output;
        [Tooltip("Low-pass cutoff (Hz) when fully muffled (princess's senses).")]
        [Range(200f, 22000f)] public float muffledCutoff = 900f;

        Actor actor;
        AudioSource src;
        AudioLowPassFilter lp;
        bool airborne;
        float minVy, quietUntil;

        void Awake()
        {
            actor = GetComponent<Actor>();
            var go = new GameObject("Voice Source");
            go.transform.SetParent(transform, false);
            src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            lp = go.AddComponent<AudioLowPassFilter>();
            lp.cutoffFrequency = 22000f;
        }

        void Start()
        {
            if (!output)
            {
                var router = FindAnyObjectByType<AudioRouter>();
                if (router && router.mixer)
                {
                    var g = router.mixer.FindMatchingGroups("Master");
                    if (g.Length > 0) output = g[0];
                }
            }
            if (output) src.outputAudioMixerGroup = output;   // set before AudioRouter's sweep would route it to SFX

            // GameFlow.Awake resets GameEvents, so subscribe in Start (like HUD)
            GameEvents.Jumped += OnJumped;
            GameEvents.Died += OnDied;
            GameEvents.Respawned += OnRespawned;
            GameEvents.Switched += OnSwitched;
            GameEvents.SilentCall += OnSilentCall;
        }

        void OnDestroy()
        {
            GameEvents.Jumped -= OnJumped;
            GameEvents.Died -= OnDied;
            GameEvents.Respawned -= OnRespawned;
            GameEvents.Switched -= OnSwitched;
            GameEvents.SilentCall -= OnSilentCall;
        }

        void OnJumped() { if (actor.IsLeader) Say(jump); }
        void OnDied(string zone) { if (actor.IsLeader) { Say(death); quietUntil = Time.time + 1f; } }
        void OnRespawned() { if (actor.IsLeader) { airborne = false; Invoke(nameof(SayRespawn), respawnDelay); } }
        void SayRespawn() => Say(respawn);
        void OnSwitched(Sense s) { if (s == actor.sense && actor.IsLeader) Say(switchIn); }
        void OnSilentCall() { if (actor.IsLeader) Say(silentCall); }

        void FixedUpdate()
        {
            if (!actor.IsLeader) { airborne = false; return; }
            if (!actor.Grounded)
            {
                if (!airborne) { airborne = true; minVy = 0f; }
                minVy = Mathf.Min(minVy, actor.Velocity.y);
            }
            else if (airborne)
            {
                airborne = false;
                if (-minVy >= hardLandSpeed && Time.time >= quietUntil) Say(hardLand);
            }
        }

        void Update()
        {
            float muffle = AudioManager.I ? AudioManager.I.Muffle
                         : SenseController.I && !SenseController.I.IsDark ? 1f : 0f;
            // log-space sweep so the cutoff glides evenly to the ear
            lp.cutoffFrequency = Mathf.Exp(Mathf.Lerp(Mathf.Log(22000f), Mathf.Log(muffledCutoff), Mathf.SmoothStep(0f, 1f, muffle)));
        }

        void Say(Line line)
        {
            if (line == null || line.clips == null || line.clips.Length == 0) return;
            if (Time.time - line.last < line.cooldown || Random.value > line.chance) return;
            var clip = line.clips[Random.Range(0, line.clips.Length)];
            if (!clip) return;
            line.last = Time.time;
            src.Stop();   // one voice at a time
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.clip = clip;
            src.volume = line.volume * (output ? GameSettings.SfxVolume : 1f);
            src.Play();
        }
    }
}

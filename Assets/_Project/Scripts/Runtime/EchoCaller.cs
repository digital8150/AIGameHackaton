using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// The prince calls out and the world answers. Button (R) always works; holding V samples the
    /// microphone and turns the player's own voice volume into echoes (design: "반향 — 마이크 음량만").
    /// </summary>
    public class EchoCaller : MonoBehaviour
    {
        public AudioClip callClip;
        public float cooldown = 0.45f;
        public float micThreshold = 0.02f;
        public float micInterval = 0.22f;

        public float LastMicLevel { get; private set; }
        public bool MicActive { get; private set; }
        public int CallCount { get; private set; }

        AudioSource sfx;
        float last = -9, lastMicEcho = -9;

        void Awake()
        {
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
        }

        void Update()
        {
            var input = GameInput.I;
            var duo = DuoController.I;
            if (input == null || duo == null || GameFlow.InputLocked) return;

            if (input.Echo.WasPressedThisFrame()) Call(1f);

            MicTick();
        }

        /// <summary>
        /// The prince calling out on purpose (button or voice) — unlike footsteps, checkpoints or a bell, which also
        /// make echoes. Echo switches and bells answer only to calls.
        /// </summary>
        public static event System.Action<Vector2, float> Called;

        /// <summary>Emit a called echo (also used by tests and the director).</summary>
        public static void CallAt(Vector2 p, float strength)
        {
            SenseController.I.EmitEcho(p, strength);
            Called?.Invoke(p, strength);
        }

        void Call(float strength)
        {
            var duo = DuoController.I;
            if (duo.Leader.sense != Sense.Dark)
            {
                // the princess cannot hear her own call: only silence comes back
                GameEvents.Caption?.Invoke("……");
                return;
            }
            if (Time.time - last < cooldown) return;
            last = Time.time;
            CallCount++;
            Vector2 p = (Vector2)duo.Leader.transform.position + Vector2.up * 0.6f;
            CallAt(p, strength);
            if (callClip) sfx.PlayOneShot(callClip, 0.8f);
            GameEvents.PuzzleAttempt?.Invoke("echo", true);
        }

        // the player's own voice, while V is held, becomes the prince's echo (loudness only — words go to Whisper)
        void MicTick()
        {
            var voice = VoiceInput.I;
            MicActive = voice && voice.Recording;
            LastMicLevel = MicActive ? voice.Level : 0f;
            if (!MicActive || LastMicLevel <= micThreshold || Time.time - lastMicEcho <= micInterval) return;
            lastMicEcho = Time.time;
            if (DuoController.I.Leader.sense != Sense.Dark) return;
            float s = Mathf.Clamp(LastMicLevel / (micThreshold * 6f), 0.35f, 1.4f);
            CallAt((Vector2)DuoController.I.Leader.transform.position + Vector2.up * 0.6f, s);
            CallCount++;
            GameEvents.PuzzleAttempt?.Invoke("echo", true);
        }
    }
}

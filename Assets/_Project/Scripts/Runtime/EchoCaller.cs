using UnityEngine;

namespace DuskTwilight
{
    public enum EchoSize { Small, Medium, Large }

    /// <summary>
    /// The prince calls out and the world answers. The echo comes in three sizes (기획: 메아리 크기):
    /// how long the echo key is held, or how loud the player's own voice is while V is held (마이크 음량만).
    /// Sizes are visible radii in metres; bells and switches hear an echo out to <c>reachShare</c> of that radius.
    /// </summary>
    public class EchoCaller : MonoBehaviour
    {
        public AudioClip callClip;
        public float cooldown = 0.9f;

        [Header("Echo sizes (visible radius, m)")]
        public float smallRadius = 5f;
        public float mediumRadius = 10f;
        public float largeRadius = 15f;

        [Header("Key hold")]
        [Tooltip("Held at least this long = medium (shorter = small)")]
        public float mediumHold = 0.25f;
        [Tooltip("Held this long = large; the echo goes off at once")]
        public float largeHold = 0.7f;
        [Tooltip("Faint pulse at the prince when the charge reaches the next size (no effect on the world)")]
        public float chargePulse = 0.12f;

        [Header("Microphone loudness")]
        public float micThreshold = 0.02f;
        [Tooltip("Level ≥ threshold × this = medium")]
        public float micMediumScale = 3f;
        [Tooltip("Level ≥ threshold × this = large")]
        public float micLargeScale = 7f;
        [Tooltip("Seconds of voice sampled for its peak before the echo goes off")]
        public float micWindow = 0.2f;

        public float LastMicLevel { get; private set; }
        public bool MicActive { get; private set; }
        public int CallCount { get; private set; }
        public EchoSize LastSize { get; private set; }
        /// <summary>Seconds the echo key has been held (−1 when not charging).</summary>
        public float Charge => charging ? Time.time - pressTime : -1f;

        AudioSource sfx;
        float last = -9, pressTime, micStart = -1, micPeak;
        bool charging;
        int chargeStep;

        void Awake()
        {
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
        }

        void Update()
        {
            var input = GameInput.I;
            var duo = DuoController.I;
            if (input == null || duo == null) return;
            if (GameFlow.InputLocked) { charging = false; return; }

            if (input.Echo.WasPressedThisFrame())
            {
                if (duo.Leader.sense != Sense.Dark) { GameEvents.Caption?.Invoke("……"); GameEvents.SilentCall?.Invoke(); }   // she cannot hear her own call
                else if (Time.time - last >= cooldown) { charging = true; pressTime = Time.time; chargeStep = 0; }
            }
            if (charging)
            {
                float held = Time.time - pressTime;
                int step = held >= largeHold ? 2 : held >= mediumHold ? 1 : 0;
                if (step > chargeStep)
                {
                    chargeStep = step;
                    if (chargePulse > 0) SenseController.I?.EmitEcho(CallPoint, chargePulse * step);
                }
                if (duo.Leader.sense != Sense.Dark) charging = false;
                else if (step == 2) { charging = false; Call(EchoSize.Large); }
                else if (!input.Echo.IsPressed()) { charging = false; Call(step == 1 ? EchoSize.Medium : EchoSize.Small); }
            }

            MicTick();
        }

        Vector2 CallPoint => (Vector2)DuoController.I.Leader.transform.position + Vector2.up * 0.6f;

        public float RadiusOf(EchoSize s) => s == EchoSize.Large ? largeRadius : s == EchoSize.Medium ? mediumRadius : smallRadius;

        /// <summary>
        /// The prince calling out on purpose (button or voice) — unlike footsteps, checkpoints or a bell, which also
        /// make echoes. Echo switches and bells answer to calls (and bells to each other, see <see cref="EchoResonator.Rang"/>).
        /// </summary>
        public static event System.Action<Vector2, float> Called;

        /// <summary>Emit a called echo of the given strength (radius = strength × echoRadiusPerStrength).</summary>
        public static void CallAt(Vector2 p, float strength)
        {
            SenseController.I.EmitEcho(p, strength);
            Called?.Invoke(p, strength);
        }

        /// <summary>Emit a called echo with a visible radius in metres (tests, director).</summary>
        public static void CallRadius(Vector2 p, float radius) => CallAt(p, StrengthFor(radius));

        public static float StrengthFor(float radius)
        {
            var sc = SenseController.I;
            return radius / Mathf.Max(0.01f, sc ? sc.echoRadiusPerStrength : 10f);
        }

        void Call(EchoSize size)
        {
            if (Time.time - last < cooldown) return;
            last = Time.time;
            CallCount++;
            LastSize = size;
            CallRadius(CallPoint, RadiusOf(size));
            if (callClip) sfx.PlayOneShot(callClip, size == EchoSize.Large ? 1f : size == EchoSize.Medium ? 0.8f : 0.55f);
            GameEvents.PuzzleAttempt?.Invoke("echo", true);
        }

        // the player's own voice, while V is held, becomes the prince's echo (loudness only — words go to Whisper)
        void MicTick()
        {
            var voice = VoiceInput.I;
            MicActive = voice && voice.Recording;
            LastMicLevel = MicActive ? voice.Level : 0f;
            if (!MicActive) { micStart = -1; return; }
            if (micStart < 0)
            {
                if (LastMicLevel <= micThreshold || Time.time - last < cooldown) return;
                micStart = Time.time; micPeak = 0;
            }
            micPeak = Mathf.Max(micPeak, LastMicLevel);
            if (Time.time - micStart < micWindow) return;
            micStart = -1;
            if (DuoController.I.Leader.sense != Sense.Dark) return;
            var size = micPeak >= micThreshold * micLargeScale ? EchoSize.Large
                     : micPeak >= micThreshold * micMediumScale ? EchoSize.Medium : EchoSize.Small;
            Call(size);
        }
    }
}

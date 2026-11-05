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
        /// <summary>0..1 of the way to a large echo (0 when not charging).</summary>
        public float Charge01 => charging ? Mathf.Clamp01((Time.time - pressTime) / Mathf.Max(largeHold, 0.01f)) : 0f;
        public bool Charging => charging;
        /// <summary>0 right after an echo, 1 when the next one is ready.</summary>
        public float CooldownProgress => cooldown > 0f ? Mathf.Clamp01((Time.time - last) / cooldown) : 1f;
        /// <summary>The prince himself let the echo go, by key or voice (not footsteps, bells or the director).</summary>
        public event System.Action<Vector2, EchoSize> Released;
        /// <summary>Where the prince's echo starts; null when there is no leader yet.</summary>
        public Vector2? CallPosition => DuoController.I && DuoController.I.Leader ? CallPoint : (Vector2?)null;

        /// <summary>Automated tests: behaves as if the echo key is held (see Actor.TestMove).</summary>
        public bool TestHoldEcho { get; set; }
        bool testHeld;

        AudioSource sfx;
        float last = -9, pressTime, micStart = -1, micPeak;
        bool charging, tapMayInteract;
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

            bool testPressed = TestHoldEcho && !testHeld;
            testHeld = TestHoldEcho;
            // a press that closed a word panel is not a call
            if ((input.Echo.WasPressedThisFrame() && !input.LockedLastFrame) || testPressed)
            {
                if (duo.Leader.sense != Sense.Dark)
                {
                    // she cannot hear her own call; E beside a box or a lever is her hands, not her voice
                    if (!InteractInReach && !PushBox.IsBesideAny(duo.Leader)) { GameEvents.Caption?.Invoke("……"); GameEvents.SilentCall?.Invoke(); }
                }
                else if (Time.time - last >= cooldown)
                {
                    charging = true; pressTime = Time.time; chargeStep = 0;
                    tapMayInteract = input.Interact.IsPressed() && InteractInReach;
                }
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
                else if (!input.Echo.IsPressed() && !TestHoldEcho)
                {
                    charging = false;
                    // a short press near a lever or word target was for them (GameInput.InteractNow)
                    if (!tapMayInteract || held >= input.interactTapSeconds) Call(step == 1 ? EchoSize.Medium : EchoSize.Small);
                }
            }

            MicTick();
        }

        /// <summary>Something the leader's E would act on right now.</summary>
        static bool InteractInReach => (Lever.Nearest && !Lever.Nearest.gate.Open)
                                    || (WordTarget.Nearest && WordTarget.Nearest.Listening);

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
            Released?.Invoke(CallPoint, size);
            if (callClip) sfx.PlayLimited(callClip, size == EchoSize.Large ? 1f : size == EchoSize.Medium ? 0.8f : 0.55f);
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

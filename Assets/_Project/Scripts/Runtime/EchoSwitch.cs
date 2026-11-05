using System;
using System.Collections;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// 메아리 스위치. Turns on when an echo's wavefront reaches it (only the prince can call echoes), and stays on
    /// until a <see cref="SwitchGate"/> resets it. The lamp on top is visible to both senses; in the princess's sense
    /// the click shows as a caption.
    /// </summary>
    public class EchoSwitch : MonoBehaviour, IResettable
    {
        [Header("Hit")]
        [Tooltip("Share of the echo's radius that counts as a hit (the shader's reveal fades from 70%)")]
        [Range(0.3f, 1f)] public float reachShare = 0.85f;
        [Tooltip("Size of the switch head (added to the echo radius)")]
        public float headRadius = 0.4f;
        [Tooltip("Where the head is, relative to this object")]
        public Vector2 headOffset = new Vector2(0, 1.6f);
        [Tooltip("Turns on when the prince's call reaches it")]
        public bool answersCalls = true;
        [Tooltip("Turns on when a bell's sound reaches it (6m bell → 5.5m to the head)")]
        public bool answersBells = true;

        [Header("Look")]
        public SpriteRenderer lamp;
        public Color offColor = new Color(0.35f, 0.3f, 0.4f, 1f);
        public Color onColor = new Color(1f, 0.82f, 0.4f, 1f);
        public UnityEngine.Rendering.Universal.Light2D lampLight;
        public string clickCaption = "딸깍";
        public AudioClip clickClip;
        [Tooltip("Echo the switch gives back when it clicks on (shows where it is)")]
        public float clickEcho = 0.25f;

        public bool On { get; private set; }
        public event Action<EchoSwitch> Changed;

        AudioSource sfx;
        Vector2 Head => (Vector2)transform.position + headOffset;

        void Awake()
        {
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            Apply();
        }

        // only the prince's deliberate call counts (not footsteps, checkpoints or other switches' clicks)
        void OnEnable() { EchoCaller.Called += OnCall; EchoResonator.Rang += OnBell; }
        void OnDisable() { EchoCaller.Called -= OnCall; EchoResonator.Rang -= OnBell; }

        void OnCall(Vector2 from, float strength) { if (answersCalls) OnEcho(from, strength); }
        void OnBell(Vector2 from, float strength, EchoResonator bell) { if (answersBells) OnEcho(from, strength); }

        void OnEcho(Vector2 from, float strength)
        {
            if (On || pending) return;
            var sc = SenseController.I;
            float r = strength * sc.echoRadiusPerStrength * sc.DirectorBoost * reachShare + headRadius;
            float d = Vector2.Distance(from, Head);
            if (d > r || d < 0.05f) return;   // its own click echo is ignored (d≈0)
            pending = true;
            StartCoroutine(TurnOnAfter(d / Mathf.Max(0.1f, sc.echoSpeed)));
        }

        bool pending;

        IEnumerator TurnOnAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            pending = false;
            if (On) yield break;
            On = true;
            Apply();
            if (clickClip) sfx.PlayOneShot(clickClip, 0.7f);
            if (!string.IsNullOrEmpty(clickCaption)) GameEvents.Caption?.Invoke(clickCaption);
            if (clickEcho > 0) SenseController.I?.EmitEcho(Head, clickEcho);
            GameEvents.PuzzleAttempt?.Invoke("switch", true);
            Changed?.Invoke(this);
        }

        /// <summary>Back to off (an overloaded gate resets all its switches).</summary>
        public void TurnOff()
        {
            if (!On) return;
            On = false;
            Apply();
            Changed?.Invoke(this);
        }

        public void ResetState()
        {
            StopAllCoroutines();
            pending = false;
            On = false;
            Apply();
        }

        void Apply()
        {
            if (lamp) lamp.color = On ? onColor : offColor;
            if (lampLight) lampLight.enabled = On;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = On ? new Color(1f, 0.8f, 0.3f) : new Color(0.7f, 0.6f, 1f);
            Gizmos.DrawWireSphere(Head, headRadius);
            if (!answersCalls) { Gizmos.DrawLine(Head + new Vector2(-0.3f, 0.3f), Head + new Vector2(0.3f, -0.3f)); }
        }
    }
}

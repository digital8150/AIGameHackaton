using System.Collections;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// The core puzzle (기획서 스테이지 3 "황혼"):
    ///  - which lever is wired to the hidden gear is only knowable by ear (prince: gear ticking + echo of the linkage),
    ///  - when to pull it is only knowable by eye (princess: the signal lantern turns gold).
    /// </summary>
    public class TwilightGate : MonoBehaviour
    {
        [Header("Parts")]
        public Transform gate;
        [Tooltip("How far the gate rises when it opens (world units).")]
        public float gateRise = 4f;
        [Tooltip("Seconds the gate takes to rise.")]
        public float openDuration = 1.4f;
        public SpriteRenderer lanternLamp;
        public Renderer lanternGlow;
        public SoundEmitter gear;
        public Transform gearVisual;

        [Header("Signal rhythm")]
        public float waitTime = 2.1f;
        public float goTime = 1.0f;
        public Color waitColor = new Color(0.95f, 0.30f, 0.28f);
        public Color goColor = new Color(1f, 0.85f, 0.35f);
        [Tooltip("Seconds the mechanism stays jammed after a wrong pull.")]
        public float jamDuration = 1.5f;

        [Header("Gear (hidden mechanism)")]
        [Tooltip("Gear visual spin speed (deg/s): closed, while opening/open.")]
        public Vector2 gearSpin = new Vector2(90f, 260f);
        [Tooltip("Gear sound while the gate rises: tick interval, echo strength.")]
        public Vector2 gearOpening = new Vector2(0.35f, 1.1f);
        [Tooltip("Gear sound after the gate is open: tick interval, echo strength.")]
        public Vector2 gearOpened = new Vector2(1.6f, 0.6f);

        [Header("Captions (what the sound looks like in the princess's sense)")]
        public string jammedCaption = "끼긱—";
        public string wrongLeverCaption = "덜컹—";
        public string wrongTimingCaption = "철컥—";
        public string openCaption = "쿠르르릉—";

        [Header("Audio")]
        public AudioClip openClip, jamClip, leverClip;

        [HideInInspector] public float glowBoost = 1f; // raised by the accessibility director

        public bool IsGo { get; private set; }
        public bool Open { get; private set; }
        public bool Jammed => Time.time < jamUntil;
        public int Attempts { get; private set; }

        float cycle, jamUntil;
        AudioSource sfx;
        MaterialPropertyBlock mpb;
        static readonly int IdGlowColor = Shader.PropertyToID("_GlowColor");
        static readonly int IdIntensity = Shader.PropertyToID("_Intensity");

        void Awake()
        {
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            mpb = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (!Open)
            {
                cycle += Time.deltaTime;
                if (cycle > waitTime + goTime) cycle -= waitTime + goTime;
                IsGo = cycle > waitTime && !Jammed;
            }
            else IsGo = true;

            Color c = IsGo ? goColor : waitColor;
            float flicker = Jammed ? (Mathf.PingPong(Time.time * 14f, 1f) > 0.5f ? 1f : 0.2f) : 1f;
            if (lanternLamp) lanternLamp.color = Color.Lerp(Color.white, c, 0.7f) * flicker + new Color(0, 0, 0, 1);
            if (lanternGlow)
            {
                lanternGlow.GetPropertyBlock(mpb);
                mpb.SetColor(IdGlowColor, c);
                mpb.SetFloat(IdIntensity, (IsGo ? 1.6f : 0.7f) * flicker * glowBoost);
                lanternGlow.SetPropertyBlock(mpb);
            }
            if (gearVisual && !Jammed) gearVisual.Rotate(0, 0, -(Open ? gearSpin.y : gearSpin.x) * Time.deltaTime);
        }

        public void Pull(Lever lever)
        {
            if (Open) return;
            if (leverClip) sfx.PlayLimited(leverClip, 0.7f);
            if (Jammed) { GameEvents.Caption?.Invoke(jammedCaption); return; }
            Attempts++;
            bool ok = lever.wired && IsGo;
            GameEvents.PuzzleAttempt?.Invoke(ok ? "gate" : lever.wired ? "gate-timing" : "gate-lever", ok);
            if (ok) StartCoroutine(OpenRoutine());
            else
            {
                jamUntil = Time.time + jamDuration;
                if (jamClip) sfx.PlayLimited(jamClip, 0.9f);
                CameraRig.I?.Kick(0.18f);
                SenseController.I?.EmitEcho(lever.transform.position, 0.5f);
                GameEvents.Caption?.Invoke(lever.wired ? wrongTimingCaption : wrongLeverCaption);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (gate)
            {
                // where the gate ends up when open
                Gizmos.color = new Color(1f, 0.85f, 0.35f, 0.8f);
                var up = gate.position + Vector3.up * gateRise;
                Gizmos.DrawLine(gate.position, up);
                Gizmos.DrawWireCube(up, new Vector3(1f, 0.2f, 0));
            }
            foreach (var l in FindObjectsByType<Lever>(FindObjectsSortMode.None))
            {
                if (l.gate != this) continue;
                Gizmos.color = l.wired ? new Color(1f, 0.85f, 0.35f) : new Color(0.6f, 0.6f, 0.7f, 0.6f);
                Gizmos.DrawLine(transform.position, l.transform.position);
            }
        }
#endif

        IEnumerator OpenRoutine()
        {
            Open = true;
            if (gear) { gear.interval = gearOpening.x; gear.echoStrength = gearOpening.y; }
            if (openClip) sfx.PlayLimited(openClip, 1f);
            SenseController.I?.EmitEcho(gate.position, 1.6f);
            GameEvents.Caption?.Invoke(openCaption);
            CameraRig.I?.Kick(0.35f);
            Vector3 a = gate.localPosition, b = a + Vector3.up * gateRise;
            for (float t = 0; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, openDuration))
            {
                gate.localPosition = Vector3.Lerp(a, b, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
            gate.localPosition = b;
            if (gear) { gear.interval = gearOpened.x; gear.echoStrength = gearOpened.y; }
        }
    }
}

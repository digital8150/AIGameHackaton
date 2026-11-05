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
        public float gateRise = 4f;
        public SpriteRenderer lanternLamp;
        public Renderer lanternGlow;
        public SoundEmitter gear;
        public Transform gearVisual;

        [Header("Signal rhythm")]
        public float waitTime = 2.1f;
        public float goTime = 1.0f;
        public Color waitColor = new Color(0.95f, 0.30f, 0.28f);
        public Color goColor = new Color(1f, 0.85f, 0.35f);

        [Header("Audio")]
        public AudioClip openClip, jamClip, leverClip;

        public float glowBoost = 1f; // raised by the accessibility director

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
            if (gearVisual && !Jammed) gearVisual.Rotate(0, 0, (Open ? -260f : -90f) * Time.deltaTime);
        }

        public void Pull(Lever lever)
        {
            if (Open) return;
            if (leverClip) sfx.PlayOneShot(leverClip, 0.7f);
            if (Jammed) { GameEvents.Caption?.Invoke("끼긱—"); return; }
            Attempts++;
            bool ok = lever.wired && IsGo;
            GameEvents.PuzzleAttempt?.Invoke(ok ? "gate" : lever.wired ? "gate-timing" : "gate-lever", ok);
            if (ok) StartCoroutine(OpenRoutine());
            else
            {
                jamUntil = Time.time + 1.5f;
                if (jamClip) sfx.PlayOneShot(jamClip, 0.9f);
                CameraRig.I?.Kick(0.18f);
                SenseController.I?.EmitEcho(lever.transform.position, 0.5f);
                GameEvents.Caption?.Invoke(lever.wired ? "철컥—" : "덜컹—");
            }
        }

        IEnumerator OpenRoutine()
        {
            Open = true;
            if (gear) { gear.interval = 0.35f; gear.echoStrength = 1.1f; }
            if (openClip) sfx.PlayOneShot(openClip, 1f);
            SenseController.I?.EmitEcho(gate.position, 1.6f);
            GameEvents.Caption?.Invoke("쿠르르릉—");
            CameraRig.I?.Kick(0.35f);
            Vector3 a = gate.localPosition, b = a + Vector3.up * gateRise;
            for (float t = 0; t < 1f; t += Time.deltaTime / 1.4f)
            {
                gate.localPosition = Vector3.Lerp(a, b, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
            gate.localPosition = b;
            if (gear) { gear.interval = 1.6f; gear.echoStrength = 0.6f; }
        }
    }
}

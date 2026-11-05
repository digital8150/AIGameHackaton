using System.Collections.Generic;
using UnityEngine;

namespace DuskTwilight
{
    public enum MissedSignal { None, Sound, Visual, Timing }
    public enum Intervention { None, Subtle, Strong }

    public struct DirectorObservation
    {
        public Sense mode;
        public string zone;
        public string clueSense;      // sound | visual | timing
        public float timeInZone;
        public float darkFraction;    // share of time in zone spent in the prince's (sound) sense
        public int deathsInZone;
        public int switchesInZone;
        public int echoCallsInZone;
        public int failsInZone;
        public int leverFails;        // pulled a lever the gear is not wired to (never listened)
        public int timingFails;       // right lever, lantern was not gold (never looked)
        public string trigger;        // death | stuck | fail
    }

    public struct DirectorDecision
    {
        public MissedSignal missed;
        public Intervention intensity;
        public float confidence;
        public string source;
    }

    /// <summary>How hard each assist pushes. x = subtle, y = strong.</summary>
    [System.Serializable]
    public class AssistTuning
    {
        [Tooltip("Echo wave size multiplier (sound missed).")]
        public Vector2 echoBoost = new Vector2(1.35f, 1.7f);
        [Tooltip("Sound emitter loudness (sound missed).")]
        public Vector2 emitterLoudness = new Vector2(1.4f, 1.8f);
        [Tooltip("Signal lantern glow multiplier (visual missed).")]
        public Vector2 lanternGlow = new Vector2(1.6f, 2.2f);
        [Tooltip("Gold window length multiplier (timing missed).")]
        public Vector2 goTimeScale = new Vector2(1.5f, 1.9f);
        [Tooltip("Light planks: lit share of the period (timing missed).")]
        public Vector2 plankOnRatio = new Vector2(0.75f, 0.85f);
    }

    /// <summary>Everything the director says. Hints are whispered; notices are the small assist chip.</summary>
    [System.Serializable]
    public class AssistLines
    {
        public string soundHint = "…왕자라면, 들을 수 있을지도.";
        public string visualHint = "…공주라면, 볼 수 있을지도.";
        public string timingHintGate = "…금빛이 머무는 그 순간에.";
        public string timingHintPlanks = "…빛이 숨을 쉬는 박자에.";
        public string noZoneHint = "…둘이 함께라면, 길은 있어.";
        public string soundNotice = "♪ 소리를 더 크게";
        public string visualNotice = "☀ 빛을 더 밝게";
        public string timingNoticeGate = "◆ 금빛을 더 길게";
        public string timingNoticePlanks = "◆ 빛을 더 오래";
        public string assistOn = "보조 켜짐";
        public string assistOff = "보조 꺼짐";
    }

    /// <summary>
    /// Brain interface. The design calls for Jev (judgement AI: pick-one-with-confidence).
    /// This scene ships the rule-based fallback; a Jev-backed brain implements the same two questions.
    /// </summary>
    public interface IDirectorBrain
    {
        DirectorDecision Decide(DirectorObservation o);
    }

    public class RuleBasedBrain : IDirectorBrain
    {
        public DirectorDecision Decide(DirectorObservation o)
        {
            var d = new DirectorDecision { missed = MissedSignal.None, intensity = Intervention.None, confidence = 0.3f, source = "규칙 폴백" };
            switch (o.clueSense)
            {
                case "sound":
                    if (o.darkFraction < 0.25f || o.echoCallsInZone == 0)
                    {
                        d.missed = MissedSignal.Sound;
                        d.confidence = Mathf.Min(0.95f, 0.62f + 0.09f * o.deathsInZone + (o.echoCallsInZone == 0 ? 0.1f : 0f) + (o.trigger == "stuck" ? 0.05f : 0f));
                    }
                    break;
                case "visual":
                    // the clue is light-only (inscription, light planks)
                    if (o.failsInZone + o.deathsInZone >= 1 && o.darkFraction > 0.5f)
                    {
                        d.missed = MissedSignal.Visual;
                        d.confidence = Mathf.Min(0.93f, 0.62f + 0.1f * (o.failsInZone + o.deathsInZone));
                    }
                    else if (o.deathsInZone >= 2)
                    {
                        d.missed = MissedSignal.Timing;
                        d.confidence = Mathf.Min(0.9f, 0.55f + 0.08f * o.deathsInZone);
                    }
                    break;
                case "timing":
                    if (o.leverFails >= 1 && o.darkFraction < 0.2f)
                    {
                        // guessing levers without ever listening for the gear
                        d.missed = MissedSignal.Sound;
                        d.confidence = Mathf.Min(0.92f, 0.6f + 0.1f * o.leverFails);
                    }
                    else if (o.timingFails >= 1 && o.darkFraction > 0.5f)
                    {
                        d.missed = MissedSignal.Visual;
                        d.confidence = Mathf.Min(0.93f, 0.6f + 0.1f * o.failsInZone);
                    }
                    else if (o.timingFails >= 2)
                    {
                        // they can see the lantern but keep missing the window
                        d.missed = MissedSignal.Timing;
                        d.confidence = Mathf.Min(0.9f, 0.55f + 0.08f * o.failsInZone);
                    }
                    else if (o.darkFraction < 0.15f && o.trigger == "stuck")
                    {
                        d.missed = MissedSignal.Sound; // never listened for which lever is wired
                        d.confidence = 0.7f;
                    }
                    break;
            }
            if (d.missed != MissedSignal.None)
                d.intensity = (o.deathsInZone + o.failsInZone >= 3 || o.timeInZone > 50f) ? Intervention.Strong : Intervention.Subtle;
            return d;
        }
    }

    /// <summary>
    /// 적응형 접근성 디렉터. Event-driven (death / 30s in a zone / failed attempt), acts only when confident,
    /// always says what it changed, and can be switched off (F2). It never diagnoses the player — it only
    /// decides "this signal is being missed".
    /// </summary>
    public class AccessibilityDirector : MonoBehaviour
    {
        public static AccessibilityDirector I { get; private set; }

        [Header("Judgement")]
        [Range(0, 1)] public float confidenceThreshold = 0.65f;
        [Tooltip("Seconds between two interventions.")]
        public float cooldown = 18f;
        [Tooltip("Seconds in one zone before the director checks whether the player is stuck.")]
        public float stuckSeconds = 30f;

        [Header("What it can adjust (empty = collect from the scene)")]
        public TwilightGate gate;
        public SoundEmitter[] emitters = new SoundEmitter[0];
        public LightPlatform[] lightPlatforms = new LightPlatform[0];

        [Header("Assist strength (subtle → strong)")]
        public AssistTuning tuning = new AssistTuning();

        [Header("Words (whisper = narration, notice = assist chip)")]
        public AssistLines lines = new AssistLines();

        public bool Enabled { get; private set; } = true;
        public readonly List<string> Log = new List<string>();
        public DirectorObservation LastObservation { get; private set; }

        IDirectorBrain brain = new RuleBasedBrain();
        readonly JevDirectorBrain jev = new JevDirectorBrain();
        DirectorZone[] zones;
        DirectorZone current;
        float zoneEnter, darkTime, lastAct = -99, lastStuckCheck;
        int deaths, switches, echoes, fails, leverFails, timingFails;
        float baseGoTime;
        float[] baseOnTimes = new float[0];

        void Awake() { I = this; }

        void Start()
        {
            zones = FindObjectsByType<DirectorZone>(FindObjectsSortMode.None);
            if (!gate) gate = FindAnyObjectByType<TwilightGate>();
            if (emitters == null || emitters.Length == 0) emitters = FindObjectsByType<SoundEmitter>(FindObjectsSortMode.None);
            if (lightPlatforms == null || lightPlatforms.Length == 0) lightPlatforms = FindObjectsByType<LightPlatform>(FindObjectsSortMode.None);
            if (gate) baseGoTime = gate.goTime;
            baseOnTimes = System.Array.ConvertAll(lightPlatforms, p => p ? p.onTime : 0f);
            GameEvents.Died += z => { deaths++; Evaluate("death"); };
            GameEvents.Switched += s => switches++;
            GameEvents.PuzzleAttempt += (id, ok) =>
            {
                if (id == "echo") { echoes++; return; }
                if (!ok) { fails++; if (id == "gate-lever") leverFails++; if (id == "gate-timing") timingFails++; Evaluate("fail"); }
            };
        }

        void Update()
        {
            if (GameInput.I && GameInput.I.DirectorToggle.WasPressedThisFrame()) Toggle();

            var duo = DuoController.I;
            if (!duo) return;
            float x = duo.Leader.transform.position.x;
            DirectorZone z = null;
            foreach (var cand in zones) if (cand.Contains(x)) { z = cand; break; }
            if (z != current)
            {
                current = z;
                zoneEnter = Time.time;
                darkTime = 0; deaths = 0; switches = 0; echoes = 0; fails = 0; leverFails = 0; timingFails = 0;
                lastStuckCheck = Time.time;
                if (z) GameEvents.ZoneEntered?.Invoke(z.zoneId);
            }
            if (SenseController.I && SenseController.I.IsDark) darkTime += Time.deltaTime;
            if (current && Time.time - lastStuckCheck > stuckSeconds)
            {
                lastStuckCheck = Time.time;
                Evaluate("stuck");
            }
        }

        DirectorObservation Observe(string trigger)
        {
            float t = Mathf.Max(0.01f, Time.time - zoneEnter);
            return new DirectorObservation
            {
                mode = SenseController.I ? SenseController.I.Current : Sense.Light,
                zone = current ? current.zoneId : "-",
                clueSense = current ? current.ClueId : "-",
                timeInZone = t,
                darkFraction = Mathf.Clamp01(darkTime / t),
                deathsInZone = deaths,
                switchesInZone = switches,
                echoCallsInZone = echoes,
                failsInZone = fails,
                leverFails = leverFails,
                timingFails = timingFails,
                trigger = trigger
            };
        }

        async void Evaluate(string trigger)
        {
            if (!current) return;
            var o = Observe(trigger);
            LastObservation = o;
            // Jev first (judgement AI); rules if there is no key, no network, or no timely answer
            var d = await jev.DecideAsync(o) ?? brain.Decide(o);
            if (!this) return;
            Resolve(trigger, o, d);
        }

        /// <summary>Player asked for help by voice ("힌트"): always answer, gently, with the zone's own clue.</summary>
        public async void RequestHint()
        {
            var o = Observe("asked");
            var d = current ? (await jev.DecideAsync(o) ?? brain.Decide(o)) : default;
            if (!this) return;
            if (d.missed == MissedSignal.None && current)
                d.missed = current.clue == ClueSense.Sound ? MissedSignal.Sound : current.clue == ClueSense.Visual ? MissedSignal.Visual : MissedSignal.Timing;
            if (d.missed == MissedSignal.None) { GameEvents.Hint?.Invoke(lines.noZoneHint); return; }
            d.intensity = Intervention.Subtle;
            d.confidence = Mathf.Max(d.confidence, confidenceThreshold);
            lastAct = -99;
            Resolve("asked", o, d);
        }

        void Resolve(string trigger, DirectorObservation o, DirectorDecision d)
        {
            string head = $"[{trigger}] {o.zone} · 소리감각 {o.darkFraction:P0} · 사망 {o.deathsInZone} · 실패 {o.failsInZone}";
            if (!Enabled) { Push($"{head}\n  → 디렉터 꺼짐 (F2)"); return; }
            if (d.missed == MissedSignal.None || d.confidence < confidenceThreshold)
            {
                Push($"{head}\n  → 판단({d.source}): 개입 안 함 (확신도 {d.confidence:P0})");
                return;
            }
            if (Time.time - lastAct < cooldown)
            {
                Push($"{head}\n  → 판단: {Name(d.missed)} 놓침 ({d.confidence:P0}) · 쿨다운 중");
                return;
            }
            lastAct = Time.time;
            string act = Apply(d);
            Push($"{head}\n  → 판단({d.source}): {Name(d.missed)} 신호 놓침 (확신도 {d.confidence:P0}) → {act}");
        }

        string Apply(DirectorDecision d)
        {
            bool strong = d.intensity == Intervention.Strong;
            var t = tuning;
            string Whisper(string fallback) => current && !string.IsNullOrEmpty(current.hintOverride) ? current.hintOverride : fallback;
            switch (d.missed)
            {
                case MissedSignal.Sound:
                    SenseController.I.DirectorBoost = strong ? t.echoBoost.y : t.echoBoost.x;
                    foreach (var e in emitters) if (e) e.Loudness = strong ? t.emitterLoudness.y : t.emitterLoudness.x;
                    GameEvents.Hint?.Invoke(Whisper(lines.soundHint));
                    GameEvents.Notice?.Invoke(lines.soundNotice);
                    return strong ? "파동 크게 강화" : "파동 강화";
                case MissedSignal.Visual:
                    GameEvents.Hint?.Invoke(Whisper(lines.visualHint));
                    if (gate) { gate.glowBoost = strong ? t.lanternGlow.y : t.lanternGlow.x; GameEvents.Notice?.Invoke(lines.visualNotice); }
                    return "빛 신호 대비 강화";
                case MissedSignal.Timing:
                    GameEvents.Hint?.Invoke(Whisper(gate ? lines.timingHintGate : lines.timingHintPlanks));
                    if (gate) { gate.goTime = baseGoTime * (strong ? t.goTimeScale.y : t.goTimeScale.x); GameEvents.Notice?.Invoke(lines.timingNoticeGate); }
                    if (lightPlatforms.Length > 0)
                    {
                        foreach (var lp in lightPlatforms) if (lp) lp.onTime = lp.period * (strong ? t.plankOnRatio.y : t.plankOnRatio.x);
                        GameEvents.Notice?.Invoke(lines.timingNoticePlanks);
                    }
                    return "금빛 신호 시간 연장";
            }
            return "-";
        }

        void Toggle()
        {
            Enabled = !Enabled;
            if (!Enabled)
            {
                if (SenseController.I) SenseController.I.DirectorBoost = 1f;
                foreach (var e in emitters) if (e) e.Loudness = 1f;
                if (gate) { gate.glowBoost = 1f; gate.goTime = baseGoTime; }
                for (int i = 0; i < lightPlatforms.Length; i++) if (lightPlatforms[i]) lightPlatforms[i].onTime = baseOnTimes[i];
            }
            GameEvents.Notice?.Invoke(Enabled ? lines.assistOn : lines.assistOff);
            Push(Enabled ? "디렉터 켜짐" : "디렉터 꺼짐 — 조정 초기화");
        }

        static string Name(MissedSignal m) => m switch
        {
            MissedSignal.Sound => "소리",
            MissedSignal.Visual => "시각",
            MissedSignal.Timing => "타이밍",
            _ => "없음"
        };

        void Push(string line)
        {
            Log.Add(line);
            if (Log.Count > 6) Log.RemoveAt(0);
        }
    }
}

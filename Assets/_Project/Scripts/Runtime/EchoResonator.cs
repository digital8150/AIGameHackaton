using System;
using System.Collections;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// 종. A bell answers an echo that reaches it with its own fixed-size echo from where it hangs (기획: 종 반지름 6m).
    /// Bells can also answer each other (연쇄 공명) and set off switches that listen to bells.
    /// The princess sees it swing and a caption.
    /// </summary>
    public class EchoResonator : MonoBehaviour, IResettable
    {
        [Header("Ring")]
        [Tooltip("Visible radius of the bell's own echo, in metres (fixed — 기획 6m)")]
        public float ringRadius = 6f;
        [Tooltip("Where the sound comes from, relative to this object (e.g. under the bell)")]
        public Vector2 soundOffset = new Vector2(0, -1.2f);
        [Tooltip("Share of an incoming echo's radius that counts as reaching it (6m bell → 5.1m)")]
        [Range(0.3f, 1f)] public float reachShare = 0.85f;
        [Tooltip("Rings when the prince's call reaches it")]
        public bool answersCalls = true;
        [Tooltip("Rings when another bell's sound reaches it (연쇄 공명)")]
        public bool answersBells = true;
        [Tooltip("Can't ring again for this long (also stops two bells ringing each other forever)")]
        public float cooldown = 2f;

        [Header("Look")]
        [Tooltip("Swings when it rings (pivot at the rope)")]
        public Transform swing;
        public float swingAngle = 14f;
        public string caption = "댕—";
        public AudioClip ringClip;

        /// <summary>A bell rang: where its sound starts and its strength.</summary>
        public static event Action<Vector2, float, EchoResonator> Rang;

        AudioSource sfx;
        float lastRing = -99f;
        public Vector2 Source => (Vector2)transform.position + soundOffset;
        float Strength => EchoCaller.StrengthFor(ringRadius);

        void Awake()
        {
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
        }

        void OnEnable() { EchoCaller.Called += OnCall; Rang += OnBell; }
        void OnDisable() { EchoCaller.Called -= OnCall; Rang -= OnBell; }

        void OnCall(Vector2 from, float strength) { if (answersCalls) Hear(from, strength); }
        void OnBell(Vector2 from, float strength, EchoResonator bell) { if (answersBells && bell != this) Hear(from, strength); }

        void Hear(Vector2 from, float strength)
        {
            float d = Vector2.Distance(from, transform.position);
            var sc = SenseController.I;
            float r = strength * sc.echoRadiusPerStrength * sc.DirectorBoost * reachShare;
            if (d > r || Time.time - lastRing < cooldown) return;
            float delay = d / Mathf.Max(0.1f, sc.echoSpeed);
            lastRing = Time.time + delay;
            StartCoroutine(Ring(delay));
        }

        IEnumerator Ring(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (ringClip) sfx.PlayOneShot(ringClip, 0.8f);
            if (!string.IsNullOrEmpty(caption)) GameEvents.Caption?.Invoke(caption);
            SenseController.I?.EmitEcho(Source, Strength);
            Rang?.Invoke(Source, Strength, this);
            GameEvents.PuzzleAttempt?.Invoke("bell", true);
            if (!swing) yield break;
            for (float t = 0; t < 1.6f; t += Time.deltaTime)
            {
                swing.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 9f) * swingAngle * (1f - t / 1.6f));
                yield return null;
            }
            swing.localRotation = Quaternion.identity;
        }

        public void ResetState()
        {
            StopAllCoroutines();
            lastRing = -99f;
            if (swing) swing.localRotation = Quaternion.identity;
        }

        void OnDrawGizmos()
        {
            // reach of its own sound: to other bells (inner), to switch heads (+ their head radius)
            Gizmos.color = new Color(0.7f, 0.6f, 1f, 0.6f);
            Gizmos.DrawWireSphere(Source, ringRadius * reachShare);
            Gizmos.color = new Color(0.7f, 0.6f, 1f, 0.2f);
            Gizmos.DrawWireSphere(Source, ringRadius);
        }
    }
}

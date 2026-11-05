using System.Collections;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// 첫 종소리. A bell (or anything that rings) answers an echo that reaches it with its own echo from where it
    /// hangs — sound carried further than the prince's voice. The princess sees it swing and a caption.
    /// </summary>
    public class EchoResonator : MonoBehaviour
    {
        [Header("Ring")]
        [Tooltip("Strength of the echo it gives back (radius = strength × SenseController.echoRadiusPerStrength)")]
        public float ringStrength = 0.7f;
        [Tooltip("Where the sound comes from, relative to this object (e.g. under the bell)")]
        public Vector2 soundOffset = new Vector2(0, -1.2f);
        [Tooltip("Share of the incoming echo's radius that counts as reaching it")]
        [Range(0.3f, 1f)] public float reachShare = 0.85f;
        public float cooldown = 1.2f;

        [Header("Look")]
        [Tooltip("Swings when it rings (pivot at the rope)")]
        public Transform swing;
        public float swingAngle = 14f;
        public string caption = "댕—";
        public AudioClip ringClip;

        AudioSource sfx;
        float lastRing = -99f;
        Vector2 Source => (Vector2)transform.position + soundOffset;

        void Awake()
        {
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
        }

        // answers the prince's call only (its own ring and footsteps don't set it off)
        void OnEnable() { EchoCaller.Called += OnEcho; }
        void OnDisable() { EchoCaller.Called -= OnEcho; }

        void OnEcho(Vector2 from, float strength)
        {
            float d = Vector2.Distance(from, transform.position);
            if (d < 0.3f) return;   // our own ring
            var sc = SenseController.I;
            float r = strength * sc.echoRadiusPerStrength * sc.DirectorBoost * reachShare;
            if (d > r || Time.time - lastRing < cooldown) return;
            lastRing = Time.time + d / Mathf.Max(0.1f, sc.echoSpeed);
            StartCoroutine(Ring(d / Mathf.Max(0.1f, sc.echoSpeed)));
        }

        IEnumerator Ring(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (ringClip) sfx.PlayOneShot(ringClip, 0.8f);
            if (!string.IsNullOrEmpty(caption)) GameEvents.Caption?.Invoke(caption);
            SenseController.I?.EmitEcho(Source, ringStrength);
            GameEvents.PuzzleAttempt?.Invoke("bell", true);
            if (!swing) yield break;
            for (float t = 0; t < 1.6f; t += Time.deltaTime)
            {
                swing.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 9f) * swingAngle * (1f - t / 1.6f));
                yield return null;
            }
            swing.localRotation = Quaternion.identity;
        }

        void OnDrawGizmos()
        {
            if (!Application.isPlaying && !SenseController.I) { Gizmos.color = new Color(0.7f, 0.6f, 1f, 0.6f); Gizmos.DrawWireSphere(Source, ringStrength * 10f * reachShare); return; }
            var sc = SenseController.I;
            Gizmos.color = new Color(0.7f, 0.6f, 1f, 0.6f);
            Gizmos.DrawWireSphere(Source, ringStrength * (sc ? sc.echoRadiusPerStrength : 10f) * reachShare);
        }
    }
}

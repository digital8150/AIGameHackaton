using System.Collections;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// Listens to echo switches. When every <see cref="required"/> switch is on (and none of <see cref="forbidden"/>),
    /// it opens: a door rises and/or objects appear (light platforms, a word on an altar…).
    /// If a forbidden switch comes on too (두 칸의 문: the echo reached too far) every switch turns off again.
    /// Indicator lamps on the door count the required switches that are on.
    /// </summary>
    public class SwitchGate : MonoBehaviour, IResettable
    {
        [Header("Switches")]
        public EchoSwitch[] required;
        [Tooltip("Turning one of these on overloads the gate: all switches reset")]
        public EchoSwitch[] forbidden;
        [Tooltip("Pause before an overload resets everything (so the player sees what happened)")]
        public float resetDelay = 0.8f;
        public string overloadCaption = "철컹";
        [Tooltip("Wait this long after the last required switch before opening, so a forbidden switch further out on the same echo still counts")]
        public float settleTime = 0.7f;

        [Header("Opens")]
        [Tooltip("Rises out of the way (keeps its collider)")]
        public Transform door;
        public float doorRise = 5.5f;
        public float doorTime = 1.1f;
        [Tooltip("Set active when the gate opens (inactive in the scene until then)")]
        public GameObject[] reveal;
        [Tooltip("Enabled when the gate opens (disabled in the scene until then)")]
        public Behaviour[] enable;

        [Header("Indicators")]
        [Tooltip("One per required switch, lit in order as switches come on")]
        public SpriteRenderer[] indicators;
        public Color indicatorOff = new Color(0.3f, 0.25f, 0.35f, 1f);
        public Color indicatorOn = new Color(1f, 0.82f, 0.4f, 1f);

        public bool Open { get; private set; }
        bool resetting, settling;

        void Start()
        {
            foreach (var s in required) if (s) s.Changed += OnChanged;
            if (forbidden != null) foreach (var s in forbidden) if (s) s.Changed += OnChanged;
            if (reveal != null) foreach (var g in reveal) if (g) g.SetActive(false);
            if (enable != null) foreach (var b in enable) if (b) b.enabled = false;
            Refresh();
        }

        void OnChanged(EchoSwitch s)
        {
            Refresh();
            if (Open || resetting) return;
            bool overloaded = false;
            if (forbidden != null) foreach (var f in forbidden) if (f && f.On) overloaded = true;
            if (overloaded) { StartCoroutine(ResetAll()); return; }
            foreach (var r in required) if (r && !r.On) return;
            if (!settling) StartCoroutine(Settle());
        }

        IEnumerator Settle()
        {
            settling = true;
            yield return new WaitForSeconds(settleTime);
            settling = false;
            if (Open || resetting) yield break;
            if (forbidden != null) foreach (var f in forbidden) if (f && f.On) yield break;   // overload is already resetting
            foreach (var r in required) if (r && !r.On) yield break;
            StartCoroutine(DoOpen());
        }

        IEnumerator ResetAll()
        {
            resetting = true;
            GameEvents.PuzzleAttempt?.Invoke("switch-gate", false);
            yield return new WaitForSeconds(resetDelay);
            if (!string.IsNullOrEmpty(overloadCaption)) GameEvents.Caption?.Invoke(overloadCaption);
            CameraRig.I?.Kick(0.12f);
            foreach (var r in required) if (r) r.TurnOff();
            if (forbidden != null) foreach (var f in forbidden) if (f) f.TurnOff();
            resetting = false;
            Refresh();
        }

        IEnumerator DoOpen()
        {
            Open = true;
            GameEvents.PuzzleAttempt?.Invoke("switch-gate", true);
            if (reveal != null) foreach (var g in reveal) if (g) g.SetActive(true);
            if (enable != null) foreach (var be in enable) if (be) be.enabled = true;
            if (!door) yield break;
            SenseController.I?.EmitEcho(door.position, 1.2f);
            CameraRig.I?.Kick(0.2f);
            Vector3 a = door.localPosition, b = a + Vector3.up * doorRise;
            for (float t = 0; t < 1; t += Time.deltaTime / Mathf.Max(0.05f, doorTime))
            {
                door.localPosition = Vector3.Lerp(a, b, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
            door.localPosition = b;
        }

        /// <summary>Falling in the section turns the switches off again (an opened gate stays open).</summary>
        public void ResetState()
        {
            if (Open) return;
            StopAllCoroutines();
            resetting = settling = false;
            foreach (var r in required) if (r) r.ResetState();
            if (forbidden != null) foreach (var f in forbidden) if (f) f.ResetState();
            Refresh();
        }

        void Refresh()
        {
            if (indicators == null) return;
            int on = 0;
            foreach (var r in required) if (r && r.On) on++;
            for (int i = 0; i < indicators.Length; i++)
                if (indicators[i]) indicators[i].color = (Open || i < on) ? indicatorOn : indicatorOff;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.8f);
            if (required != null) foreach (var r in required) if (r) Gizmos.DrawLine(transform.position, r.transform.position);
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
            if (forbidden != null) foreach (var f in forbidden) if (f) Gizmos.DrawLine(transform.position, f.transform.position);
            if (door) { Gizmos.color = Color.white; Gizmos.DrawLine(door.position, door.position + Vector3.up * doorRise); }
        }
    }
}

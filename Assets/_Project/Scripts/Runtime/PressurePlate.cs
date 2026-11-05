using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// 압력판과 철문. The princess standing on it, or a box resting on it, holds the iron gate open; the prince is
    /// too light to press it. Step off and the gate drops again — a box keeps it open for good.
    /// </summary>
    public class PressurePlate : MonoBehaviour
    {
        [Header("Plate")]
        [Tooltip("Width of the pressing area (centred on this object)")]
        public float width = 2f;
        [Tooltip("How far the plate sinks when pressed")]
        public float pressDepth = 0.12f;
        public Transform plateVisual;

        [Header("Gate")]
        [Tooltip("The iron gate (keeps its collider while closed)")]
        public Transform gate;
        public float gateRise = 5f;
        [Tooltip("Metres per second")]
        public float gateSpeed = 6f;
        public string pressCaption = "철컥";

        public bool Pressed { get; private set; }

        Vector3 gateHome, plateHome;
        Collider2D[] gateCols;

        void Awake()
        {
            if (gate) { gateHome = gate.localPosition; gateCols = gate.GetComponentsInChildren<Collider2D>(); }
            if (plateVisual) plateHome = plateVisual.localPosition;
        }

        void Update()
        {
            bool was = Pressed;
            Pressed = PrincessOn() || BoxOn();
            if (Pressed && !was)
            {
                if (!string.IsNullOrEmpty(pressCaption)) GameEvents.Caption?.Invoke(pressCaption);
                SenseController.I?.EmitEcho(transform.position, 0.35f);
                GameEvents.PuzzleAttempt?.Invoke("plate", true);
            }
            if (plateVisual) plateVisual.localPosition = Vector3.MoveTowards(plateVisual.localPosition, plateHome + (Pressed ? Vector3.down * pressDepth : Vector3.zero), Time.deltaTime);
            if (gate)
            {
                var target = gateHome + (Pressed ? Vector3.up * gateRise : Vector3.zero);
                gate.localPosition = Vector3.MoveTowards(gate.localPosition, target, gateSpeed * Time.deltaTime);
            }
        }

        bool PrincessOn()
        {
            var duo = DuoController.I;
            if (!duo || !duo.princess) return false;
            var p = duo.princess.transform.position;
            return Mathf.Abs(p.x - transform.position.x) < width * 0.5f && p.y > transform.position.y - 0.3f && p.y < transform.position.y + 0.5f;
        }

        bool BoxOn()
        {
            var hits = Physics2D.OverlapBoxAll((Vector2)transform.position + Vector2.up * 0.3f, new Vector2(width * 0.9f, 0.4f), 0f);
            foreach (var h in hits) if (h && h.GetComponentInParent<PushBox>()) return true;
            return false;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.8f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.15f, new Vector3(width, 0.3f, 0));
            if (gate)
            {
                Gizmos.DrawLine(transform.position, gate.position);
                Gizmos.DrawLine(gate.position, gate.position + Vector3.up * gateRise);
            }
        }
#endif
    }
}

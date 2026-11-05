using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// A lever by the twilight gate. Only one is wired to the hidden gear.
    /// Drop the prefab anywhere; if <see cref="gate"/> is empty it uses the TwilightGate it sits under (or the nearest one).
    /// </summary>
    public class Lever : MonoBehaviour
    {
        public static Lever Nearest { get; private set; }

        [Tooltip("The lever the hidden gear is connected to. Exactly one per gate.")]
        public bool wired;
        [Tooltip("Empty = parent TwilightGate, else the nearest one in the scene.")]
        public TwilightGate gate;

        [Header("Look")]
        public SpriteRenderer sr;
        public Sprite upSprite, downSprite;
        public TextMesh prompt;
        public Color promptColor = new Color(1, 0.95f, 0.85f);
        [Tooltip("Seconds the handle stays down after a pull.")]
        public float pullShowTime = 0.5f;

        [Header("Reach")]
        [Tooltip("Half-width / half-height of the area the leader must stand in.")]
        public Vector2 reach = new Vector2(0.9f, 1.2f);

        float downUntil;

        void Awake()
        {
            if (!gate) gate = GetComponentInParent<TwilightGate>();
            if (!gate)
            {
                float best = float.MaxValue;
                foreach (var g in FindObjectsByType<TwilightGate>(FindObjectsSortMode.None))
                {
                    float d = (g.transform.position - transform.position).sqrMagnitude;
                    if (d < best) { best = d; gate = g; }
                }
            }
        }

        void Update()
        {
            var duo = DuoController.I;
            if (!duo || !gate) return;
            var p = duo.Leader.transform.position;
            bool near = Mathf.Abs(p.x - transform.position.x) < reach.x && Mathf.Abs(p.y - transform.position.y) < reach.y;
            if (near) Nearest = this; else if (Nearest == this) Nearest = null;
            if (prompt) { var c = promptColor; c.a = near && !gate.Open ? 1 : 0; prompt.color = c; }
            if (near && !GameFlow.InputLocked && GameInput.I.Interact.WasPressedThisFrame()) Trigger();
            if (sr) sr.sprite = (Time.time < downUntil || (gate.Open && wired)) ? downSprite : upSprite;
        }

        /// <summary>Pull (button or voice "당겨").</summary>
        public void Trigger()
        {
            if (!gate) return;
            downUntil = Time.time + pullShowTime;
            gate.Pull(this);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = wired ? new Color(1f, 0.85f, 0.35f, 0.9f) : new Color(0.7f, 0.7f, 0.8f, 0.5f);
            Gizmos.DrawWireCube(transform.position, new Vector3(reach.x * 2, reach.y * 2, 0));
        }
#endif
    }
}

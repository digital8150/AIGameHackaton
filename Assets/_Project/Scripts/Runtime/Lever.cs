using UnityEngine;

namespace DuskTwilight
{
    /// <summary>A lever by the twilight gate. Only one is wired to the hidden gear.</summary>
    public class Lever : MonoBehaviour
    {
        public static Lever Nearest { get; private set; }

        public bool wired;
        public TwilightGate gate;
        public SpriteRenderer sr;
        public Sprite upSprite, downSprite;
        public float reach = 0.9f;
        public TextMesh prompt;

        float downUntil;

        void Update()
        {
            var duo = DuoController.I;
            if (!duo) return;
            bool near = Mathf.Abs(duo.Leader.transform.position.x - transform.position.x) < reach
                        && Mathf.Abs(duo.Leader.transform.position.y - transform.position.y) < 1.2f;
            if (near) Nearest = this; else if (Nearest == this) Nearest = null;
            if (prompt) prompt.color = new Color(1, 0.95f, 0.85f, near && !gate.Open ? 1 : 0);
            if (near && !GameFlow.InputLocked && GameInput.I.Interact.WasPressedThisFrame()) Trigger();
            if (sr) sr.sprite = (Time.time < downUntil || (gate.Open && wired)) ? downSprite : upSprite;
        }

        /// <summary>Pull (button or voice "당겨").</summary>
        public void Trigger()
        {
            downUntil = Time.time + 0.5f;
            gate.Pull(this);
        }
    }
}

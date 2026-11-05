using UnityEngine;

namespace DuskTwilight
{
    /// <summary>Water, thorns… touching it sends the pair back to the last checkpoint (like falling off the level).</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Hazard : MonoBehaviour
    {
        [Tooltip("Caption shown when the leader falls in (what it looks like when you cannot hear the splash)")]
        public string caption = "풍덩";

        void Reset() { GetComponent<BoxCollider2D>().isTrigger = true; }
        void Awake() { GetComponent<BoxCollider2D>().isTrigger = true; }

        void OnTriggerEnter2D(Collider2D other)
        {
            var duo = DuoController.I;
            if (!duo || !GameFlow.I) return;
            if (other.attachedRigidbody != duo.Leader.GetComponent<Rigidbody2D>()) return;
            if (!string.IsNullOrEmpty(caption)) GameEvents.Caption?.Invoke(caption);
            SenseController.I?.EmitEcho(other.transform.position, 0.8f);
            GameFlow.I.Fall();
        }

        void OnDrawGizmos()
        {
            var c = GetComponent<BoxCollider2D>();
            if (!c) return;
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(c.offset, c.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}

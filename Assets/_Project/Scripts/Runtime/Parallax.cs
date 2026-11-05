using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// 2.5D layer scroll. factor 0 = moves with the world (play layer), 0..1 = farther away (follows the camera),
    /// negative = in front of the camera plane (slides past faster than the world — foreground).
    /// </summary>
    public class Parallax : MonoBehaviour
    {
        [Header("Depth")]
        [Tooltip("1 = infinitely far (sticks to camera), 0 = play layer, < 0 = foreground (moves faster than the world)")]
        [Range(-1.5f, 1f)] public float factor = 0.6f;
        [Tooltip("Same idea vertically. Foreground usually uses a small negative value.")]
        [Range(-1.5f, 1f)] public float yFactor = 0.85f;
        [Tooltip("Camera height at which this layer sits exactly where it was placed in the scene")]
        public float referenceY = 6f;
        [Tooltip("Camera x at which this layer sits exactly where it was placed in the scene")]
        public float referenceX = 0f;

        Vector3 origin;
        Transform cam;

        void Start()
        {
            origin = transform.position;
            cam = Camera.main ? Camera.main.transform : null;
        }

        void LateUpdate()
        {
            if (!cam) return;
            transform.position = new Vector3(
                origin.x + (cam.position.x - referenceX) * factor,
                origin.y + (cam.position.y - referenceY) * yFactor,
                origin.z);
        }

        void OnDrawGizmosSelected()
        {
            // where the layer will be when the camera is at x+10 — shows how fast it slides
            var sr = GetComponent<SpriteRenderer>();
            if (!sr) return;
            var b = sr.bounds;
            Gizmos.color = factor < 0 ? new Color(1f, 0.6f, 0.3f, 0.8f) : new Color(0.6f, 0.7f, 1f, 0.8f);
            Gizmos.DrawWireCube(b.center + Vector3.right * 10f * factor, b.size);
        }
    }
}

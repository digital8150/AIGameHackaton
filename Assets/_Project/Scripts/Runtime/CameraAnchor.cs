using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// Keeps an ambient effect (fireflies, drifting motes) centred on the camera. The particle system should simulate in
    /// world space: the emitter follows the view, but each mote stays where it was born, so walking through the level
    /// feels like moving through a swarm rather than dragging one along.
    /// </summary>
    public class CameraAnchor : MonoBehaviour
    {
        [Tooltip("Offset from the camera centre (world units)")]
        public Vector2 offset;

        Transform cam;

        void LateUpdate()
        {
            if (!cam) { var c = Camera.main; if (!c) return; cam = c.transform; }
            transform.position = new Vector3(cam.position.x + offset.x, cam.position.y + offset.y, transform.position.z);
        }
    }
}

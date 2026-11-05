using UnityEngine;

namespace DuskTwilight
{
    /// <summary>Horizontal parallax for backdrop strips.</summary>
    public class Parallax : MonoBehaviour
    {
        public float factor = 0.6f;
        public float yFactor = 0.85f;
        Vector3 origin;
        Transform cam;

        void Start() { origin = transform.position; cam = Camera.main.transform; }

        void LateUpdate()
        {
            if (!cam) return;
            transform.position = new Vector3(origin.x + cam.position.x * factor, origin.y + (cam.position.y - 6f) * yFactor, origin.z);
        }
    }
}

using UnityEngine;

namespace DuskTwilight
{
    /// <summary>Keeps the sky quad glued to the camera and covering the view.</summary>
    public class SkyFollow : MonoBehaviour
    {
        Camera cam;
        void Start() { cam = Camera.main; }
        void LateUpdate()
        {
            if (!cam) return;
            float h = cam.orthographicSize * 2f + 2f;
            transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 10f);
            transform.localScale = new Vector3(h * cam.aspect + 2f, h, 1f);
        }
    }
}

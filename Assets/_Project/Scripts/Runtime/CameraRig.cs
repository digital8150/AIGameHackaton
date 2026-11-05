using UnityEngine;

namespace DuskTwilight
{
    /// <summary>Smooth follow with look-ahead, level bounds and a small kick/shake. Pixel snapping is left to PixelPerfectCamera.</summary>
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig I { get; private set; }

        public Vector2 offset = new Vector2(0, 3.2f);
        public float lookAhead = 2.2f;
        public float smoothTime = 0.18f;
        public float minX = 7f, maxX = 100f, minY = 3.5f, maxY = 12f;

        Vector3 vel;
        float shake;
        float aheadX;

        void Awake() { I = this; }

        Vector3 Target()
        {
            var leader = DuoController.I ? DuoController.I.Leader : null;
            if (!leader) return transform.position;
            aheadX = Mathf.Lerp(aheadX, leader.Facing * lookAhead, Time.deltaTime * 2.2f);
            Vector2 p = (Vector2)leader.transform.position + offset + new Vector2(aheadX, 0);
            p.x = Mathf.Clamp(p.x, minX, maxX);
            p.y = Mathf.Clamp(p.y, minY, maxY);
            return new Vector3(p.x, p.y, transform.position.z);
        }

        public void Snap() { aheadX = 0; basePos = Target(); transform.position = basePos; vel = Vector3.zero; init = true; }

        public void Kick(float amount) { shake = Mathf.Max(shake, amount); }

        Vector3 basePos;
        bool init;

        void LateUpdate()
        {
            if (!init) { basePos = transform.position; init = true; }
            basePos = Vector3.SmoothDamp(basePos, Target(), ref vel, smoothTime);
            shake = Mathf.MoveTowards(shake, 0, Time.deltaTime * 1.2f);
            var jitter = shake > 0 ? new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0) * shake * 0.5f : Vector3.zero;
            transform.position = basePos + jitter;
        }
    }
}

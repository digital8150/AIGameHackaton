using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// Smooth follow with look-ahead, level bounds and a small kick/shake and a damped landing thump (<see cref="Impact"/>). Inside a <see cref="CameraZone"/> it eases
    /// to that zone's zoom and offset (e.g. pulled back and looking up on a climb). Pixel snapping is left to
    /// PixelPerfectCamera (off in this project).
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig I { get; private set; }

        public Vector2 offset = new Vector2(0, 3.2f);
        public float lookAhead = 2.2f;
        public float smoothTime = 0.18f;
        [Tooltip("Camera centre bounds at the base zoom (they shrink automatically when a zone zooms out)")]
        public float minX = 7f, maxX = 100f, minY = 3.5f, maxY = 12f;

        [Header("Zones")]
        [Tooltip("How fast zoom and zone offset ease (per second)")]
        public float zoneEase = 1.2f;

        [Header("Impact")]
        [Tooltip("Thump oscillation speed (Hz)")]
        public float impactFrequency = 4.5f;
        [Tooltip("How fast the thump dies out (per second; higher = shorter)")]
        public float impactDamping = 5f;
        [Tooltip("Largest downward dip (m) however big the hit")]
        public float impactMaxOffset = 1.2f;
        [Tooltip("Sideways wobble relative to the vertical dip")]
        [Range(0f, 0.5f)] public float impactSideways = 0.12f;
        [Tooltip("Zoom punch: orthographic size change (m) at a full-size hit (positive = pulls in)")]
        public float impactZoom = 0.35f;

        Vector3 vel;
        float shake;
        float impactAmp, impactT, impactSide;
        float aheadX;
        Camera cam;
        float baseSize, size;
        Vector2 zoneOffset;
        float riseUp;

        void Awake()
        {
            I = this;
            cam = GetComponent<Camera>();
            baseSize = size = cam ? cam.orthographicSize : 7.5f;
        }

        Vector3 Target()
        {
            var leader = DuoController.I ? DuoController.I.Leader : null;
            if (!leader) return transform.position;
            aheadX = Mathf.Lerp(aheadX, leader.Facing * lookAhead, Time.deltaTime * 2.2f);
            Vector2 p = (Vector2)leader.transform.position + offset + zoneOffset + new Vector2(aheadX, riseUp);
            // a wider view must stay inside the level just the same
            float dy = size - baseSize, dx = dy * (cam ? cam.aspect : 1.78f);
            p.x = Mathf.Clamp(p.x, minX + dx, Mathf.Max(minX + dx, maxX - dx));
            p.y = Mathf.Clamp(p.y, minY + dy, Mathf.Max(minY + dy, maxY - dy));
            return new Vector3(p.x, p.y, transform.position.z);
        }

        void UpdateZone(bool instant)
        {
            var leader = DuoController.I ? DuoController.I.Leader : null;
            var zone = leader ? CameraZone.At(leader.transform.position) : null;
            float targetSize = zone ? zone.orthoSize : baseSize;
            Vector2 targetOffset = zone ? zone.extraOffset : Vector2.zero;
            float rising = 0f;
            if (zone && leader)
            {
                var rb = leader.GetComponent<Rigidbody2D>();
                if (rb && rb.linearVelocity.y > 1f) rising = zone.riseLookUp;
            }
            float k = instant ? 1f : 1f - Mathf.Exp(-zoneEase * Time.deltaTime);
            size = Mathf.Lerp(size, targetSize, k);
            zoneOffset = Vector2.Lerp(zoneOffset, targetOffset, k);
            riseUp = Mathf.Lerp(riseUp, rising, instant ? 1f : 1f - Mathf.Exp(-zoneEase * 0.6f * Time.deltaTime));
            if (cam) cam.orthographicSize = size - ImpactValue() / Mathf.Max(0.01f, impactMaxOffset) * impactZoom;
        }

        // damped oscillation: starts at the full dip (cos), rings back and forth while it fades
        float ImpactValue() => impactAmp > 0.001f ? impactAmp * Mathf.Exp(-impactDamping * impactT) * Mathf.Cos(impactT * impactFrequency * Mathf.PI * 2f) : 0f;

        /// <summary>A downward thump (a heavy landing): the view dips, springs back and settles. amount = dip in metres, capped.</summary>
        public void Impact(float amount)
        {
            float a = Mathf.Min(Mathf.Abs(amount), impactMaxOffset);
            if (a < ImpactValue()) return;
            impactAmp = a; impactT = 0f;
            impactSide = (Random.value < 0.5f ? -1f : 1f) * impactSideways;
        }

        public void Snap() { UpdateZone(true); aheadX = 0; basePos = Target(); transform.position = basePos; vel = Vector3.zero; init = true; }

        public void Kick(float amount) { shake = Mathf.Max(shake, amount); }

        Vector3 basePos;
        bool init;

        void LateUpdate()
        {
            if (!init) { basePos = transform.position; init = true; }
            UpdateZone(false);
            basePos = Vector3.SmoothDamp(basePos, Target(), ref vel, smoothTime);
            shake = Mathf.MoveTowards(shake, 0, Time.deltaTime * 1.2f);
            var jitter = shake > 0 ? new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0) * shake * 0.5f : Vector3.zero;
            float imp = 0f;
            if (impactAmp > 0.001f)
            {
                impactT += Time.deltaTime;
                imp = ImpactValue();
                if (impactT * impactDamping > 6f) impactAmp = 0f;
            }
            transform.position = basePos + jitter + new Vector3(imp * impactSide, -imp, 0f);
        }
    }
}

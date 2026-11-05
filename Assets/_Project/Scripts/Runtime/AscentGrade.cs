using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DuskTwilight
{
    /// <summary>
    /// 오를수록 황혼으로. The higher the camera climbs (between <see cref="fromY"/> and <see cref="toY"/>), the more the
    /// dark forest gives way to golden twilight: the global 2D light warms and brightens, a warm grading volume fades
    /// in, the top backdrop fades over the bottom one. Reaching the top should feel like arriving.
    /// </summary>
    public class AscentGrade : MonoBehaviour
    {
        [Header("Height (world Y of the leader's feet)")]
        public float fromY = 8f;
        public float toY = 29f;
        [Tooltip("0..1 height → 0..1 warmth (ease in: stays dusky low down, opens up near the top)")]
        public AnimationCurve curve = new AnimationCurve(new Keyframe(0, 0, 0, 0.3f), new Keyframe(0.55f, 0.32f, 0.9f, 0.9f), new Keyframe(1, 1, 2f, 0));
        public float smoothing = 0.6f;

        [Header("Global light (empty = the scene's global Light2D)")]
        public Light2D globalLight;
        public Color topLightColor = new Color(1f, 0.82f, 0.58f);
        public float topIntensity = 0.85f;

        [Header("Grading")]
        [Tooltip("Warm volume whose weight follows the climb")]
        public Volume topVolume;

        [Header("Backdrops")]
        [Tooltip("Fade in with height (the golden twilight forest)")]
        public SpriteRenderer[] fadeIn;
        [Tooltip("Fade out with height (the dark forest)")]
        public SpriteRenderer[] fadeOut;

        /// <summary>Current 0..1 (for other effects).</summary>
        public static float Amount { get; private set; }

        Color baseColor; float baseIntensity; float t;
        Camera cam;

        void Start()
        {
            cam = Camera.main;
            if (!globalLight)
                foreach (var l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
                    if (l.lightType == Light2D.LightType.Global) { globalLight = l; break; }
            if (globalLight) { baseColor = globalLight.color; baseIntensity = globalLight.intensity; }
            t = Target();
            Apply();
        }

        float Target()
        {
            // the leader, not the camera: the camera stops at the roof long before the summit
            var duo = DuoController.I;
            float y = duo && duo.Leader ? duo.Leader.transform.position.y : cam ? cam.transform.position.y : 0f;
            return Mathf.Clamp01(curve.Evaluate(Mathf.InverseLerp(fromY, toY, y)));
        }

        void LateUpdate()
        {
            t = Mathf.MoveTowards(t, Target(), Time.deltaTime * smoothing);
            Apply();
        }

        void Apply()
        {
            Amount = t;
            if (globalLight)
            {
                globalLight.color = Color.Lerp(baseColor, topLightColor, t);
                globalLight.intensity = Mathf.Lerp(baseIntensity, topIntensity, t);
            }
            if (topVolume) topVolume.weight = t;
            if (fadeIn != null) foreach (var r in fadeIn) if (r) { var c = r.color; c.a = t; r.color = c; }
            if (fadeOut != null) foreach (var r in fadeOut) if (r) { var c = r.color; c.a = 1f - t; r.color = c; }
        }

        void OnDisable()
        {
            if (globalLight) { globalLight.color = baseColor; globalLight.intensity = baseIntensity; }
            Amount = 0f;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.4f, 0.8f, 0.6f);
            Gizmos.DrawLine(new Vector3(-10, fromY), new Vector3(120, fromY));
            Gizmos.color = new Color(1f, 0.8f, 0.4f, 0.8f);
            Gizmos.DrawLine(new Vector3(-10, toY), new Vector3(120, toY));
        }
#endif
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// One layer of the title screen: pointer parallax, slow breathing sway/zoom, and a shader reveal
    /// (animates a float property such as _Reveal on its own material instance). TitleScreen starts the reveal.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class TitleLayer : MonoBehaviour
    {
        [Header("Parallax")]
        [Tooltip("Pixels the layer moves at the screen edge (deeper layers move less).")]
        public Vector2 parallax = new Vector2(12, 6);
        [Range(0.5f, 20f)] public float parallaxEase = 3f;

        [Header("Breathing")]
        [Tooltip("Sway in pixels (x, y).")]
        public Vector2 sway = new Vector2(0, 4);
        [Range(0.01f, 2f)] public float swaySpeed = 0.35f;
        [Range(0f, 0.05f)] public float breatheScale = 0.006f;
        [Tooltip("Phase offset so layers don't move in lockstep.")]
        public float phase;

        [Header("Intro")]
        [Tooltip("Start scale, eased to 1 during the reveal (slow push-in / pull-out).")]
        public float introScale = 1f;
        [Tooltip("Start offset in pixels, eased to 0 during the reveal.")]
        public Vector2 introOffset;

        [Header("Shader reveal")]
        [Tooltip("Graphic whose material has the reveal property. Empty = no shader reveal.")]
        public Graphic revealGraphic;
        public string revealProperty = "_Reveal";
        public AnimationCurve revealCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Tooltip("Also fade a CanvasGroup on this object with the reveal.")]
        public bool fadeCanvasGroup;

        RectTransform rt;
        Vector2 basePos;
        Vector2 par;
        Material mat;
        CanvasGroup group;
        int revealId;
        float revealT = 1f, revealDur = 1f, revealDelay;
        bool revealing;

        public bool Revealed => !revealing && revealT >= 1f;

        void Awake()
        {
            rt = (RectTransform)transform;
            basePos = rt.anchoredPosition;
            revealId = Shader.PropertyToID(revealProperty);
            group = GetComponent<CanvasGroup>();
            if (revealGraphic && revealGraphic.material)
            {
                mat = new Material(revealGraphic.material);
                revealGraphic.material = mat;
            }
        }

        /// <summary>Hide now; call Reveal to bring it in.</summary>
        public void Hide()
        {
            revealing = false;
            revealT = 0f;
            Apply(0f);
        }

        public void Reveal(float duration, float delay = 0f)
        {
            revealDur = Mathf.Max(0.01f, duration);
            revealDelay = delay;
            revealT = 0f;
            revealing = true;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (revealing)
            {
                if (revealDelay > 0f) revealDelay -= dt;
                else
                {
                    revealT = Mathf.Min(1f, revealT + dt / revealDur);
                    if (revealT >= 1f) revealing = false;
                }
            }
            float k = revealCurve.Evaluate(revealT);
            Apply(k);

            Vector2 target = Vector2.zero;
            var mouse = Mouse.current;
            if (mouse != null && Screen.width > 0)
            {
                Vector2 m = mouse.position.ReadValue();
                Vector2 n = new Vector2(m.x / Screen.width - 0.5f, m.y / Screen.height - 0.5f) * 2f;
                n = Vector2.ClampMagnitude(n, 1.5f);
                target = new Vector2(-n.x * parallax.x, -n.y * parallax.y);
            }
            par = Vector2.Lerp(par, target, 1f - Mathf.Exp(-parallaxEase * dt));

            float t = Time.unscaledTime * swaySpeed * Mathf.PI * 2f + phase;
            Vector2 s = new Vector2(Mathf.Sin(t * 0.7f) * sway.x, Mathf.Sin(t) * sway.y);
            Vector2 intro = introOffset * (1f - k);
            rt.anchoredPosition = basePos + par + s + intro;
            float scale = Mathf.Lerp(introScale, 1f, k) * (1f + Mathf.Sin(t * 0.8f) * breatheScale);
            rt.localScale = new Vector3(Mathf.Sign(rt.localScale.x) * scale, scale, 1f);
        }

        void Apply(float k)
        {
            if (mat) mat.SetFloat(revealId, k);
            if (fadeCanvasGroup && group) group.alpha = k;
        }

        void OnDestroy()
        {
            if (mat) Destroy(mat);
        }
    }
}

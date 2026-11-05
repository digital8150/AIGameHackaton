using UnityEngine;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// Opens a storybook spread (settings, pause) like a real book, faked in 2D:
    /// the closed book rises into the middle of the screen, the leather cover swings open around the spine in
    /// perspective (<see cref="BookFlipGraphic"/> front → inside face), the spread slides to centre, and the left page
    /// fills in once the cover lies flat. <see cref="Set"/>(0 → 1) is driven by the owning panel; closing plays it back.
    /// Sits on the spread (the paper Image, TitlePaper with _Spread on); layout lives in the prefab.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class StorybookBook : MonoBehaviour
    {
        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly int HideLeftId = Shader.PropertyToID("_HideLeft");
        static readonly int PadId = Shader.PropertyToID("_Pad");

        [Tooltip("The spread's paper (TitlePaper with _Spread). Empty = Image on this object.")]
        public Graphic paper;
        [Tooltip("Leather front of the closed cover (TitleBookCover).")]
        public BookFlipGraphic coverFront;
        [Tooltip("Inside of the cover = the left page (TitlePaper, uvRect 0..0.5, mirrored).")]
        public BookFlipGraphic coverBack;
        public CanvasGroup leftPage;
        public CanvasGroup rightPage;
        [Tooltip("Gilt title on the closed cover, fades as the cover lifts.")]
        public Graphic coverTitle;

        [Header("Timeline (share of Set 0 → 1)")]
        [Range(0.05f, 0.5f)] public float appearEnd = 0.22f;
        [Range(0.5f, 0.98f)] public float flipEnd = 0.82f;

        [Header("Appear")]
        public Vector2 appearOffset = new Vector2(0f, -50f);
        [Range(0.5f, 1f)] public float appearScale = 0.86f;
        [Tooltip("Closed book is centred on screen; the spread centres as it opens.")]
        public bool centreClosedBook = true;

        Vector2 restPos;
        Material paperMat, backMat;
        bool ready;

        void Awake() => Init();

        void Init()
        {
            if (ready) return;
            ready = true;
            if (!paper) paper = GetComponent<Graphic>();
            restPos = ((RectTransform)transform).anchoredPosition;
            if (coverBack)
            {
                // own copy: it must keep the full spread size, not the cover rect's
                backMat = new Material(coverBack.material) { name = coverBack.material.name + " (Cover Back)" };
                backMat.SetFloat(HideLeftId, 0f);
                coverBack.material = backMat;
            }
        }

        public void Set(float k)
        {
            Init();
            var rt = (RectTransform)transform;
            var size = rt.rect.size;
            if (!paperMat && paper)
            {
                var sz = paper.GetComponent<UIShaderSize>();
                paperMat = sz ? sz.Material : paper.material;
            }
            if (backMat) backMat.SetVector(SizeId, new Vector4(size.x, size.y, 0, 0));

            float a = Smooth(Mathf.Clamp01(k / appearEnd));
            float f = Smooth(Mathf.Clamp01((k - appearEnd) / (flipEnd - appearEnd)));
            float l = Mathf.Clamp01((k - flipEnd) / (1f - flipEnd));
            float angle = 180f * f;

            float pad = paperMat ? paperMat.GetFloat(PadId) : 0f;
            float half = (size.x * 0.5f - pad) * 0.5f;
            float shift = centreClosedBook ? Mathf.Lerp(-half, 0f, f) : 0f;
            rt.anchoredPosition = restPos + appearOffset * (1f - a) + new Vector2(shift, 0f);
            rt.localScale = Vector3.one * Mathf.Lerp(appearScale, 1f, a);

            bool open = l > 0f;
            if (paperMat) paperMat.SetFloat(HideLeftId, open ? 0f : 1f);
            if (coverFront)
            {
                coverFront.SetAngle(angle);
                coverFront.enabled = angle < 90f;
            }
            if (coverBack)
            {
                coverBack.SetAngle(angle);
                coverBack.enabled = angle >= 90f && !open;
            }
            if (leftPage) leftPage.alpha = l;
            if (rightPage) rightPage.alpha = 1f;   // under the closed cover; revealed as it lifts
            if (coverTitle) coverTitle.canvasRenderer.SetAlpha(1f - Mathf.Clamp01(angle / 18f));
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        void OnDestroy()
        {
            if (backMat) Destroy(backMat);
        }
    }
}

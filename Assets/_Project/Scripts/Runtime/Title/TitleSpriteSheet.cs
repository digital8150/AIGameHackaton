using UnityEngine;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// Plays a packed sprite sheet on a RawImage by stepping its uvRect (no sliced sprite assets needed),
    /// and tells the TitlePortrait shader which frame is showing (_FrameRect) so its fades, burn reveal
    /// and rim light work per frame. Sheet layout: frames left→right, top→bottom, each frameSize px with
    /// a transparent gutter on every side.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(RawImage))]
    public class TitleSpriteSheet : MonoBehaviour, IMaterialModifier
    {
        static readonly int FrameRectId = Shader.PropertyToID("_FrameRect");

        [Header("Sheet layout (px)")]
        [Min(1)] public int columns = 12;
        [Min(1)] public int frameCount = 144;
        public Vector2Int frameSize = new Vector2Int(400, 540);
        [Min(0)] public int gutter = 2;

        [Header("Playback")]
        [Range(1f, 60f)] public float fps = 24f;
        [Tooltip("Frame shown in edit mode and at start.")]
        [Min(0)] public int startFrame;
        [Tooltip("Play forward then backward (for loops whose last frame doesn't meet the first).")]
        public bool pingPong;

        [Header("Material")]
        [Tooltip("Render through a private copy of the material that carries the current _FrameRect (edit and play mode) — "
               + "for sheets without a TitleLayer instance, e.g. the loading-screen duo with edge fades.")]
        public bool ownFrameMaterial;

        RawImage image;
        int shown = -1;
        Material shownOn;
        Material frameMat, frameMatBase;
        Vector4 frameRect = new Vector4(0, 0, 1, 1);

        void OnEnable()
        {
            image = GetComponent<RawImage>();
            shown = -1;
            Show(startFrame);
            if (ownFrameMaterial) image.SetMaterialDirty();
        }

        void OnDisable()
        {
            if (image) image.SetMaterialDirty();
        }

        void OnDestroy()
        {
            if (frameMat) DestroyImmediate(frameMat);
        }

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!ownFrameMaterial || !isActiveAndEnabled || !baseMaterial || !baseMaterial.HasProperty(FrameRectId)) return baseMaterial;
            if (!frameMat || frameMatBase != baseMaterial)
            {
                if (frameMat) DestroyImmediate(frameMat);
                frameMat = new Material(baseMaterial) { name = baseMaterial.name + " (Frame)", hideFlags = HideFlags.HideAndDontSave };
                frameMatBase = baseMaterial;
            }
            else frameMat.CopyPropertiesFromMaterial(baseMaterial);   // keep inspector tweaks live
            frameMat.SetVector(FrameRectId, frameRect);
            return frameMat;
        }

        void Update()
        {
            if (!Application.isPlaying) { Show(startFrame); return; }
            int n = Mathf.Max(1, frameCount);
            int step = Mathf.FloorToInt(Time.unscaledTime * fps);
            int f = pingPong && n > 1 ? PingPong(step, n) : step % n;
            Show((f + startFrame) % n);
        }

        static int PingPong(int step, int n)
        {
            int period = 2 * (n - 1);
            int k = step % period;
            return k < n ? k : period - k;
        }

        void Show(int frame)
        {
            if (!image || !image.texture) return;
            // TitleLayer swaps in a material instance in Awake; only ever write to that instance during play
            // (never to the shared asset), and re-push when the live material changes
            var mat = Application.isPlaying && image.material != image.defaultMaterial ? image.material : null;
            if (frame == shown && mat == shownOn) return;
            shown = frame;
            shownOn = mat;

            float w = image.texture.width, h = image.texture.height;
            int col = frame % columns, row = frame / columns;
            float cw = frameSize.x + 2 * gutter, ch = frameSize.y + 2 * gutter;
            float x = (col * cw + gutter) / w;
            float y = 1f - (row * ch + gutter + frameSize.y) / h;   // texture v runs bottom→top
            var r = new Rect(x, y, frameSize.x / w, frameSize.y / h);
            image.uvRect = r;
            frameRect = new Vector4(r.x, r.y, r.width, r.height);
            if (ownFrameMaterial) { if (frameMat) frameMat.SetVector(FrameRectId, frameRect); else image.SetMaterialDirty(); return; }
            if (mat && mat.name.EndsWith("(Instance)")) mat.SetVector(FrameRectId, new Vector4(r.x, r.y, r.width, r.height));
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// One face of a book cover swinging around the spine, drawn as a perspective-foreshortened strip mesh so a flat
    /// UI shader reads as a turning board. The rect is the closed cover: pivot on the spine (left edge, middle).
    /// <see cref="angle"/> 0 = lying closed to the right, 180 = lying open to the left.
    /// <see cref="mirror"/> for the inside face (seen from behind once past 90°).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class BookFlipGraphic : MaskableGraphic
    {
        [Range(0f, 180f)] public float angle;
        [Tooltip("Virtual camera distance in px — smaller = stronger perspective.")]
        [Range(400f, 6000f)] public float focal = 1900f;
        [Range(2, 48)] public int segments = 20;
        [Tooltip("Part of the material's UV space this face shows.")]
        public Rect uvRect = new Rect(0, 0, 1, 1);
        [Tooltip("Inside face: UVs run from the spine outward in the other direction.")]
        public bool mirror;
        [Tooltip("Light falloff as the board turns edge-on (0 = none).")]
        [Range(0f, 1f)] public float shading = 0.45f;

        public void SetAngle(float a)
        {
            if (Mathf.Approximately(a, angle)) return;
            angle = a;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            float rad = angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            // light: full when lying flat, dimmer when standing edge-on
            float shade = 1f - shading * Mathf.Abs(sin);
            var col = (Color32)(color * new Color(shade, shade, shade, 1f));

            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments;
                float x0 = r.xMin + u * r.width;          // distance from the spine along the board
                float z = x0 * sin;                        // lifted towards the viewer
                float s = focal / Mathf.Max(focal - z, 1f);
                float x = x0 * cos * s;
                float uvx = mirror ? uvRect.xMax - u * uvRect.width : uvRect.xMin + u * uvRect.width;
                vh.AddVert(new Vector3(x, r.yMin * s), col, new Vector2(uvx, uvRect.yMin));
                vh.AddVert(new Vector3(x, r.yMax * s), col, new Vector2(uvx, uvRect.yMax));
                if (i > 0)
                {
                    int b = i * 2;
                    vh.AddTriangle(b - 2, b - 1, b + 1);
                    vh.AddTriangle(b - 2, b + 1, b);
                }
            }
        }
    }
}

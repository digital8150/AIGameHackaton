using UnityEngine;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// Feeds the TitleLogo shader its text rect (for the gilt gradient / light sweep) and plays the ink reveal.
    /// Each Text line under the logo gets its own material instance.
    /// </summary>
    public class TitleLogo : MonoBehaviour
    {
        static readonly int RectId = Shader.PropertyToID("_Rect");
        static readonly int RevealId = Shader.PropertyToID("_Reveal");

        [Tooltip("Lines of the logo, revealed in order.")]
        public Text[] lines;
        [Tooltip("Extra graphics (dividers, glow) faded in with the last line.")]
        public CanvasGroup ornaments;
        [Range(0.1f, 5f)] public float lineDuration = 1.4f;
        [Tooltip("Delay between lines starting.")]
        [Range(0f, 3f)] public float lineStagger = 0.45f;

        Material[] mats;
        readonly Vector3[] corners = new Vector3[4];
        float t = -1f;

        void Awake()
        {
            mats = new Material[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i] || !lines[i].material) continue;
                mats[i] = new Material(lines[i].material);
                lines[i].material = mats[i];
            }
        }

        public void Hide()
        {
            t = -1f;
            Push(0f);
        }

        public void Reveal() { if (t < 0f) t = 0f; }

        public float Duration => lineDuration + lineStagger * Mathf.Max(0, lines.Length - 1);

        void Update()
        {
            if (t >= 0f)
            {
                t += Time.unscaledDeltaTime;
                Push(t);
            }
        }

        void Push(float time)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (!mats[i]) continue;
                // uGUI batches vertices into root-canvas space, so the rect goes there too
                lines[i].rectTransform.GetWorldCorners(corners);
                var root = lines[i].canvas ? lines[i].canvas.rootCanvas.transform : null;
                Vector3 a = root ? root.InverseTransformPoint(corners[0]) : corners[0];
                Vector3 b = root ? root.InverseTransformPoint(corners[2]) : corners[2];
                mats[i].SetVector(RectId, new Vector4(a.x, b.x, a.y, b.y));
                float k = Mathf.Clamp01((time - i * lineStagger) / lineDuration);
                mats[i].SetFloat(RevealId, Mathf.SmoothStep(0f, 1f, k));
            }
            if (ornaments) ornaments.alpha = Mathf.Clamp01((time - Duration * 0.6f) / (lineDuration * 0.6f));
        }

        void OnDestroy()
        {
            if (mats == null) return;
            foreach (var m in mats) if (m) Destroy(m);
        }
    }
}

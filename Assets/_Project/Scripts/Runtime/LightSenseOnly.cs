using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// An effect only the princess sees (fireflies): fades out while the prince leads — he can't see light — and back in
    /// when she does, over about the length of the sense switch. Works on particle renderers and sprites alike by
    /// tinting their colour alpha through a property block.
    /// </summary>
    public class LightSenseOnly : MonoBehaviour
    {
        [Tooltip("Seconds to fade (≈ SenseController.switchDuration)")]
        public float fadeTime = 0.8f;

        Renderer[] renderers;
        MaterialPropertyBlock block;
        float alpha = 1f;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Color = Shader.PropertyToID("_Color");

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
            var sc = SenseController.I;
            alpha = sc && sc.IsDark ? 0f : 1f;
            Apply();
        }

        void Update()
        {
            var sc = SenseController.I;
            float target = sc && sc.IsDark ? 0f : 1f;
            if (Mathf.Approximately(alpha, target)) return;
            alpha = Mathf.MoveTowards(alpha, target, Time.deltaTime / Mathf.Max(0.05f, fadeTime));
            Apply();
        }

        void Apply()
        {
            var c = new Color(1f, 1f, 1f, alpha);
            foreach (var r in renderers)
            {
                if (!r) continue;
                r.enabled = alpha > 0.001f;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColor, c);
                block.SetColor(Color, c);
                r.SetPropertyBlock(block);
            }
        }
    }
}

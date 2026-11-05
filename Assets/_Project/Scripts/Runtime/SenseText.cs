using UnityEngine;

namespace DuskTwilight
{
    /// <summary>World text that exists for one sense: e.g. a word carved in light only the princess can read.</summary>
    [RequireComponent(typeof(TextMesh))]
    public class SenseText : MonoBehaviour
    {
        [Range(0, 1)] public float lightAlpha = 1f;
        [Range(0, 1)] public float darkAlpha = 0f;
        public float shimmer = 0.15f;

        TextMesh tm;
        Color baseColor;

        void Awake()
        {
            tm = GetComponent<TextMesh>();
            baseColor = tm.color;
            baseColor.a = 1f;
        }

        public void SetBaseColor(Color c) { baseColor = c; baseColor.a = 1f; }

        void LateUpdate()
        {
            float dark = SenseController.I ? SenseController.I.SenseAt(transform.position) : 0f;
            float a = Mathf.Lerp(lightAlpha, darkAlpha, dark);
            a *= 1f - shimmer + shimmer * Mathf.Sin(Time.time * 3f + transform.position.x);
            var c = baseColor; c.a = a;
            tm.color = c;
        }
    }
}

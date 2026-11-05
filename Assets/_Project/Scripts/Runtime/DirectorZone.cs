using UnityEngine;

namespace DuskTwilight
{
    /// <summary>Which sense holds a zone's clue — what the director suspects the player is missing.</summary>
    public enum ClueSense { Sound, Visual, Timing }

    /// <summary>
    /// Marks the stretch of level the director reasons about. The range is drawn in the Scene view
    /// (select it to see the label); drag minX/maxX or use "Fit To Children" from the component menu.
    /// </summary>
    public class DirectorZone : MonoBehaviour
    {
        public string zoneId;
        [Tooltip("Which sense holds this zone's clue: Sound (prince hears it) | Visual (princess sees it) | Timing (when to act)")]
        public ClueSense clue = ClueSense.Sound;
        [Tooltip("World X range of the zone (left edge, right edge).")]
        public float minX, maxX;

        [Header("Hint (empty = director default for the clue)")]
        [Tooltip("Whispered when the director decides this zone's clue is being missed, or when the player asks for a hint.")]
        [TextArea] public string hintOverride;

        /// <summary>Lower-case name sent to Jev (sound | visual | timing).</summary>
        public string ClueId => clue.ToString().ToLowerInvariant();

        public bool Contains(float x) => x >= minX && x <= maxX;

        [ContextMenu("Fit To Children")]
        void FitToChildren()
        {
            var rs = GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            minX = b.min.x; maxX = b.max.x;
        }

        void OnValidate() { if (maxX < minX) maxX = minX; }

#if UNITY_EDITOR
        static readonly Color[] Colors = { new Color(0.55f, 0.75f, 1f), new Color(1f, 0.8f, 0.35f), new Color(1f, 0.45f, 0.6f) };

        void OnDrawGizmos() => Draw(0.06f);
        void OnDrawGizmosSelected()
        {
            Draw(0.16f);
            UnityEditor.Handles.Label(new Vector3(minX + 0.2f, transform.position.y + 5.5f, 0), $"{zoneId} · {clue}");
        }

        void Draw(float fill)
        {
            var c = Colors[(int)clue];
            float y = transform.position.y, h = 12f;
            var center = new Vector3((minX + maxX) * 0.5f, y + h * 0.5f - 4f, 0);
            var size = new Vector3(maxX - minX, h, 0);
            Gizmos.color = new Color(c.r, c.g, c.b, fill);
            Gizmos.DrawCube(center, size);
            Gizmos.color = c;
            Gizmos.DrawWireCube(center, size);
        }
#endif
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// A rectangle of the level where the camera frames things differently: zoomed out to show a climb, looking up
    /// toward the next foothold, or pulled back at the summit. While the leader is inside, <see cref="CameraRig"/>
    /// eases to this zone's size and offset (the highest priority zone wins; outside every zone it eases back).
    /// The rectangle is this object's position (bottom-left) + <see cref="size"/>.
    /// </summary>
    public class CameraZone : MonoBehaviour
    {
        public static readonly List<CameraZone> All = new List<CameraZone>();

        public Vector2 size = new Vector2(20f, 20f);
        [Tooltip("Camera orthographic size inside (half the visible height)")]
        public float orthoSize = 8.5f;
        [Tooltip("Extra camera offset inside (y > 0 = look up at what's coming)")]
        public Vector2 extraOffset = new Vector2(0f, 1.5f);
        [Tooltip("Extra look-up while the leader is rising (jumping up a climb)")]
        public float riseLookUp = 1f;
        public int priority;

        public Rect Area => new Rect((Vector2)transform.position, size);

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public static CameraZone At(Vector2 p)
        {
            CameraZone best = null;
            foreach (var z in All)
                if (z.Area.Contains(p) && (!best || z.priority > best.priority)) best = z;
            return best;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var r = Area;
            Gizmos.color = new Color(0.5f, 0.9f, 1f, 0.6f);
            Gizmos.DrawWireCube(r.center, r.size);
            UnityEditor.Handles.Label(r.position + Vector2.up * r.height, $"{name} ×{orthoSize:0.#}");
        }
#endif
    }
}

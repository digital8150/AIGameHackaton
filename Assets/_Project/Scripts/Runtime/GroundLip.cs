using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace DuskTwilight
{
    /// <summary>
    /// Lays an illustrated turf strip along every exposed top edge of a Tilemap (the tilemap keeps the collision).
    /// The strips are rebuilt from the tile data, so editing the level moves the turf with it. They are not saved
    /// in the scene (DontSave) — only this component and its settings are.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Tilemap))]
    public class GroundLip : MonoBehaviour
    {
        [Header("Look")]
        [Tooltip("Horizontally tileable turf strip. Its pivot height marks where the walkable surface is.")]
        public Sprite lipSprite;
        [Tooltip("SenseSprite material for the strip (one texture per material)")]
        public Material material;
        public int sortingOrder = 5;

        [Header("Placement")]
        [Tooltip("Extra length past each end of a run so the turf wraps over the corner")]
        [Range(0f, 1f)] public float overhang = 0.25f;
        [Tooltip("Shift the strip up (+) or down (-) relative to the tile top")]
        [Range(-1f, 1f)] public float yOffset = 0f;
        [Tooltip("Runs shorter than this many tiles get no turf")]
        [Min(1)] public int minRun = 1;

        Tilemap map;
        readonly List<GameObject> strips = new List<GameObject>();
        readonly List<(Vector3 from, Vector3 to)> runs = new List<(Vector3, Vector3)>();
        bool dirty;

        void OnEnable()
        {
            map = GetComponent<Tilemap>();
            Tilemap.tilemapTileChanged += OnTilesChanged;
            Rebuild();
        }

        void OnDisable()
        {
            Tilemap.tilemapTileChanged -= OnTilesChanged;
            Clear();
        }

        void OnValidate() => dirty = true;

        void OnTilesChanged(Tilemap changed, Tilemap.SyncTile[] tiles)
        {
            if (changed == map) dirty = true;
        }

        void Update()
        {
            if (!dirty) return;
            dirty = false;
            Rebuild();
        }

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            Clear();
            if (!map || !lipSprite) return;
            map.CompressBounds();
            var b = map.cellBounds;
            for (int y = b.yMin; y < b.yMax; y++)
            {
                int start = int.MinValue;
                for (int x = b.xMin; x <= b.xMax; x++)
                {
                    bool top = x < b.xMax && map.HasTile(new Vector3Int(x, y, 0)) && !map.HasTile(new Vector3Int(x, y + 1, 0));
                    if (top && start == int.MinValue) start = x;
                    if (!top && start != int.MinValue)
                    {
                        if (x - start >= minRun) AddStrip(start, x, y);
                        start = int.MinValue;
                    }
                }
            }
        }

        void AddStrip(int x0, int x1, int y)
        {
            Vector3 left = map.CellToWorld(new Vector3Int(x0, y + 1, 0));
            Vector3 right = map.CellToWorld(new Vector3Int(x1, y + 1, 0));
            runs.Add((left, right));

            var go = new GameObject("Lip " + x0 + ".." + x1 + " @" + y) { hideFlags = HideFlags.DontSave | HideFlags.NotEditable };
            go.transform.SetParent(transform, false);
            float width = right.x - left.x + overhang * 2f;
            go.transform.position = new Vector3((left.x + right.x) * 0.5f, left.y + yOffset, transform.position.z);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = lipSprite;
            if (material) sr.sharedMaterial = material;
            sr.sortingOrder = sortingOrder;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2(width, lipSprite.bounds.size.y);
            strips.Add(go);
        }

        void Clear()
        {
            foreach (var s in strips)
                if (s) { if (Application.isPlaying) Destroy(s); else DestroyImmediate(s); }
            strips.Clear();
            runs.Clear();
            // strays from a domain reload (DontSave children are not tracked across reloads)
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i).gameObject;
                if (c.name.StartsWith("Lip ") && (c.hideFlags & HideFlags.DontSave) != 0)
                { if (Application.isPlaying) Destroy(c); else DestroyImmediate(c); }
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 1f, 0.5f, 0.9f);
            foreach (var r in runs)
                Gizmos.DrawLine(r.from + Vector3.up * yOffset, r.to + Vector3.up * yOffset);
        }
    }
}

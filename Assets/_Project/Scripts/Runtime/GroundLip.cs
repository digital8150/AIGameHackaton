using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace DuskTwilight
{
    /// <summary>
    /// Dresses every exposed edge of a Tilemap with illustrated strips (the tilemap keeps the collision):
    /// turf along top faces, a rock rim along side faces, hanging roots under ceilings.
    /// Strips are rebuilt from the tile data, so editing the level moves them with it. They are not saved
    /// in the scene (DontSave) — only this component and its settings are.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Tilemap))]
    public class GroundLip : MonoBehaviour
    {
        [Header("Top (turf)")]
        [Tooltip("Horizontally tileable turf strip. Its pivot height marks where the walkable surface is.")]
        public Sprite lipSprite;
        [Tooltip("SenseSprite material for the strip (one texture per material)")]
        public Material material;
        public int sortingOrder = 5;
        [Tooltip("Extra length past each end of a run so the turf wraps over the corner")]
        [Range(0f, 1f)] public float overhang = 0.25f;
        [Tooltip("Shift the strip up (+) or down (-) relative to the tile top")]
        [Range(-1f, 1f)] public float yOffset = 0f;
        [Tooltip("Runs shorter than this many tiles get no strip")]
        [Min(1)] public int minRun = 1;

        [Header("Sides (rock rim)")]
        [Tooltip("Vertically tileable rim strip whose exposed edge faces RIGHT; its pivot x marks the face. Empty = no side strips.")]
        public Sprite sideSprite;
        public Material sideMaterial;
        public int sideSortingOrder = 4;
        [Tooltip("Shorten side strips at the top so they tuck under the turf")]
        [Range(0f, 1f)] public float sideTopInset = 0.3f;

        [Header("Ceiling (hanging roots)")]
        [Tooltip("Horizontally tileable strip hanging below a ceiling; its pivot height marks the ceiling line. Empty = none.")]
        public Sprite ceilingSprite;
        public Material ceilingMaterial;
        public int ceilingSortingOrder = 4;

        [Header("Level edges")]
        [Tooltip("Skip faces on the outermost columns/rows of the tilemap (they face off-level)")]
        public bool skipBoundsFaces = true;

        Tilemap map;
        readonly List<GameObject> strips = new List<GameObject>();
        readonly List<(Vector3 from, Vector3 to, Color c)> gizmoRuns = new List<(Vector3, Vector3, Color)>();
        readonly HashSet<Vector3Int> filled = new HashSet<Vector3Int>();
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
            if (changed != map || dirty) return;
            // only tiles appearing or disappearing move the strips (a colour fade, e.g. FakeWall vanishing, does not)
            foreach (var t in tiles)
                if ((t.tile != null) != filled.Contains(t.position)) { dirty = true; return; }
        }

        void Update()
        {
            if (!dirty) return;
            dirty = false;
            Rebuild();
        }

        bool Has(int x, int y) => map.HasTile(new Vector3Int(x, y, 0));

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            Clear();
            if (!map) return;
            map.CompressBounds();
            var b = map.cellBounds;
            filled.Clear();
            foreach (var c in b.allPositionsWithin) if (map.HasTile(c)) filled.Add(c);

            // horizontal faces: top (turf) and bottom (ceiling roots)
            for (int y = b.yMin; y < b.yMax; y++)
            {
                ScanRow(b, y, +1, (x0, x1) => { if (lipSprite) AddTop(x0, x1, y); });
                if (ceilingSprite && !(skipBoundsFaces && y == b.yMin))
                    ScanRow(b, y, -1, (x0, x1) => AddCeiling(x0, x1, y));
            }
            // vertical faces: right side (+1) and left side (-1)
            if (sideSprite)
                for (int x = b.xMin; x < b.xMax; x++)
                {
                    if (!(skipBoundsFaces && x == b.xMax - 1)) ScanColumn(b, x, +1, (y0, y1) => AddSide(x, y0, y1, +1));
                    if (!(skipBoundsFaces && x == b.xMin)) ScanColumn(b, x, -1, (y0, y1) => AddSide(x, y0, y1, -1));
                }
        }

        void ScanRow(BoundsInt b, int y, int dir, System.Action<int, int> emit)
        {
            int start = int.MinValue;
            for (int x = b.xMin; x <= b.xMax; x++)
            {
                bool face = x < b.xMax && Has(x, y) && !Has(x, y + dir);
                if (face && start == int.MinValue) start = x;
                if (!face && start != int.MinValue)
                {
                    if (x - start >= minRun) emit(start, x);
                    start = int.MinValue;
                }
            }
        }

        void ScanColumn(BoundsInt b, int x, int dir, System.Action<int, int> emit)
        {
            int start = int.MinValue;
            for (int y = b.yMin; y <= b.yMax; y++)
            {
                bool face = y < b.yMax && Has(x, y) && !Has(x + dir, y);
                if (face && start == int.MinValue) start = y;
                if (!face && start != int.MinValue)
                {
                    emit(start, y);
                    start = int.MinValue;
                }
            }
        }

        void AddTop(int x0, int x1, int y)
        {
            Vector3 l = map.CellToWorld(new Vector3Int(x0, y + 1, 0)), r = map.CellToWorld(new Vector3Int(x1, y + 1, 0));
            Make("Lip", lipSprite, material, sortingOrder,
                 new Vector3((l.x + r.x) * 0.5f, l.y + yOffset, transform.position.z),
                 new Vector2(r.x - l.x + overhang * 2f, lipSprite.bounds.size.y), false);
            gizmoRuns.Add((l, r, new Color(0.6f, 1f, 0.5f)));
        }

        void AddCeiling(int x0, int x1, int y)
        {
            Vector3 l = map.CellToWorld(new Vector3Int(x0, y, 0)), r = map.CellToWorld(new Vector3Int(x1, y, 0));
            Make("Roots", ceilingSprite, ceilingMaterial, ceilingSortingOrder,
                 new Vector3((l.x + r.x) * 0.5f, l.y, transform.position.z),
                 new Vector2(r.x - l.x, ceilingSprite.bounds.size.y), false);
            gizmoRuns.Add((l, r, new Color(0.8f, 0.6f, 0.4f)));
        }

        void AddSide(int x, int y0, int y1, int dir)
        {
            // face line: right edge of the cell for dir=+1, left edge for dir=-1
            float fx = map.CellToWorld(new Vector3Int(dir > 0 ? x + 1 : x, 0, 0)).x;
            float bottom = map.CellToWorld(new Vector3Int(0, y0, 0)).y;
            float top = map.CellToWorld(new Vector3Int(0, y1, 0)).y;
            // tuck under the turf only where the top of this run is an exposed top face
            if (!Has(x, y1)) top -= sideTopInset;
            float len = Mathf.Max(0.1f, top - bottom);
            Make("Side", sideSprite, sideMaterial, sideSortingOrder,
                 new Vector3(fx, (top + bottom) * 0.5f, transform.position.z),
                 new Vector2(sideSprite.bounds.size.x, len), dir < 0);
            gizmoRuns.Add((new Vector3(fx, bottom), new Vector3(fx, top), new Color(0.7f, 0.7f, 1f)));
        }

        void Make(string kind, Sprite sprite, Material mat, int order, Vector3 pos, Vector2 size, bool flipX)
        {
            var go = new GameObject(kind + " " + strips.Count) { hideFlags = HideFlags.DontSave | HideFlags.NotEditable };
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (mat) sr.sharedMaterial = mat;
            sr.sortingOrder = order;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = size;
            sr.flipX = flipX;
            strips.Add(go);
        }

        void Clear()
        {
            foreach (var s in strips)
                if (s) { if (Application.isPlaying) Destroy(s); else DestroyImmediate(s); }
            strips.Clear();
            gizmoRuns.Clear();
            // strays from a domain reload (DontSave children are not tracked across reloads)
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i).gameObject;
                if ((c.hideFlags & HideFlags.DontSave) != 0)
                { if (Application.isPlaying) Destroy(c); else DestroyImmediate(c); }
            }
        }

        void OnDrawGizmosSelected()
        {
            foreach (var r in gizmoRuns)
            {
                Gizmos.color = r.c;
                Gizmos.DrawLine(r.from, r.to);
            }
        }
    }
}

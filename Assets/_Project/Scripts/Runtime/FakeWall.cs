using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace DuskTwilight
{
    /// <summary>
    /// 가짜 벽. Put on the "Fake Walls" tilemap. Each connected piece of wall is solid until the prince's echo (or a
    /// bell) reaches it — once it has been found it stays hollow and can be walked through. Colliders are built at
    /// runtime from the tiles (one box per cell), so painting the tilemap is all a level needs.
    /// </summary>
    [RequireComponent(typeof(Tilemap))]
    public class FakeWall : MonoBehaviour
    {
        [Header("Found by")]
        [Tooltip("Off by default: walls are found only by the prince's echo. On = the prince walking within revealDistance also finds it.")]
        public bool revealByProximity = false;
        public float revealDistance = 3f;
        [Tooltip("An echo (or a bell) reaching it")]
        public bool openOnEcho = true;
        [Tooltip("Once found the wall is gone for everyone (the princess and boxes pass too)")]
        public bool vanishWhenFound = true;
        [Tooltip("Seconds a found wall stays drawn before it vanishes (it can already be walked through), so the prince sees what he found")]
        [Min(0f)] public float vanishDelay = 1.2f;
        [Header("Vanishing")]
        [Tooltip("Seconds each tile takes to fade out")]
        [Min(0.05f)] public float fadeTime = 1.8f;
        [Tooltip("Seconds per metre: the fade spreads outward from where the echo touched the wall, like a ripple")]
        [Min(0f)] public float fadeSpread = 0.18f;
        [Tooltip("Pale glow a tile takes on while it fades (alpha = strength)")]
        public Color fadeGlow = new Color(1f, 0.92f, 0.7f, 0.55f);
        [Tooltip("Flicker while fading (0 = a plain fade)")]
        [Range(0f, 1f)] public float fadeShimmer = 0.3f;
        public float shimmerSpeed = 9f;

        [Tooltip("Share of the echo's radius that counts as reaching the wall (same as switches and bells)")]
        [Range(0.3f, 1f)] public float reachShare = 0.85f;
        [Tooltip("Bells reveal fake walls too")]
        public bool answersBells = true;
        [Tooltip("Small echo from the wall when it is found (shows its hollow shape)")]
        public float foundEcho = 0.35f;
        [Tooltip("Caption when a wall is found (what it looks like when you cannot hear it)")]
        public string foundCaption = "";

        [Header("Princess puzzled")]
        [Tooltip("Shown over the princess when she walks into a wall the prince hasn't seen through yet (Prefabs/FX/QuestionMark)")]
        public GameObject puzzledMark;
        [Tooltip("Seconds she must push against it before the mark shows")]
        public float puzzledAfter = 0.35f;
        public float puzzledCooldown = 4f;

        [Header("Linked")]
        [Tooltip("World rectangles (x, y = bottom-left, w, h). Finding any piece inside an area finds every piece in it (e.g. sun/moon/star ceilings vanish together).")]
        public Rect[] linkedAreas = new Rect[0];

        class Part { public List<Vector3> centres = new List<Vector3>(); public List<Vector3Int> cells = new List<Vector3Int>(); public GameObject go; public bool open; }
        readonly List<Part> parts = new List<Part>();
        Tilemap map;

        void Start()
        {
            map = GetComponent<Tilemap>();
            map.CompressBounds();
            var seen = new HashSet<Vector3Int>();
            foreach (var c in map.cellBounds.allPositionsWithin)
            {
                if (!map.HasTile(c) || seen.Contains(c)) continue;
                var part = new Part();
                var q = new Queue<Vector3Int>();
                q.Enqueue(c); seen.Add(c);
                while (q.Count > 0)
                {
                    var cell = q.Dequeue();
                    part.centres.Add(map.GetCellCenterWorld(cell));
                    part.cells.Add(cell);
                    foreach (var d in new[] { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down })
                        if (map.HasTile(cell + d) && seen.Add(cell + d)) q.Enqueue(cell + d);
                }
                // not a child of the tilemap: GroundLip rebuilds (and clears) the tilemap's children.
                // A plain scene object (no DontSave) so it goes away with the scene / when play mode ends.
                part.go = new GameObject($"{name} — solid until found") { layer = gameObject.layer };
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(part.go, gameObject.scene);
                var size = map.cellSize;
                foreach (var w in part.centres)
                {
                    var box = part.go.AddComponent<BoxCollider2D>();
                    box.offset = part.go.transform.InverseTransformPoint(w);
                    box.size = new Vector2(size.x, size.y);
                }
                parts.Add(part);
            }
        }

        void OnDestroy() { foreach (var p in parts) if (p.go) Destroy(p.go); }

        static readonly List<FakeWall> all = new List<FakeWall>();

        /// <summary>Is there a fake wall / ceiling tile (not yet found) at this world point?</summary>
        public static bool SolidAt(Vector2 world)
        {
            foreach (var f in all)
            {
                var m = f.map ? f.map : f.GetComponent<Tilemap>();
                if (m && m.HasTile(m.WorldToCell(world))) return true;
            }
            return false;
        }

        void OnEnable() { all.Add(this); EchoCaller.Called += OnCall; EchoResonator.Rang += OnBell; }
        void OnDisable() { all.Remove(this); EchoCaller.Called -= OnCall; EchoResonator.Rang -= OnBell; }

        void OnCall(Vector2 from, float strength) { if (openOnEcho) Hear(from, strength); }
        void OnBell(Vector2 from, float strength, EchoResonator bell) { if (openOnEcho && answersBells) Hear(from, strength); }

        float pushingWall, lastMark = -99f;

        /// <summary>The princess walking into an unfound wall: after a moment a "?" pops over her head.</summary>
        void CheckPuzzled(DuoController duo)
        {
            if (!puzzledMark) return;
            var a = duo.Leader;
            bool against = false;
            if (a == duo.princess && a.InputDir != 0)
            {
                Vector2 p = (Vector2)a.transform.position + Vector2.up * 0.8f;
                foreach (var part in parts)
                {
                    if (part.open) continue;
                    foreach (var c in part.centres)
                    {
                        float dx = c.x - p.x;
                        if (Mathf.Abs(c.y - p.y) < 1f && Mathf.Abs(dx) < 1.0f && Mathf.Sign(dx) == a.InputDir) { against = true; break; }
                    }
                    if (against) break;
                }
            }
            pushingWall = against ? pushingWall + Time.deltaTime : 0f;
            if (pushingWall >= puzzledAfter && Time.time - lastMark > puzzledCooldown)
            {
                lastMark = Time.time;
                var m = Instantiate(puzzledMark, a.transform.position, Quaternion.identity);
                var pm = m.GetComponent<PopMark>();
                if (pm) pm.target = a.transform;
            }
        }

        void Update()
        {
            var duo = DuoController.I;
            if (duo) CheckPuzzled(duo);
            if (!revealByProximity) return;
            if (!duo || duo.Leader != duo.prince) return;
            Vector2 p = (Vector2)duo.Leader.transform.position + Vector2.up * 0.5f;
            foreach (var part in parts)
            {
                if (part.open) continue;
                foreach (var c in part.centres)
                    if (Mathf.Abs(c.x - p.x) - 0.5f <= revealDistance && Mathf.Abs(c.y - p.y) - 0.5f <= revealDistance)
                    { StartCoroutine(Open(part, 0f, p)); break; }
            }
        }

        void Hear(Vector2 from, float strength)
        {
            var sc = SenseController.I;
            if (!sc) return;
            float r = strength * sc.echoRadiusPerStrength * sc.DirectorBoost * reachShare + 0.5f;   // + half a cell
            foreach (var p in parts)
            {
                if (p.open) continue;
                float best = float.MaxValue;
                foreach (var c in p.centres) best = Mathf.Min(best, Vector2.Distance(from, c));
                if (best <= r) StartCoroutine(Open(p, best / Mathf.Max(0.1f, sc.echoSpeed), from));
            }
        }

        bool InArea(Part p, Rect r)
        {
            foreach (var c in p.centres) if (r.Contains((Vector2)c)) return true;
            return false;
        }

        IEnumerator Open(Part p, float delay, Vector2 origin)
        {
            p.open = true;
            if (linkedAreas != null)
                foreach (var r in linkedAreas)
                    if (InArea(p, r))
                        foreach (var o in parts)
                            if (!o.open && InArea(o, r)) StartCoroutine(Open(o, delay, origin));
            yield return new WaitForSeconds(delay);
            if (p.go) Destroy(p.go);
            if (foundEcho > 0 && p.centres.Count > 0) SenseController.I?.EmitEcho(p.centres[p.centres.Count / 2], foundEcho);
            if (!string.IsNullOrEmpty(foundCaption)) GameEvents.Caption?.Invoke(foundCaption);
            GameEvents.PuzzleAttempt?.Invoke("fake-wall", true);
            if (vanishWhenFound && map)
            {
                // passable from here on; the found echo draws the wall for the prince before the tiles go
                if (vanishDelay > 0f) yield return new WaitForSeconds(vanishDelay);
                yield return Fade(p, origin);
                foreach (var c in p.cells) map.SetTile(c, null);
                foreach (var lip in GetComponents<GroundLip>()) lip.Rebuild();
            }
        }

        readonly List<SpriteRenderer> strips = new List<SpriteRenderer>();

        /// <summary>Tiles (and the GroundLip strips on them) dissolve one by one, rippling out from where the echo touched.</summary>
        IEnumerator Fade(Part p, Vector2 origin)
        {
            int n = p.cells.Count;
            var start = new float[n];
            var cols = new Color[n];
            float near = float.MaxValue, end = 0f;
            for (int i = 0; i < n; i++) near = Mathf.Min(near, Vector2.Distance(origin, p.centres[i]));
            for (int i = 0; i < n; i++)
            {
                start[i] = (Vector2.Distance(origin, p.centres[i]) - near) * fadeSpread;
                end = Mathf.Max(end, start[i] + fadeTime);
                map.SetTileFlags(p.cells[i], TileFlags.None);
            }
            for (float t = 0f; ; t += Time.deltaTime)
            {
                bool done = t >= end;
                for (int i = 0; i < n; i++)
                {
                    float u = done ? 1f : Mathf.Clamp01((t - start[i]) / fadeTime);
                    float bump = Mathf.Sin(u * Mathf.PI);   // 0 → 1 → 0 over the fade
                    float a = 1f - Mathf.SmoothStep(0f, 1f, u);
                    a *= 1f - fadeShimmer * bump * (0.5f + 0.5f * Mathf.Sin(t * shimmerSpeed + i * 1.7f));
                    var c = Color.Lerp(Color.white, fadeGlow, bump * fadeGlow.a);
                    c.a = a;
                    cols[i] = c;
                    map.SetColor(p.cells[i], c);
                }
                // strips are rebuilt by GroundLip at any time, so look them up each frame
                GetComponentsInChildren(strips);
                foreach (var sr in strips)
                {
                    Vector2 sc = sr.bounds.center;
                    int best = -1; float bd = 1.2f;
                    for (int i = 0; i < n; i++)
                    {
                        float d = Vector2.Distance(sc, p.centres[i]);
                        if (d < bd) { bd = d; best = i; }
                    }
                    if (best >= 0) sr.color = cols[best];
                }
                if (done) yield break;
                yield return null;
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.6f);
            if (linkedAreas != null) foreach (var r in linkedAreas) Gizmos.DrawWireCube(r.center, r.size);
            if (!Application.isPlaying) return;
            Gizmos.color = new Color(1f, 0.6f, 0.3f, 0.5f);
            foreach (var p in parts)
                if (!p.open) foreach (var c in p.centres) Gizmos.DrawWireCube(c, Vector3.one * 0.9f);
        }
#endif
    }
}

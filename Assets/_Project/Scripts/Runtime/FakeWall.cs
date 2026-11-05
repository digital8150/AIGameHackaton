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
        [Tooltip("The prince walking within revealDistance of it sees through it (새 1~3단계: 3m)")]
        public bool revealByProximity = true;
        public float revealDistance = 3f;
        [Tooltip("An echo (or a bell) reaching it")]
        public bool openOnEcho = true;
        [Tooltip("Once found the wall is gone for everyone (the princess and boxes pass too)")]
        public bool vanishWhenFound = true;
        [Tooltip("Share of the echo's radius that counts as reaching the wall (same as switches and bells)")]
        [Range(0.3f, 1f)] public float reachShare = 0.85f;
        [Tooltip("Bells reveal fake walls too")]
        public bool answersBells = true;
        [Tooltip("Small echo from the wall when it is found (shows its hollow shape)")]
        public float foundEcho = 0.35f;
        [Tooltip("Caption when a wall is found (what it looks like when you cannot hear it)")]
        public string foundCaption = "";

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

        void OnEnable() { EchoCaller.Called += OnCall; EchoResonator.Rang += OnBell; }
        void OnDisable() { EchoCaller.Called -= OnCall; EchoResonator.Rang -= OnBell; }

        void OnCall(Vector2 from, float strength) { if (openOnEcho) Hear(from, strength); }
        void OnBell(Vector2 from, float strength, EchoResonator bell) { if (openOnEcho && answersBells) Hear(from, strength); }

        void Update()
        {
            if (!revealByProximity) return;
            var duo = DuoController.I;
            if (!duo || duo.Leader != duo.prince) return;
            Vector2 p = (Vector2)duo.Leader.transform.position + Vector2.up * 0.5f;
            foreach (var part in parts)
            {
                if (part.open) continue;
                foreach (var c in part.centres)
                    if (Mathf.Abs(c.x - p.x) - 0.5f <= revealDistance && Mathf.Abs(c.y - p.y) - 0.5f <= revealDistance)
                    { StartCoroutine(Open(part, 0f)); break; }
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
                if (best <= r) StartCoroutine(Open(p, best / Mathf.Max(0.1f, sc.echoSpeed)));
            }
        }

        IEnumerator Open(Part p, float delay)
        {
            p.open = true;
            yield return new WaitForSeconds(delay);
            if (p.go) Destroy(p.go);
            if (vanishWhenFound && map)
            {
                foreach (var c in p.cells) map.SetTile(c, null);
                foreach (var lip in GetComponents<GroundLip>()) lip.Rebuild();
            }
            if (foundEcho > 0 && p.centres.Count > 0) SenseController.I?.EmitEcho(p.centres[p.centres.Count / 2], foundEcho);
            if (!string.IsNullOrEmpty(foundCaption)) GameEvents.Caption?.Invoke(foundCaption);
            GameEvents.PuzzleAttempt?.Invoke("fake-wall", true);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            Gizmos.color = new Color(1f, 0.6f, 0.3f, 0.5f);
            foreach (var p in parts)
                if (!p.open) foreach (var c in p.centres) Gizmos.DrawWireCube(c, Vector3.one * 0.9f);
        }
#endif
    }
}

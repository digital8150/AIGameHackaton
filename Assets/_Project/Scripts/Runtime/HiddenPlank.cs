using System.Collections.Generic;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// A hidden plank catches the prince's echo: when the wave arrives, a slanted glint wipes across it once, away
    /// from where the sound came from, and its outline stays lit for a moment, so the player sees at once that
    /// something is there. Answers called echoes and bells, not footsteps. The look (bands, slant, seconds, outline,
    /// colours) is on the material, SenseSprite's Echo glint.
    /// </summary>
    public class HiddenPlank : MonoBehaviour
    {
        static readonly int ProgressId = Shader.PropertyToID("_GlintProgress");
        static readonly int DirId = Shader.PropertyToID("_GlintDir");
        static readonly int BoundsId = Shader.PropertyToID("_GlintBounds");
        static readonly int SecondsId = Shader.PropertyToID("_GlintSeconds");
        static readonly int OutlineId = Shader.PropertyToID("_GlintOutline");
        static readonly int OutlineSecondsId = Shader.PropertyToID("_GlintOutlineSeconds");
        static readonly int OutlineFadeId = Shader.PropertyToID("_GlintOutlineFade");
        static readonly int RememberId = Shader.PropertyToID("_GlintRemember");

        static readonly List<HiddenPlank> all = new List<HiddenPlank>();

        [Tooltip("Blank = the sprite under this object.")]
        public SpriteRenderer visual;
        [Tooltip("Start when the wave actually reaches the plank (speed from SenseController); off = at once.")]
        public bool waitForWave = true;

        [Header("Once found")]
        [Tooltip("After the first echo (or a step) the plank keeps a faint dashed outline in the prince's view, so it reads as " +
                 "'still here, just unseen' rather than 'gone'. Strength and dash length are on the material (Found: …).")]
        public bool keepFoundOutline = true;
        [Tooltip("Outline strength while someone stands on the plank (prince's view)")]
        [Range(0f, 1f)] public float treadOutline = 0.8f;
        [Tooltip("Seconds for the tread outline to come and go")]
        [Min(0.01f)] public float treadFade = 0.25f;

        /// <summary>Found by an echo or a step (stays for the stage).</summary>
        public bool Found { get; private set; }

        MaterialPropertyBlock block;
        float startAt = -1f, elapsed = -1f, dir = 1f, trodUntil = -1f, tread;
        bool blockSet;

        /// <summary>Is a plank the player has not found yet within <paramref name="radius"/> of this point? (E-key nudge)</summary>
        public static bool AnyUnfoundWithin(Vector2 point, float radius)
        {
            foreach (var h in all)
            {
                if (h.Found || !h.visual) continue;
                Bounds b = h.visual.bounds;
                Vector2 c = b.ClosestPoint(new Vector3(point.x, point.y, b.center.z));
                if ((c - point).sqrMagnitude <= radius * radius) return true;
            }
            return false;
        }

        void Awake()
        {
            if (!visual) visual = GetComponentInChildren<SpriteRenderer>();
            block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            all.Add(this);
            EchoCaller.Called += OnCall;
            EchoResonator.Rang += OnBell;
        }

        void OnDisable()
        {
            all.Remove(this);
            EchoCaller.Called -= OnCall;
            EchoResonator.Rang -= OnBell;
            startAt = elapsed = -1f;
            Clear();
        }

        void OnCall(Vector2 at, float strength) => Heard(at, strength);
        void OnBell(Vector2 at, float strength, EchoResonator bell) => Heard(at, strength);

        void Heard(Vector2 at, float strength)
        {
            var sense = SenseController.I;
            if (!visual || !sense) return;
            Bounds b = visual.bounds;
            float distance = Vector2.Distance(at, b.ClosestPoint(new Vector3(at.x, at.y, b.center.z)));
            if (distance > strength * sense.echoRadiusPerStrength) return;
            dir = at.x <= b.center.x ? 1f : -1f;
            startAt = Time.time + (waitForWave ? distance / Mathf.Max(sense.echoSpeed, 0.01f) : 0f);
        }

        // someone standing on top: the plank lights up under their feet (and counts as found)
        void OnCollisionStay2D(Collision2D c)
        {
            if (!c.collider.GetComponentInParent<Actor>()) return;
            if (c.collider.bounds.min.y >= c.otherCollider.bounds.max.y - 0.12f) { trodUntil = Time.time + 0.12f; Found = true; }
        }

        float MaterialFloat(int id, float fallback)
        {
            var m = visual.sharedMaterial;
            return m && m.HasFloat(id) ? m.GetFloat(id) : fallback;
        }

        void Update()
        {
            if (!visual) return;
            if (startAt >= 0f && Time.time >= startAt) { startAt = -1f; elapsed = 0f; Found = true; }

            float progress = 0f, glintOutline = 0f;
            if (elapsed >= 0f)
            {
                elapsed += Time.deltaTime;
                float sweep = elapsed / Mathf.Max(MaterialFloat(SecondsId, 0.4f), 0.01f);
                float hold = MaterialFloat(OutlineSecondsId, 1.25f);
                float fade = Mathf.Max(MaterialFloat(OutlineFadeId, 0.5f), 0.01f);
                if (sweep >= 1f && elapsed >= hold) elapsed = -1f;
                else
                {
                    progress = sweep < 1f ? Mathf.Max(sweep, 0.0001f) : 0f;
                    glintOutline = Mathf.Clamp01((hold - elapsed) / fade);
                }
            }
            tread = Mathf.MoveTowards(tread, Time.time < trodUntil ? treadOutline : 0f, Time.deltaTime / treadFade);

            float outline = Mathf.Max(glintOutline, tread);
            bool remember = keepFoundOutline && Found;
            if (progress <= 0f && outline <= 0f && !remember) { Clear(); return; }

            Bounds b = visual.bounds;
            visual.GetPropertyBlock(block);
            block.SetFloat(ProgressId, progress);
            block.SetFloat(DirId, dir);
            block.SetVector(BoundsId, new Vector4(b.min.x, b.max.x, b.center.y, b.extents.y));
            block.SetFloat(OutlineId, outline);
            block.SetFloat(RememberId, remember ? 1f : 0f);
            visual.SetPropertyBlock(block);
            blockSet = true;
        }

        void Clear()
        {
            if (!blockSet) return;
            blockSet = false;
            if (visual) visual.SetPropertyBlock(null);   // back on the SRP batcher
        }

        void OnDrawGizmosSelected()
        {
            var sr = visual ? visual : GetComponentInChildren<SpriteRenderer>();
            if (!sr) return;
            Gizmos.color = new Color(1f, 1f, 1f, 0.7f);
            Gizmos.DrawWireCube(sr.bounds.center, sr.bounds.size);
        }
    }
}

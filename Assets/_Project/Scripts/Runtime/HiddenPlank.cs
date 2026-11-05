using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// A hidden plank catches the prince's echo: when the wave arrives, a slanted glint wipes across it once, away
    /// from where the sound came from, so the player sees at once that something is there. Answers called echoes and
    /// bells, not footsteps. The look (bands, slant, seconds, colour) is on the material, SenseSprite's Echo glint.
    /// </summary>
    public class HiddenPlank : MonoBehaviour
    {
        static readonly int ProgressId = Shader.PropertyToID("_GlintProgress");
        static readonly int DirId = Shader.PropertyToID("_GlintDir");
        static readonly int BoundsId = Shader.PropertyToID("_GlintBounds");
        static readonly int SecondsId = Shader.PropertyToID("_GlintSeconds");

        [Tooltip("Blank = the sprite under this object.")]
        public SpriteRenderer visual;
        [Tooltip("Start when the wave actually reaches the plank (speed from SenseController); off = at once.")]
        public bool waitForWave = true;

        MaterialPropertyBlock block;
        float startAt = -1f, progress = -1f, dir = 1f;

        void Awake()
        {
            if (!visual) visual = GetComponentInChildren<SpriteRenderer>();
            block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            EchoCaller.Called += OnCall;
            EchoResonator.Rang += OnBell;
        }

        void OnDisable()
        {
            EchoCaller.Called -= OnCall;
            EchoResonator.Rang -= OnBell;
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

        void Update()
        {
            if (startAt >= 0f && Time.time >= startAt) { startAt = -1f; progress = 0f; }
            if (progress < 0f || !visual) return;

            float seconds = visual.sharedMaterial && visual.sharedMaterial.HasFloat(SecondsId) ? visual.sharedMaterial.GetFloat(SecondsId) : 0.4f;
            progress += Time.deltaTime / Mathf.Max(seconds, 0.01f);
            if (progress >= 1f) { Clear(); return; }

            Bounds b = visual.bounds;
            visual.GetPropertyBlock(block);
            block.SetFloat(ProgressId, Mathf.Max(progress, 0.0001f));
            block.SetFloat(DirId, dir);
            block.SetVector(BoundsId, new Vector4(b.min.x, b.max.x, b.center.y, b.extents.y));
            visual.SetPropertyBlock(block);
        }

        void Clear()
        {
            progress = -1f;
            startAt = -1f;
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

using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// Something in the world that makes noise on a rhythm (dripping water, a hidden gear).
    ///  Prince's sense: you hear it (3D audio) and its echo outlines the space around it.
    ///  Princess's sense: silence — the sound is drawn as trembling arcs (SoundGlyph) plus a caption.
    /// </summary>
    public class SoundEmitter : MonoBehaviour
    {
        [Header("Sound")]
        public AudioClip[] clips;
        [Tooltip("Seconds between sounds (± jitter).")]
        public float interval = 1.4f;
        public float jitter = 0.1f;
        [Range(0, 1)] public float volume = 0.8f;
        [Tooltip("Audible range in world units: full volume inside x, silent past y. Cyan rings in the Scene view.")]
        public Vector2 audibleRange = new Vector2(2f, 18f);
        public bool active = true;

        [Header("Prince's sense")]
        [Tooltip("Echo wave size each time it sounds.")]
        public float echoStrength = 0.6f;

        [Header("Princess's sense")]
        [Tooltip("What the sound looks like, e.g. 끼릭… 끼릭… — written into the Caption TextMesh.")]
        public string caption;
        public Renderer glyph;            // SoundGlyph quad
        public TextMesh captionText;      // world-space caption, visible in light sense

        AudioSource src;
        float next;
        float pulse;
        MaterialPropertyBlock mpb;
        static readonly int IdIntensity = Shader.PropertyToID("_Intensity");

        public float Loudness { get; set; } = 1f; // director can raise this

        void Awake()
        {
            src = GetComponent<AudioSource>();
            if (!src) src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0.85f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = audibleRange.x;
            src.maxDistance = audibleRange.y;
            if (captionText && !string.IsNullOrEmpty(caption)) captionText.text = caption;
            mpb = new MaterialPropertyBlock();
            next = Time.time + Random.Range(0f, interval);
        }

        void Update()
        {
            if (active && Time.time >= next)
            {
                next = Time.time + interval + Random.Range(-jitter, jitter);
                Emit();
            }
            pulse = Mathf.MoveTowards(pulse, 0.25f, Time.deltaTime * 1.2f);
            if (glyph)
            {
                glyph.GetPropertyBlock(mpb);
                mpb.SetFloat(IdIntensity, active ? Mathf.Clamp01(pulse * Loudness) : 0f);
                glyph.SetPropertyBlock(mpb);
            }
            if (captionText && SenseController.I)
            {
                float light = 1f - SenseController.I.SenseAt(transform.position);
                var c = captionText.color;
                c.a = active ? light * Mathf.Clamp01(0.35f + pulse) : 0f;
                captionText.color = c;
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, audibleRange.x);
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, audibleRange.y);
        }
#endif

        public void Emit()
        {
            if (clips != null && clips.Length > 0) src.PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
            pulse = 1f;
            if (SenseController.I) SenseController.I.EmitEcho(transform.position, echoStrength * Loudness);
        }
    }
}

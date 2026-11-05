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
        public AudioClip[] clips;
        public float interval = 1.4f;
        public float jitter = 0.1f;
        public float echoStrength = 0.6f;
        public float volume = 0.8f;
        public string caption;            // e.g. "끼릭… 끼릭…"
        public Renderer glyph;            // SoundGlyph quad
        public TextMesh captionText;      // world-space caption, visible in light sense
        public bool active = true;

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
            src.minDistance = 2f;
            src.maxDistance = 18f;
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

        public void Emit()
        {
            if (clips != null && clips.Length > 0) src.PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
            pulse = 1f;
            if (SenseController.I) SenseController.I.EmitEcho(transform.position, echoStrength * Loudness);
        }
    }
}

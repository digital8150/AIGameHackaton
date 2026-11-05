using UnityEngine;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// One character's badge: portrait + procedural frame. Holds its own material instances and feeds them
    /// time, activity, energy, the periodic glint and the switch flash. Moved around by <see cref="SenseEmblems"/>.
    /// </summary>
    public class SenseEmblem : MonoBehaviour
    {
        static readonly int TId = Shader.PropertyToID("_T");
        static readonly int ActivityId = Shader.PropertyToID("_Activity");
        static readonly int EnergyId = Shader.PropertyToID("_Energy");
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int GlintId = Shader.PropertyToID("_Glint");

        [Header("Elements")]
        public Image frame;
        public Image portrait;

        [Header("Glint")]
        [Tooltip("Seconds between shines. 0 = never.")]
        public float glintInterval = 4f;
        [Tooltip("Seconds one shine takes to cross the badge.")]
        public float glintSeconds = 0.9f;
        [Tooltip("Seconds before the first shine, so the two badges do not shine together.")]
        public float glintOffset = 1.5f;

        [Header("Flash")]
        [Tooltip("Seconds the flash takes to fade.")]
        public float flashSeconds = 0.6f;

        Material frameMat, portraitMat;
        float flash, nextGlint, glintT = -1f;

        public RectTransform Rect => (RectTransform)transform;

        void Awake()
        {
            if (!frame || !portrait) { Debug.LogError("[SenseEmblem] frame/portrait are not assigned - use the HUD prefab.", this); return; }
            frameMat = new Material(frame.material) { name = frame.material.name + " (Instance)" };
            portraitMat = new Material(portrait.material) { name = portrait.material.name + " (Instance)" };
            frame.material = frameMat;
            portrait.material = portraitMat;
            nextGlint = Time.unscaledTime + glintOffset;
        }

        public void Flash(float strength = 1f) => flash = Mathf.Max(flash, strength);

        /// <summary>Glint now, whatever the timer says.</summary>
        public void Shine()
        {
            glintT = 0f;
            nextGlint = Time.unscaledTime + glintInterval;
        }

        /// <param name="activity">1 = leader, lower = partner.</param>
        /// <param name="energy">0..1 voice level or echo charge.</param>
        /// <param name="tint">Multiplied over the whole badge.</param>
        public void Tick(float activity, float energy, Color tint)
        {
            if (!frameMat) return;
            float dt = Time.unscaledDeltaTime;

            if (glintInterval > 0 && glintT < 0 && Time.unscaledTime >= nextGlint) Shine();
            float glint = 0f;
            if (glintT >= 0)
            {
                glintT += dt / Mathf.Max(glintSeconds, 0.01f);
                if (glintT >= 1f) glintT = -1f;
                else glint = glintT;
            }
            flash = Mathf.MoveTowards(flash, 0f, dt / Mathf.Max(flashSeconds, 0.01f));

            frameMat.SetFloat(TId, Time.unscaledTime);
            frameMat.SetFloat(ActivityId, activity);
            frameMat.SetFloat(EnergyId, energy);
            frameMat.SetFloat(FlashId, flash);
            frameMat.SetFloat(GlintId, glint);
            portraitMat.SetFloat(FlashId, flash);
            portraitMat.SetFloat(GlintId, glint);
            frame.color = portrait.color = tint;
        }

        void OnDestroy()
        {
            if (frameMat) Destroy(frameMat);
            if (portraitMat) Destroy(portraitMat);
        }
    }
}

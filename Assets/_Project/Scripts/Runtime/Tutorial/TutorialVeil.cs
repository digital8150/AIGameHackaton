using System.Collections;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// A curtain of dusk light that holds the path until the tutorial reaches it.
    /// Solid while closed; dissolves upward when opened.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class TutorialVeil : MonoBehaviour
    {
        public Renderer glow;
        public AudioClip openClip;

        BoxCollider2D col;
        MaterialPropertyBlock mpb;
        static readonly int IdIntensity = Shader.PropertyToID("_Intensity");

        void Awake()
        {
            col = GetComponent<BoxCollider2D>();
            mpb = new MaterialPropertyBlock();
        }

        public void Close()
        {
            if (!col) Awake();
            col.enabled = true;
            if (glow) { glow.enabled = true; SetIntensity(1f); }
        }

        public void Open()
        {
            if (!col) Awake();
            col.enabled = false;
            if (openClip) AudioSource.PlayClipAtPoint(openClip, transform.position, 0.5f);
            SenseController.I?.EmitEcho(transform.position, 0.6f);
            if (gameObject.activeInHierarchy) StartCoroutine(Fade());
            else if (glow) glow.enabled = false;
        }

        IEnumerator Fade()
        {
            for (float t = 1; t > 0; t -= Time.deltaTime * 1.5f)
            {
                SetIntensity(t);
                if (glow) glow.transform.localScale = new Vector3(glow.transform.localScale.x, glow.transform.localScale.y * (1f + Time.deltaTime * 0.6f), 1);
                yield return null;
            }
            if (glow) glow.enabled = false;
        }

        void SetIntensity(float v)
        {
            if (!glow) return;
            glow.GetPropertyBlock(mpb);
            mpb.SetFloat(IdIntensity, v * 1.3f);
            glow.SetPropertyBlock(mpb);
        }
    }
}

using System.Collections;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// A curtain of dusk light that holds the path until the tutorial reaches it.
    /// Solid while closed (a shimmering pillar of glow motes); dissolves upward when opened.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class TutorialVeil : MonoBehaviour
    {
        public Renderer glow;          // optional: kept for older setups; all child renderers are used
        public AudioClip openClip;
        public float intensity = 1.1f;

        BoxCollider2D col;
        Renderer[] motes;
        Vector3[] basePos;
        MaterialPropertyBlock mpb;
        bool closed = true;
        float fade = 1f;
        static readonly int IdIntensity = Shader.PropertyToID("_Intensity");

        void Awake()
        {
            col = GetComponent<BoxCollider2D>();
            mpb = new MaterialPropertyBlock();
            motes = GetComponentsInChildren<Renderer>(true);
            basePos = new Vector3[motes.Length];
            for (int i = 0; i < motes.Length; i++) basePos[i] = motes[i].transform.localPosition;
        }

        public void Close()
        {
            if (!col) Awake();
            closed = true;
            fade = 1f;
            col.enabled = true;
            foreach (var m in motes) m.enabled = true;
        }

        public void Open()
        {
            if (!col) Awake();
            if (!closed) return;
            closed = false;
            col.enabled = false;
            if (openClip) AudioSource.PlayClipAtPoint(openClip, transform.position, 0.5f);
            SenseController.I?.EmitEcho(transform.position, 0.6f);
            if (gameObject.activeInHierarchy) StartCoroutine(Fade());
            else foreach (var m in motes) m.enabled = false;
        }

        void Update()
        {
            if (motes == null) return;
            // slow upward drift + breathing, each mote out of phase
            for (int i = 0; i < motes.Length; i++)
            {
                if (!motes[i].enabled) continue;
                float ph = Time.time * 1.3f + i * 1.7f;
                float rise = closed ? 0 : (1f - fade) * (1.5f + i * 0.6f);
                motes[i].transform.localPosition = basePos[i] + new Vector3(Mathf.Sin(ph * 0.7f) * 0.06f, Mathf.Sin(ph) * 0.08f + rise, 0);
                motes[i].GetPropertyBlock(mpb);
                mpb.SetFloat(IdIntensity, intensity * fade * (0.8f + 0.2f * Mathf.Sin(ph * 1.9f)));
                motes[i].SetPropertyBlock(mpb);
            }
        }

        IEnumerator Fade()
        {
            for (fade = 1; fade > 0; fade -= Time.deltaTime * 1.2f) yield return null;
            fade = 0;
            foreach (var m in motes) m.enabled = false;
        }
    }
}

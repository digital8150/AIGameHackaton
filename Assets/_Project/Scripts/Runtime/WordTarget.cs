using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// Something the princess can speak to (빛글씨). Shared by every word puzzle:
    ///  - the nearest one in reach receives voice transcripts (VoiceCommands) and opens the choice panel on E,
    ///  - spoken letters appear as light and can fly to a spot, harden into planks, or crumble.
    /// Subclasses decide what a spoken word does.
    /// </summary>
    public abstract class WordTarget : MonoBehaviour
    {
        public static WordTarget Nearest { get; private set; }

        [Header("Words")]
        [Tooltip("Options on the choice panel (E) — the alternative to the microphone.")]
        public string[] choices = { "새벽빛", "해질녘", "별무리", "물안개" };
        public string panelQuestion = "무엇을 부를까?";
        [Tooltip("Only the princess can speak in light. In the prince's sense the target ignores words.")]
        public bool requirePrincess = true;

        [Header("Light letters")]
        [Tooltip("One light letter: TextMesh + SenseText, Glow child, inactive Plank child shown when it hardens.")]
        public GameObject letterPrefab;
        [Tooltip("Where the spoken letters appear, relative to this object: x = letter spacing, y = height.")]
        public Vector2 letterSpawn = new Vector2(1.1f, 1.6f);
        [Tooltip("Seconds between letters flying to their slots.")]
        public float letterStagger = 0.18f;
        public float letterFlyTime = 0.6f;
        [Tooltip("Collider size of each letter plank.")]
        public Vector2 letterSize = new Vector2(1.9f, 0.6f);
        [Tooltip("Plank under each letter, relative to the letter.")]
        public float plankOffsetY = -0.25f;

        [Header("Interaction")]
        [Tooltip("Half-width of the area in front of this object where E opens the choices.")]
        public float reach = 1.4f;
        public float reachHeight = 1.6f;
        [Tooltip("Offset of the reach box from this object (e.g. a bridge that is spoken to from the ground before it)")]
        public Vector2 reachOffset = Vector2.zero;
        public TextMesh prompt;

        [Header("Choice panel (child Word Panel, laid out in the prefab)")]
        public Canvas canvas;
        public RectTransform panelRect;
        public Text questionText;
        [Tooltip("One option; cloned per choice and spread evenly across the panel.")]
        public Text optionTemplate;
        public Color optionColor = new Color(1f, 0.95f, 0.86f, 0.6f);
        public Color optionSelectedColor = new Color(1f, 0.86f, 0.52f);
        public string selectedFormat = "▶ {0}";
        public AudioClip speakClip, wrongClip, bridgeClip;

        public bool Solved { get; protected set; }
        /// <summary>Raw speech is used as-is (no word matching): blank bridges take any words.</summary>
        public virtual bool AcceptsAnyText => false;

        protected AudioSource sfx;
        readonly List<Text> options = new List<Text>();
        int selected;
        bool open;
        float moveHeld;

        protected virtual void Awake()
        {
            if (!letterPrefab) Debug.LogError($"[{GetType().Name}] letterPrefab is not assigned - use LightLetter.prefab.", this);
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
        }

        protected Vector3 ReachCenter => transform.position + (Vector3)reachOffset;

        bool CanSpeakNow => !requirePrincess || SenseController.I == null || !SenseController.I.IsDark;

        protected virtual void Update()
        {
            var duo = DuoController.I;
            if (!duo) return;
            var p = duo.Leader.transform.position;
            var c = ReachCenter;
            bool near = !Solved && Mathf.Abs(p.x - c.x) < reach && Mathf.Abs(p.y - c.y) < reachHeight;
            if (near) Nearest = this; else if (Nearest == this) Nearest = null;
            if (prompt) prompt.color = new Color(1, 0.95f, 0.85f, near && !open && CanSpeakNow ? 1 : 0);

            if (open) { PanelInput(); return; }
            if (near && CanSpeakNow && !GameFlow.InputLocked && GameInput.I.Interact.WasPressedThisFrame()) Open();
        }

        protected virtual void OnDisable() { if (Nearest == this) Nearest = null; }

        /// <summary>Entry point for any input: the choice panel, or a voice transcript.</summary>
        public void Speak(string text)
        {
            if (Solved || !CanSpeakNow) return;
            text = Clean(text);
            if (text.Length == 0) return;
            if (speakClip) sfx.PlayOneShot(speakClip, 0.6f);
            OnSpoken(text);
        }

        protected abstract void OnSpoken(string text);

        /// <summary>Keep letters only (speech transcripts come with spaces and punctuation).</summary>
        public static string Clean(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in s ?? "") if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ light letters
        protected List<GameObject> Letters(string word, Vector3 origin)
        {
            var list = new List<GameObject>();
            if (!letterPrefab) return list;
            float x0 = -(word.Length - 1) * letterSpawn.x * 0.5f;
            for (int i = 0; i < word.Length; i++)
            {
                var pos = origin + new Vector3(x0 + i * letterSpawn.x, letterSpawn.y, 0);
                var inst = Instantiate(letterPrefab, pos, Quaternion.identity);
                inst.name = "Light Letter " + word[i];
                var itm = inst.GetComponentInChildren<TextMesh>();
                if (itm) itm.text = word[i].ToString();
                list.Add(inst);
            }
            return list;
        }

        /// <summary>Fly a letter to <paramref name="target"/>; if solid it becomes a plank you can stand on.</summary>
        protected IEnumerator Fly(GameObject letter, Vector3 target, float delay, bool solid, bool vanishOnArrival = false)
        {
            yield return new WaitForSeconds(delay);
            if (!letter) yield break;
            Vector3 a = letter.transform.position;
            Vector3 mid = (a + target) * 0.5f + Vector3.up * 2f;
            for (float t = 0; t < 1; t += Time.deltaTime / Mathf.Max(0.01f, letterFlyTime))
            {
                if (!letter) yield break;
                float e = Mathf.SmoothStep(0, 1, t);
                letter.transform.position = Vector3.Lerp(Vector3.Lerp(a, mid, e), Vector3.Lerp(mid, target, e), e);
                yield return null;
            }
            if (!letter) yield break;
            letter.transform.position = target;
            if (vanishOnArrival) { StartCoroutine(FadeOut(letter, 0.4f)); yield break; }
            if (!solid) yield break;
            var plank = letter.transform.Find("Plank");
            if (plank) plank.gameObject.SetActive(true);
            var col = letter.AddComponent<BoxCollider2D>();
            col.size = letterSize;
            col.offset = new Vector2(0, plankOffsetY);
            SenseController.I?.EmitEcho(target, 0.35f);
        }

        protected IEnumerator Fall(GameObject letter, float delay)
        {
            yield return new WaitForSeconds(delay + 0.35f);
            if (!letter) yield break;
            var st = letter.GetComponent<SenseText>();
            Vector3 v = new Vector3(Random.Range(-0.8f, 0.8f), 1.5f, 0);
            for (float t = 0; t < 1; t += Time.deltaTime / 0.9f)
            {
                if (!letter) yield break;
                v.y -= 9f * Time.deltaTime;
                letter.transform.position += v * Time.deltaTime;
                if (st) st.lightAlpha = st.darkAlpha = 1f - t;
                yield return null;
            }
            if (letter) Destroy(letter);
        }

        protected IEnumerator FadeOut(GameObject letter, float time)
        {
            var st = letter ? letter.GetComponent<SenseText>() : null;
            for (float t = 0; t < 1; t += Time.deltaTime / Mathf.Max(0.01f, time))
            {
                if (!letter) yield break;
                if (st) st.lightAlpha = st.darkAlpha = 1f - t;
                yield return null;
            }
            if (letter) Destroy(letter);
        }

        // ------------------------------------------------------------------ choice panel (alternative input)
        /// <summary>Open the word choices (button E, or voice "열어").</summary>
        public void Open()
        {
            if (open || Solved || !CanSpeakNow) return;
            if (!canvas) { Debug.LogError($"[{GetType().Name}] choice panel is not assigned - use a prefab with Word Panel.", this); return; }
            if (questionText) questionText.text = panelQuestion;
            if (options.Count != choices.Length) MakeOptions();
            open = true;
            GameFlow.ModalOpen = true;
            selected = 0;
            canvas.enabled = true;
            Refresh();
        }

        void ClosePanel()
        {
            open = false;
            GameFlow.ModalOpen = false;
            canvas.enabled = false;
        }

        void PanelInput()
        {
            var kb = Keyboard.current;
            float m = GameInput.I.Move.ReadValue<float>();
            if (Mathf.Abs(m) > 0.5f)
            {
                if (moveHeld <= 0) { selected = (selected + (m > 0 ? 1 : -1) + choices.Length) % choices.Length; Refresh(); }
                moveHeld = moveHeld <= 0 ? 0.3f : moveHeld - Time.deltaTime;
            }
            else moveHeld = 0;
            if (kb != null)
                for (int i = 0; i < choices.Length && i < 9; i++)
                    if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) { selected = i; Refresh(); Confirm(); return; }
            if (GameInput.I.Interact.WasPressedThisFrame() || GameInput.I.Jump.WasPressedThisFrame()) { Confirm(); return; }
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || GameInput.I.Switch.WasPressedThisFrame()) ClosePanel();
        }

        void Confirm()
        {
            string word = choices[selected];
            ClosePanel();
            Speak(word);
        }

        void Refresh()
        {
            for (int i = 0; i < options.Count; i++)
            {
                bool s = i == selected;
                options[i].text = s ? string.Format(selectedFormat, choices[i]) : choices[i];
                options[i].color = s ? optionSelectedColor : optionColor;
            }
        }

        void MakeOptions()
        {
            foreach (var o in options) if (o) Destroy(o.gameObject);
            options.Clear();
            optionTemplate.gameObject.SetActive(false);
            var tr = optionTemplate.rectTransform;
            float total = panelRect.sizeDelta.x - 20f, w = total / choices.Length;
            for (int i = 0; i < choices.Length; i++)
            {
                var t = Instantiate(optionTemplate, optionTemplate.transform.parent);
                t.gameObject.SetActive(true);
                t.name = "Option " + choices[i];
                t.rectTransform.anchoredPosition = new Vector2(-total / 2 + w * (i + 0.5f), tr.anchoredPosition.y);
                t.rectTransform.sizeDelta = new Vector2(w, tr.sizeDelta.y);
                options.Add(t);
            }
        }

#if UNITY_EDITOR
        protected virtual void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.35f, 0.8f);
            Gizmos.DrawWireCube(ReachCenter, new Vector3(reach * 2, reachHeight * 2, 0));
        }
#endif
    }
}

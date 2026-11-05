using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// 빛의 글자 (기획서 스테이지 2). A word is carved in light on the altar — only the princess can read it.
    /// Calling it out turns its letters into a bridge across the chasm.
    ///  - Alternative input (design: "단어 입력 또는 선택지"): E opens a choice of words, ←/→ + E to call one.
    ///  - Voice: a speech recogniser (Whisper bridge) calls <see cref="Speak"/> with its transcript.
    /// </summary>
    public class WordAltar : MonoBehaviour
    {
        public static WordAltar Nearest { get; private set; }

        [Header("Word")]
        public string answer = "해질녘";
        public string[] choices = { "새벽빛", "해질녘", "별무리", "물안개" };

        [Header("Bridge")]
        public Transform[] slots;          // where each letter settles (left → right)
        public Vector2 letterSize = new Vector2(1.9f, 0.6f);
        public Font letterFont;
        public Color letterColor = new Color(1f, 0.88f, 0.55f);
        public Material glowMaterial;

        [Header("Interaction")]
        public float reach = 1.4f;
        public TextMesh prompt;
        public SenseText inscription;
        public Font uiFont, uiBoldFont;
        public Sprite panelSprite;
        public AudioClip speakClip, wrongClip, bridgeClip;

        public bool Solved { get; private set; }

        AudioSource sfx;
        Canvas canvas;
        RectTransform panelRect;
        readonly List<Text> options = new List<Text>();
        int selected;
        bool open;
        float moveHeld;

        void Awake()
        {
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
        }

        void Update()
        {
            var duo = DuoController.I;
            if (!duo) return;
            bool near = !Solved && Mathf.Abs(duo.Leader.transform.position.x - transform.position.x) < reach
                        && Mathf.Abs(duo.Leader.transform.position.y - transform.position.y) < 1.6f;
            if (near) Nearest = this; else if (Nearest == this) Nearest = null;
            if (prompt) prompt.color = new Color(1, 0.95f, 0.85f, near && !open ? 1 : 0);

            if (open) { PanelInput(); return; }
            if (near && !GameFlow.InputLocked && GameInput.I.Interact.WasPressedThisFrame()) Open();
        }

        // ------------------------------------------------------------------ calling a word
        /// <summary>Entry point for any input: choice panel today, voice transcript later.</summary>
        public void Speak(string word)
        {
            if (Solved) return;
            word = (word ?? "").Trim();
            if (speakClip) sfx.PlayOneShot(speakClip, 0.6f);
            bool ok = word == answer;
            GameEvents.PuzzleAttempt?.Invoke(ok ? "word" : "word-wrong", ok);
            StartCoroutine(ok ? Build(word) : Crumble(word));
        }

        IEnumerator Build(string word)
        {
            Solved = true;
            if (bridgeClip) sfx.PlayOneShot(bridgeClip, 0.9f);
            SenseController.I?.EmitEcho(transform.position, 1.2f);
            var letters = Letters(word);
            for (int i = 0; i < letters.Count; i++)
            {
                var target = slots[Mathf.Min(i, slots.Length - 1)].position;
                StartCoroutine(Fly(letters[i], target, 0.18f * i, solid: true));
            }
            yield return new WaitForSeconds(0.18f * letters.Count + 0.7f);
            CameraRig.I?.Kick(0.12f);
        }

        IEnumerator Crumble(string word)
        {
            if (wrongClip) sfx.PlayOneShot(wrongClip, 0.8f);
            GameEvents.Caption?.Invoke("……");
            var letters = Letters(word);
            for (int i = 0; i < letters.Count; i++)
                StartCoroutine(Fall(letters[i], 0.1f * i));
            yield break;
        }

        List<GameObject> Letters(string word)
        {
            var list = new List<GameObject>();
            float x0 = -(word.Length - 1) * 0.55f;
            for (int i = 0; i < word.Length; i++)
            {
                var go = new GameObject("Light Letter " + word[i]);
                go.transform.position = transform.position + new Vector3(x0 + i * 1.1f, 1.6f, 0);
                var tm = go.AddComponent<TextMesh>();
                tm.font = letterFont;
                tm.text = word[i].ToString();
                tm.fontSize = 64;
                tm.characterSize = 0.16f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.color = letterColor;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = letterFont.material;
                mr.sortingOrder = 25;
                var st = go.AddComponent<SenseText>();
                st.lightAlpha = 1f; st.darkAlpha = 0.18f; st.shimmer = 0.1f;
                st.SetBaseColor(letterColor);
                if (glowMaterial)
                {
                    var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Destroy(g.GetComponent<Collider>());
                    g.transform.SetParent(go.transform, false);
                    g.transform.localScale = Vector3.one * 2.4f;
                    var gr = g.GetComponent<MeshRenderer>();
                    gr.sharedMaterial = glowMaterial;
                    gr.sortingOrder = 24;
                }
                list.Add(go);
            }
            return list;
        }

        IEnumerator Fly(GameObject letter, Vector3 target, float delay, bool solid)
        {
            yield return new WaitForSeconds(delay);
            Vector3 a = letter.transform.position;
            Vector3 mid = (a + target) * 0.5f + Vector3.up * 2f;
            for (float t = 0; t < 1; t += Time.deltaTime / 0.6f)
            {
                float e = Mathf.SmoothStep(0, 1, t);
                letter.transform.position = Vector3.Lerp(Vector3.Lerp(a, mid, e), Vector3.Lerp(mid, target, e), e);
                yield return null;
            }
            letter.transform.position = target;
            if (!solid) yield break;
            var col = letter.AddComponent<BoxCollider2D>();
            col.size = letterSize;
            col.offset = new Vector2(0, -0.25f);
            SenseController.I?.EmitEcho(target, 0.35f);
        }

        IEnumerator Fall(GameObject letter, float delay)
        {
            yield return new WaitForSeconds(delay + 0.35f);
            var st = letter.GetComponent<SenseText>();
            Vector3 v = new Vector3(Random.Range(-0.8f, 0.8f), 1.5f, 0);
            for (float t = 0; t < 1; t += Time.deltaTime / 0.9f)
            {
                v.y -= 9f * Time.deltaTime;
                letter.transform.position += v * Time.deltaTime;
                st.lightAlpha = st.darkAlpha = 1f - t;
                yield return null;
            }
            Destroy(letter);
        }

        // ------------------------------------------------------------------ choice panel (alternative input)
        /// <summary>Open the word choices (button E, or voice "열어").</summary>
        public void Open()
        {
            if (open || Solved) return;
            if (!canvas) BuildPanel();
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
                options[i].text = s ? $"▶ {choices[i]}" : choices[i];
                options[i].color = s ? new Color(1f, 0.86f, 0.52f) : new Color(1f, 0.95f, 0.86f, 0.6f);
            }
        }

        void BuildPanel()
        {
            var go = new GameObject("Word Panel", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            scaler.matchWidthOrHeight = 1f;

            var bg = new GameObject("Panel", typeof(Image)).GetComponent<Image>();
            bg.transform.SetParent(go.transform, false);
            bg.sprite = panelSprite;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 100f / 18f / 2f;
            panelRect = bg.rectTransform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0);
            panelRect.anchoredPosition = new Vector2(0, 40);
            panelRect.sizeDelta = new Vector2(620, 104);

            var q = MakeText(panelRect, "무엇을 부를까?", 20, new Color(0.86f, 0.80f, 0.92f), false);
            q.alignment = TextAnchor.MiddleCenter;
            Set(q.rectTransform, new Vector2(0, -12), new Vector2(600, 28));
            float w = 600f / choices.Length;
            for (int i = 0; i < choices.Length; i++)
            {
                var t = MakeText(panelRect, choices[i], 26, Color.white, true);
                t.alignment = TextAnchor.MiddleCenter;
                Set(t.rectTransform, new Vector2(-300 + w * (i + 0.5f), -56), new Vector2(w, 36));
                options.Add(t);
            }
        }

        Text MakeText(Transform parent, string s, int size, Color c, bool bold)
        {
            var t = new GameObject("Text", typeof(Text)).GetComponent<Text>();
            t.transform.SetParent(parent, false);
            t.font = bold && uiBoldFont ? uiBoldFont : uiFont;
            t.fontSize = size;
            t.color = c;
            t.text = s;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0.08f, 0.03f, 0.10f, 0.9f);
            sh.effectDistance = new Vector2(2, -2);
            return t;
        }

        static void Set(RectTransform r, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 1);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }
    }
}

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
        [Tooltip("The word carved on the altar. One letter per slot.")]
        public string answer = "해질녘";
        [Tooltip("Options on the choice panel (E). Include the answer.")]
        public string[] choices = { "새벽빛", "해질녘", "별무리", "물안개" };
        public string panelQuestion = "무엇을 부를까?";
        [Tooltip("Caption when the wrong word crumbles.")]
        public string wrongCaption = "……";

        [Header("Bridge")]
        [Tooltip("Where each letter settles (left → right). Gold boxes in the Scene view.")]
        public Transform[] slots;
        [Tooltip("Collider size of each letter plank.")]
        public Vector2 letterSize = new Vector2(1.9f, 0.6f);
        [Tooltip("One light letter: TextMesh + SenseText, Glow child, inactive Plank child shown when it hardens.")]
        public GameObject letterPrefab;
        [Tooltip("Where the spoken letters appear, relative to the altar: x = letter spacing, y = height.")]
        public Vector2 letterSpawn = new Vector2(1.1f, 1.6f);
        [Tooltip("Seconds between letters flying to their slots.")]
        public float letterStagger = 0.18f;
        public float letterFlyTime = 0.6f;
        [Tooltip("Plank under each letter, relative to the letter.")]
        public float plankOffsetY = -0.25f;

        [Header("Interaction")]
        [Tooltip("Half-width of the area in front of the altar where E opens the choices.")]
        public float reach = 1.4f;
        public float reachHeight = 1.6f;
        public TextMesh prompt;
        public SenseText inscription;

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

        public bool Solved { get; private set; }

        AudioSource sfx;
        readonly List<Text> options = new List<Text>();
        int selected;
        bool open;
        float moveHeld;

        void Awake()
        {
            if (!letterPrefab) Debug.LogError("[WordAltar] letterPrefab is not assigned - use LightLetter.prefab.", this);
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
        }

        void Update()
        {
            var duo = DuoController.I;
            if (!duo) return;
            bool near = !Solved && Mathf.Abs(duo.Leader.transform.position.x - transform.position.x) < reach
                        && Mathf.Abs(duo.Leader.transform.position.y - transform.position.y) < reachHeight;
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
                StartCoroutine(Fly(letters[i], target, letterStagger * i, solid: true));
            }
            yield return new WaitForSeconds(letterStagger * letters.Count + letterFlyTime + 0.1f);
            CameraRig.I?.Kick(0.12f);
        }

        IEnumerator Crumble(string word)
        {
            if (wrongClip) sfx.PlayOneShot(wrongClip, 0.8f);
            GameEvents.Caption?.Invoke(wrongCaption);
            var letters = Letters(word);
            for (int i = 0; i < letters.Count; i++)
                StartCoroutine(Fall(letters[i], 0.1f * i));
            yield break;
        }

        List<GameObject> Letters(string word)
        {
            var list = new List<GameObject>();
            float x0 = -(word.Length - 1) * letterSpawn.x * 0.5f;
            for (int i = 0; i < word.Length; i++)
            {
                var pos = transform.position + new Vector3(x0 + i * letterSpawn.x, letterSpawn.y, 0);
                var inst = Instantiate(letterPrefab, pos, Quaternion.identity);
                inst.name = "Light Letter " + word[i];
                var itm = inst.GetComponentInChildren<TextMesh>();
                if (itm) itm.text = word[i].ToString();
                list.Add(inst);
            }
            return list;
        }

        IEnumerator Fly(GameObject letter, Vector3 target, float delay, bool solid)
        {
            yield return new WaitForSeconds(delay);
            Vector3 a = letter.transform.position;
            Vector3 mid = (a + target) * 0.5f + Vector3.up * 2f;
            for (float t = 0; t < 1; t += Time.deltaTime / Mathf.Max(0.01f, letterFlyTime))
            {
                float e = Mathf.SmoothStep(0, 1, t);
                letter.transform.position = Vector3.Lerp(Vector3.Lerp(a, mid, e), Vector3.Lerp(mid, target, e), e);
                yield return null;
            }
            letter.transform.position = target;
            if (!solid) yield break;
            var plank = letter.transform.Find("Plank");
            if (plank) plank.gameObject.SetActive(true);
            var col = letter.AddComponent<BoxCollider2D>();
            col.size = letterSize;
            col.offset = new Vector2(0, plankOffsetY);
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
            if (questionText) questionText.text = panelQuestion;
            if (!canvas) { Debug.LogError("[WordAltar] choice panel is not assigned - use the Word Altar prefab.", this); return; }
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
        void OnValidate()
        {
            if (!string.IsNullOrEmpty(answer) && choices != null && System.Array.IndexOf(choices, answer) < 0)
                Debug.LogWarning($"[WordAltar] '{answer}' is not among the choices — the panel cannot solve it.", this);
            if (slots != null && !string.IsNullOrEmpty(answer) && slots.Length > 0 && slots.Length < answer.Length)
                Debug.LogWarning($"[WordAltar] {answer.Length} letters but {slots.Length} slots.", this);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.35f, 0.8f);
            Gizmos.DrawWireCube(transform.position, new Vector3(reach * 2, reachHeight * 2, 0));
            if (slots == null) return;
            foreach (var s in slots)
                if (s) Gizmos.DrawWireCube(s.position + new Vector3(0, plankOffsetY), letterSize);
        }
#endif

    }
}

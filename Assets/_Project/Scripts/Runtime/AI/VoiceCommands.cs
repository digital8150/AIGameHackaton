using System.Collections;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// What the twilight voice's words do in the world.
    ///  - Princess's sense: what you said rises as letters of light (기획서 "빛의 글자").
    ///  - Intent (Jev, keyword fallback) is executed only when confident; otherwise just "?" (오실행보다 덜 답답하다).
    /// Voice is always optional — every action also has a button.
    /// </summary>
    [RequireComponent(typeof(VoiceInput))]
    public class VoiceCommands : MonoBehaviour
    {
        public float executeThreshold = 0.6f;
        public Font letterFont;
        public Color letterColor = new Color(1f, 0.9f, 0.62f);

        public string LastDebug { get; private set; } = "";

        [Tooltip("Ask Jev which command / word a transcript meant. Off = Whisper (scene-word prompt) + keyword / jamo matching only — instant, no network.")]
        public bool useJev = false;

        void Start() { GetComponent<VoiceInput>().Transcribed += OnTranscribed; }

        async void OnTranscribed(string text)
        {
            var duo = DuoController.I;
            if (!duo) return;
            ShowLetters(text, duo.Leader.transform.position + Vector3.up * 1.9f);

            var altar = WordTarget.Nearest;
            if (altar && altar.AcceptsAnyText)
            {
                // blank bridges take any words: only obvious commands are treated as commands
                var k = VoiceIntent.Keywords(text, null);
                bool command = k.confidence >= executeThreshold && (k.intent == Intent.Switch || k.intent == Intent.Hint || k.intent == Intent.Jump);
                if (!command)
                {
                    LastDebug = $"\"{text}\" → 빈칸 채우기 · {VoiceInput.I.LastLatency:0.00}s";
                    altar.Speak(text);
                    return;
                }
            }
            var r = await VoiceIntent.JudgeAsync(text, altar ? altar.choices : null, useJev);
            if (!this || !DuoController.I) return;
            LastDebug = $"\"{text}\" → {r.intent}{(r.word != null ? " " + r.word : "")} ({r.confidence:P0}, {r.source}) · {VoiceInput.I.LastLatency:0.00}s";
            if (r.intent == Intent.Unrelated) return;
            if (r.confidence < executeThreshold) { GameEvents.Caption?.Invoke("?"); return; }

            switch (r.intent)
            {
                case Intent.Switch:
                    if (!duo.switchLocked) duo.Switch();
                    break;
                case Intent.Jump:
                    duo.Leader.RequestJump();
                    break;
                case Intent.Interact:
                    if (Lever.Nearest) Lever.Nearest.Trigger();
                    else if (altar) altar.Open();
                    break;
                case Intent.Hint:
                    if (AccessibilityDirector.I && AccessibilityDirector.I.isActiveAndEnabled) AccessibilityDirector.I.RequestHint();
                    else GameEvents.Hint?.Invoke("…둘이 함께라면, 길은 있어.");
                    break;
                case Intent.PuzzleWord:
                    if (altar && !string.IsNullOrEmpty(r.word)) altar.Speak(r.word);
                    break;
            }
        }

        void ShowLetters(string text, Vector3 at)
        {
            if (!letterFont || text.Length == 0) return;
            var go = new GameObject("Spoken Light");
            go.transform.position = at;
            var tm = go.AddComponent<TextMesh>();
            tm.font = letterFont;
            tm.text = text.Length > 18 ? text.Substring(0, 18) + "…" : text;
            tm.fontSize = 44;
            tm.characterSize = 0.1f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = letterColor;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = letterFont.material;
            mr.sortingOrder = 41;
            var st = go.AddComponent<SenseText>();
            st.lightAlpha = 1f; st.darkAlpha = 0f; st.shimmer = 0.1f;
            st.SetBaseColor(letterColor);
            StartCoroutine(Rise(go, st));
        }

        IEnumerator Rise(GameObject go, SenseText st)
        {
            for (float t = 0; t < 1; t += Time.deltaTime / 2.2f)
            {
                if (!go) yield break;
                go.transform.position += Vector3.up * Time.deltaTime * 0.5f;
                st.lightAlpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                yield return null;
            }
            if (go) Destroy(go);
        }
    }
}

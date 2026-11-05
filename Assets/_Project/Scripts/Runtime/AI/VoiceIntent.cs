using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DuskTwilight
{
    public enum Intent { Switch, Jump, Interact, Hint, PuzzleWord, Unrelated }

    public struct IntentResult
    {
        public Intent intent;
        public string word;       // for PuzzleWord: which altar word
        public float confidence;
        public string source;     // Jev | 키워드
    }

    /// <summary>
    /// 음성 의도 판정 (기획서 "서브: 음성 의도 판정"): Whisper text → Jev picks
    /// [전환, 점프, 상호작용, 힌트 요청, 퍼즐 단어, 무관]. Falls back to keyword matching offline.
    /// </summary>
    public static class VoiceIntent
    {
        static readonly Dictionary<string, Intent> Names = new Dictionary<string, Intent>
        {
            ["switch"] = Intent.Switch, ["jump"] = Intent.Jump, ["interact"] = Intent.Interact,
            ["hint"] = Intent.Hint, ["puzzle_word"] = Intent.PuzzleWord, ["unrelated"] = Intent.Unrelated
        };

        public static async Task<IntentResult> JudgeAsync(string transcript, string[] altarWords, bool useJev = true)
        {
            var jev = useJev ? await AskJev(transcript, altarWords) : null;
            return jev ?? Keywords(transcript, altarWords);
        }

        static async Task<IntentResult?> AskJev(string transcript, string[] altarWords)
        {
            if (!JevClient.HasKey) return null;
            var q = new Dictionary<string, JevClient.Choice>
            {
                ["intent"] = new JevClient.Choice
                {
                    instructions = "This is a Korean speech-recognition transcript of a player talking to a puzzle platformer (it may contain recognition errors). What does the player want the game to do?",
                    criteria = new Dictionary<string, string>
                    {
                        ["switch"] = "Swap the controlled character / sense, e.g. 바꿔, 전환, 교대, 왕자로, 공주로",
                        ["jump"] = "Jump, e.g. 점프, 뛰어, 올라가",
                        ["interact"] = "Use what is nearby: pull a lever, open, push, e.g. 당겨, 열어, 눌러",
                        ["hint"] = "Ask for help, e.g. 힌트, 도와줘, 모르겠어, 어떻게 해",
                        ["puzzle_word"] = altarWords != null && altarWords.Length > 0
                            ? "Calling out one of the carved words: " + string.Join(", ", altarWords)
                            : "Calling out a carved word (none is nearby)",
                        ["unrelated"] = "Talking, noise, or anything that is not a command"
                    }
                }
            };
            if (altarWords != null && altarWords.Length > 0)
            {
                var crit = altarWords.ToDictionary(w => w, w => $"The player said '{w}' (allow recognition errors and near-homophones)");
                crit["none"] = "None of these words was said";
                q["word"] = new JevClient.Choice { instructions = "Which of these words did the player say, if any?", criteria = crit };
            }
            var a = await JevClient.AskAsync(new { transcript, language = "ko" }, q);
            if (a == null || !a.TryGetValue("intent", out var intent) || !Names.TryGetValue(intent.choice ?? "", out var it)) return null;
            var r = new IntentResult { intent = it, confidence = intent.confidence, source = "Jev" };
            if (a.TryGetValue("word", out var w) && w.choice != "none")
            {
                r.word = w.choice;
                // a clearly recognised carved word wins even if the intent was judged otherwise
                if (w.confidence > intent.confidence && w.confidence >= 0.6f) { r.intent = Intent.PuzzleWord; r.confidence = w.confidence; }
            }
            if (r.intent == Intent.PuzzleWord && string.IsNullOrEmpty(r.word)) r.intent = Intent.Unrelated;
            return r;
        }

        // ------------------------------------------------------------------ offline fallback
        static readonly (Intent, string[])[] Words =
        {
            (Intent.Switch, new[] { "바꿔", "바꾸", "전환", "교대", "왕자", "공주" }),
            (Intent.Jump, new[] { "점프", "뛰어", "뛰", "올라" }),
            (Intent.Interact, new[] { "당겨", "당기", "열어", "눌러", "레버" }),
            (Intent.Hint, new[] { "힌트", "도와", "모르겠", "어떻게" }),
        };

        public static IntentResult Keywords(string transcript, string[] altarWords)
        {
            string t = (transcript ?? "").Replace(" ", "");
            if (altarWords != null)
            {
                foreach (var w in altarWords) if (t.Contains(w)) return new IntentResult { intent = Intent.PuzzleWord, word = w, confidence = 0.9f, source = "키워드" };
                // tolerate one wrong syllable (recognition noise) on short carved words
                var best = altarWords.Select(w => (w, d: Distance(t, w))).OrderBy(x => x.d).FirstOrDefault();
                if (best.w != null && best.d <= 1 && t.Length <= best.w.Length + 1)
                    return new IntentResult { intent = Intent.PuzzleWord, word = best.w, confidence = 0.7f, source = "키워드" };
                // sound-alike at the jamo level (해지연 ≈ 해질녘): Whisper base often slips a final consonant
                var jt = Jamo(t);
                var near = altarWords.Select(w => (w, r: Distance(jt, Jamo(w)) / (float)Jamo(w).Length)).OrderBy(x => x.r).First();
                if (t.Length <= near.w.Length + 1 && near.r <= 0.4f)
                    return new IntentResult { intent = Intent.PuzzleWord, word = near.w, confidence = 0.65f, source = "키워드" };
            }
            foreach (var (intent, keys) in Words)
                if (keys.Any(k => t.Contains(k))) return new IntentResult { intent = intent, confidence = 0.8f, source = "키워드" };
            return new IntentResult { intent = Intent.Unrelated, confidence = 0.5f, source = "키워드" };
        }

        /// <summary>Splits Hangul syllables into lead/vowel/tail jamo so near-homophones compare closely.</summary>
        public static string Jamo(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s)
            {
                int code = c - 0xAC00;
                if (code < 0 || code > 11171) { sb.Append(c); continue; }
                sb.Append((char)(0x1100 + code / 588)).Append((char)(0x1161 + code % 588 / 28));
                if (code % 28 != 0) sb.Append((char)(0x11A7 + code % 28));
            }
            return sb.ToString();
        }

        public static int Distance(string a, string b)
        {
            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                    d[i, j] = System.Math.Min(System.Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            return d[a.Length, b.Length];
        }
    }
}

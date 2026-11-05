using System.Collections.Generic;
using System.Threading.Tasks;

namespace DuskTwilight
{
    /// <summary>
    /// Judges how heavy the thing a spoken word names is: word list first, then this run's cache, then the local
    /// language model (<see cref="LocalLLM"/>). Anything that can't be judged is None (the letters scatter).
    /// Tested offline: Kanana 1.5 2.1B, 51/54 words right, ~45 ms each (Tools/ai_bench).
    /// </summary>
    public enum StretchKind { None, Long, Short }

    public static class WordJudge
    {
        public struct Result
        {
            public WordWeight weight;
            public float confidence;
            public string source;   // 사전 / 캐시 / AI / 폴백
        }

        const string System =
            "너는 동화 속 퍼즐의 심판이다. 플레이어가 말한 단어가 가리키는 것의 무게를 판정한다. " +
            "무거운 것이면 '무거움', 가벼운 것이면 '가벼움', 무겁지도 가볍지도 않거나 애매하면 '보통'이라고만 답한다. " +
            "추상적인 말은 그 말이 주는 느낌으로 판정한다.";

        // worked examples so the model uses every answer (none of them are puzzle words)
        static readonly (string, string)[] Shots =
        {
            ("단어: 새털", "가벼움"), ("단어: 돌덩이", "무거움"), ("단어: 숟가락", "보통"),
            ("단어: 공기", "가벼움"), ("단어: 트럭", "무거움"), ("단어: 모자", "보통"),
        };

        // three answers only: a fourth "don't know" option made the model unsure (94% → 83% in Tools/ai_bench)
        static readonly string[] Answers = { "무거움", "가벼움", "보통" };
        static readonly Dictionary<string, Result> cache = new Dictionary<string, Result>();

        public static float MinConfidence = 0.5f;
        public static float TimeoutSeconds = 4f;
        /// <summary>Jamo difference allowed for a near-miss of a listed word (0.35 = 2 of 6 jamo).</summary>
        public static float NearRatio = 0.35f;

        /// <summary>Ask once with the real instructions so the server has them cached (no result kept).</summary>
        public static async Task PrimeAsync()
        {
            var llm = LocalLLM.I;
            if (!llm) return;
            await llm.ChooseAsync(System, Shots, "단어: 의자", Answers, 30f);
            await llm.ChooseAsync(StretchSystem, StretchShots, "말: 더 커져라", StretchAnswers, 30f);
            await llm.ChooseAsync(HeavySystem, HeavyShots, "말: 묵직해져라", HeavyAnswers, 30f);
        }

        public static async Task<Result> JudgeWeightAsync(string spoken, WordLexicon lexicon)
        {
            string w = WordTarget.Clean(spoken);
            if (w.Length == 0 || (lexicon && lexicon.notThings.Contains(w))) return new Result { source = "말이 아님" };
            if (lexicon)
            {
                var lw = lexicon.Find(w, out _);
                if (lw != WordWeight.None) return new Result { weight = lw, confidence = 1f, source = "사전" };
                // a near-miss of a listed word (speech recognition) counts as that word
                var nw = lexicon.FindNear(w, NearRatio, out var near);
                if (nw != WordWeight.None) return new Result { weight = nw, confidence = 0.8f, source = "사전≈" + near };
            }
            if (cache.TryGetValue(w, out var hit)) { hit.source = "캐시"; return hit; }

            var llm = LocalLLM.I;
            if (llm && llm.Ready)
            {
                var a = await llm.ChooseAsync(System, Shots, "단어: " + spoken.Trim(), Answers, TimeoutSeconds);
                if (a.HasValue)
                {
                    var weight = a.Value.answer == "가벼움" ? WordWeight.Light : a.Value.answer == "무거움" ? WordWeight.Heavy
                               : a.Value.answer == "보통" ? WordWeight.Medium : WordWeight.None;
                    if (a.Value.probability < MinConfidence) weight = WordWeight.None;
                    var r = new Result { weight = weight, confidence = a.Value.probability, source = "AI" };
                    cache[w] = r;   // the same word always lands the same way
                    return r;
                }
            }
            return new Result { source = "폴백" };
        }
    

        // ------------------------------------------------------------------ 말로 길이 바꾸기 (gold objects)
        public struct StretchResult
        {
            public StretchKind stretch;
            public float confidence;
            public string source;
        }

        const string StretchSystem =
            "너는 동화 속 마법의 금색 물체다. 왕자가 너에게 한 말을 듣고, 길어지라는 뜻이면 '길게', 짧아지라는 뜻이면 '짧게', " +
            "그 밖의 말이면 '관계없음'이라고만 답한다. 말투나 어미가 장난스러워도(예: 길어져랏, 늘어나라구) 뜻으로 판단한다. " +
            "커져라·높아져라·자라라·올라가라·솟아라는 '길게', 작아져라·낮아져라·줄어라·내려가라는 '짧게'다.";

        static readonly (string, string)[] StretchShots =
        {
            ("말: 길어져", "길게"), ("말: 짧아져", "짧게"), ("말: 안녕", "관계없음"),
            ("말: 쭉쭉 늘어나라", "길게"), ("말: 조그맣게 줄어들어", "짧게"), ("말: 배고파", "관계없음"),
        };
        static readonly string[] StretchAnswers = { "길게", "짧게", "관계없음" };
        static readonly Dictionary<string, StretchResult> stretchCache = new Dictionary<string, StretchResult>();

        // obvious word pieces: no need to ask the model (instant, works without it)
        static readonly string[] LongPieces = { "길어", "길게", "길쭉", "늘어", "늘려", "늘이", "길다", "쭉", "커져", "커라", "크게", "높아", "높게", "높이", "자라", "키워", "올라", "올려", "솟아", "위로" };
        static readonly string[] ShortPieces = { "짧아", "짧게", "줄어", "줄여", "작아", "작게", "낮아", "낮게", "낮춰", "쪼그", "오므", "내려", "아래로" };

        public static async Task<StretchResult> JudgeStretchAsync(string spoken)
        {
            string w = WordTarget.Clean(spoken);
            if (w.Length == 0) return new StretchResult { source = "폴백" };
            if (LooksHeavy(spoken)) return new StretchResult { stretch = StretchKind.None, confidence = 1f, source = "단어" };
            bool l = false, s = false;
            foreach (var p in LongPieces) if (w.Contains(p)) l = true;
            foreach (var p in ShortPieces) if (w.Contains(p)) s = true;
            if (l != s) return new StretchResult { stretch = l ? StretchKind.Long : StretchKind.Short, confidence = 1f, source = "단어" };
            if (stretchCache.TryGetValue(w, out var hit)) { hit.source = "캐시"; return hit; }
            var llm = LocalLLM.I;
            if (llm && llm.Ready)
            {
                var a = await llm.ChooseAsync(StretchSystem, StretchShots, "말: " + spoken.Trim(), StretchAnswers, TimeoutSeconds);
                if (a.HasValue)
                {
                    var k = a.Value.answer == "길게" ? StretchKind.Long : a.Value.answer == "짧게" ? StretchKind.Short : StretchKind.None;
                    if (a.Value.probability < MinConfidence) k = StretchKind.None;
                    var r = new StretchResult { stretch = k, confidence = a.Value.probability, source = "AI" };
                    stretchCache[w] = r;
                    return r;
                }
            }
            return new StretchResult { source = "폴백" };
        }

        // ------------------------------------------------------------------ 무거워져 (a floating gold stone)
        const string HeavySystem =
            "너는 동화 속 공중에 떠 있는 마법의 금색 돌이다. 왕자가 너에게 한 말을 듣고, 무거워지라거나 떨어지라거나 내려오라는 뜻이면 '무겁게', " +
            "그 밖의 말이면 '관계없음'이라고만 답한다. 말투나 어미가 장난스러워도(예: 무거워져랏, 떨어지라구) 뜻으로 판단한다.";

        static readonly (string, string)[] HeavyShots =
        {
            ("말: 무거워져", "무겁게"), ("말: 안녕", "관계없음"), ("말: 쿵 하고 떨어져라", "무겁게"),
            ("말: 배고파", "관계없음"), ("말: 아래로 내려와", "무겁게"), ("말: 길어져", "관계없음"),
        };
        static readonly string[] HeavyAnswers = { "무겁게", "관계없음" };
        static readonly Dictionary<string, StretchResult> heavyCache = new Dictionary<string, StretchResult>();
        static readonly string[] HeavyPieces = { "무거", "무겁", "묵직", "떨어", "내려", "가라앉", "추락", "아래로", "쿵" };

        static readonly string[] HeavyOnlyPieces = { "무거", "무겁", "묵직", "떨어", "가라앉", "추락", "쿵", "내려와", "내려오" };

        /// <summary>Clearly about weight / falling (not length).</summary>
        public static bool LooksHeavy(string spoken)
        {
            string w = WordTarget.Clean(spoken);
            foreach (var p in HeavyOnlyPieces) if (w.Contains(p)) return true;
            return false;
        }

        /// <summary>Clearly about length (and not about weight).</summary>
        public static bool LooksStretch(string spoken)
        {
            if (LooksHeavy(spoken)) return false;
            string w = WordTarget.Clean(spoken);
            foreach (var p in LongPieces) if (w.Contains(p)) return true;
            foreach (var p in ShortPieces) if (p != "내려" && p != "아래로" && w.Contains(p)) return true;
            return false;
        }

        /// <summary>Heavy → <see cref="StretchKind.Short"/> (the stone drops), otherwise None.</summary>
        public static async Task<StretchResult> JudgeHeavyAsync(string spoken)
        {
            string w = WordTarget.Clean(spoken);
            if (w.Length == 0) return new StretchResult { source = "폴백" };
            foreach (var p in HeavyPieces) if (w.Contains(p)) return new StretchResult { stretch = StretchKind.Short, confidence = 1f, source = "단어" };
            if (heavyCache.TryGetValue(w, out var hit)) { hit.source = "캐시"; return hit; }
            var llm = LocalLLM.I;
            if (llm && llm.Ready)
            {
                var a = await llm.ChooseAsync(HeavySystem, HeavyShots, "말: " + spoken.Trim(), HeavyAnswers, TimeoutSeconds);
                if (a.HasValue)
                {
                    var k = a.Value.answer == "무겁게" && a.Value.probability >= MinConfidence ? StretchKind.Short : StretchKind.None;
                    var r = new StretchResult { stretch = k, confidence = a.Value.probability, source = "AI" };
                    heavyCache[w] = r;
                    return r;
                }
            }
            return new StretchResult { source = "폴백" };
        }
    }
}

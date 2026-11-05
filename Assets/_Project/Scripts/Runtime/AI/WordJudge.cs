using System.Collections.Generic;
using System.Threading.Tasks;

namespace DuskTwilight
{
    /// <summary>
    /// Judges how heavy the thing a spoken word names is: word list first, then this run's cache, then the local
    /// language model (<see cref="LocalLLM"/>). Anything that can't be judged is None (the letters scatter).
    /// Tested offline: Kanana 1.5 2.1B, 51/54 words right, ~45 ms each (Tools/ai_bench).
    /// </summary>
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

        /// <summary>Ask once with the real instructions so the server has them cached (no result kept).</summary>
        public static async Task PrimeAsync()
        {
            var llm = LocalLLM.I;
            if (llm) await llm.ChooseAsync(System, Shots, "단어: 의자", Answers, 30f);
        }

        public static async Task<Result> JudgeWeightAsync(string spoken, WordLexicon lexicon)
        {
            string w = WordTarget.Clean(spoken);
            if (w.Length == 0 || (lexicon && lexicon.notThings.Contains(w))) return new Result { source = "말이 아님" };
            if (lexicon)
            {
                var lw = lexicon.Find(w, out _);
                if (lw != WordWeight.None) return new Result { weight = lw, confidence = 1f, source = "사전" };
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
    }
}

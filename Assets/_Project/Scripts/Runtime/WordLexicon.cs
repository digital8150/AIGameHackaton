using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuskTwilight
{
    /// <summary>
    /// Word → weight list used before (and instead of) the language model: fixed answers for the puzzle's own words
    /// and for words the model gets wrong. A spoken phrase matches an entry it contains ("깃털이", "가벼운 깃털").
    /// </summary>
    [CreateAssetMenu(menuName = "Dusk Twilight/Word Lexicon")]
    public class WordLexicon : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string word;
            public WordWeight weight;
        }

        public List<Entry> entries = new List<Entry>();
        [Tooltip("Greetings, fillers, replies: not things, so they scatter without asking the model")]
        public List<string> notThings = new List<string>();

        static readonly string[] Particles = { "", "이", "가", "을", "를", "은", "는", "도", "야", "아" };

        /// <summary>
        /// The weight of the longest entry contained in <paramref name="cleaned"/>, or None. One-letter entries (돌, 연, 쇠)
        /// only match the whole word, optionally with a particle — "연필" is not "연".
        /// </summary>
        public WordWeight Find(string cleaned, out string matched)
        {
            matched = null;
            var best = WordWeight.None;
            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.word)) continue;
                if (e.word.Length == 1)
                {
                    bool whole = false;
                    foreach (var p in Particles) if (cleaned == e.word + p) whole = true;
                    if (!whole) continue;
                }
                else if (!cleaned.Contains(e.word)) continue;
                if (matched == null || e.word.Length > matched.Length) { matched = e.word; best = e.weight; }
            }
            return best;
        }

        /// <summary>
        /// Speech recognition near-miss ("기탈" for 깃털, "알맥이" for 갈매기): the entry whose jamo are closest, if the
        /// difference is at most <paramref name="maxRatio"/> of the entry's jamo. Entries of two or more letters only.
        /// </summary>
        public WordWeight FindNear(string cleaned, float maxRatio, out string matched)
        {
            matched = null;
            var best = WordWeight.None;
            string jw = VoiceIntent.Jamo(cleaned);
            float bestR = float.MaxValue;
            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.word) || e.word.Length < 2) continue;
                string je = VoiceIntent.Jamo(e.word);
                float r = VoiceIntent.Distance(jw, je) / (float)je.Length;
                if (r <= maxRatio && r < bestR) { bestR = r; best = e.weight; matched = e.word; }
            }
            return best;
        }

        public IEnumerable<string> Words { get { foreach (var e in entries) yield return e.word; } }
    }
}

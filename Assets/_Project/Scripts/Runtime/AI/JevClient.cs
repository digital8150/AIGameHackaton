using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace DuskTwilight
{
    /// <summary>
    /// Minimal HTTP client for TypeSafe Jev (judgement model: picks one of fixed options, with confidence).
    /// Follows the official API (docs.typesafe.ai):
    ///   POST https://api.typesafe.ai/v1/systemone
    ///   Authorization: Bearer &lt;key&gt;; body { model, state, questions: { id: { type:"choice", instructions, criteria:{name:desc} } } }
    ///   → { model, answers: { id: { type, choice, confidence, probabilities } }, usage }
    /// Key: env TYPESAFE_API_KEY (the SDKs' default), or a one-line file at persistentDataPath/typesafe_api_key.txt.
    /// No key / offline / slow → callers fall back to rules (design: "Jev가 응답하지 않아도 폴백으로 계속 돌아간다").
    /// </summary>
    public static class JevClient
    {
        public const string Endpoint = "https://api.typesafe.ai/v1/systemone";
        public static string Model = "jev-latest";
        public static int TimeoutSeconds = 4;

        static string key;
        static bool keyLoaded;
        public static string LastStatus { get; private set; } = "idle";

        public struct Choice
        {
            public string instructions;
            public Dictionary<string, string> criteria; // option name → description
        }

        public struct Answer
        {
            public string choice;
            public float confidence;
            public Dictionary<string, float> probabilities;
        }

        public static bool HasKey
        {
            get
            {
                if (!keyLoaded)
                {
                    keyLoaded = true;
                    key = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY");
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        var path = Path.Combine(Application.persistentDataPath, "typesafe_api_key.txt");
                        if (File.Exists(path)) key = File.ReadAllText(path).Trim();
                    }
                }
                return !string.IsNullOrWhiteSpace(key);
            }
        }

        /// <summary>Asks several choice questions about one state in a single call (evaluated in parallel server-side).
        /// Returns null on any failure — never throws into gameplay.</summary>
        public static async Task<Dictionary<string, Answer>> AskAsync(object state, Dictionary<string, Choice> questions)
        {
            if (!HasKey) { LastStatus = "no key"; return null; }
            var qs = new JObject();
            foreach (var kv in questions)
            {
                var crit = new JObject();
                foreach (var c in kv.Value.criteria) crit[c.Key] = c.Value;
                qs[kv.Key] = new JObject { ["type"] = "choice", ["instructions"] = kv.Value.instructions, ["criteria"] = crit };
            }
            var body = new JObject { ["model"] = Model, ["state"] = state is string s ? (JToken)s : JToken.FromObject(state), ["questions"] = qs };

            for (int attempt = 0; attempt < 2; attempt++)
            {
                using var req = new UnityWebRequest(Endpoint, "POST");
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Formatting.None)));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", "Bearer " + key);
                req.timeout = TimeoutSeconds;
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var json = JObject.Parse(req.downloadHandler.text);
                        var result = new Dictionary<string, Answer>();
                        foreach (var a in (JObject)json["answers"])
                        {
                            var probs = new Dictionary<string, float>();
                            if (a.Value["probabilities"] is JObject p)
                                foreach (var kv in p) probs[kv.Key] = kv.Value.Value<float>();
                            result[a.Key] = new Answer
                            {
                                choice = a.Value.Value<string>("choice"),
                                confidence = a.Value.Value<float>("confidence"),
                                probabilities = probs
                            };
                        }
                        LastStatus = "ok " + json.Value<string>("model");
                        return result;
                    }
                    catch (Exception e) { LastStatus = "parse error"; Debug.LogWarning("[Jev] bad response: " + e.Message); return null; }
                }

                long code = req.responseCode;
                LastStatus = code > 0 ? $"http {code}" : req.error;
                // 429 rate limit / 529 overloaded: back off once (docs: exponential backoff, honour retry-after)
                if ((code == 429 || code == 529) && attempt == 0)
                {
                    float wait = 0.6f;
                    var ra = req.GetResponseHeader("retry-after");
                    if (float.TryParse(ra, out var sec)) wait = Mathf.Min(sec, 2f);
                    await Task.Delay(TimeSpan.FromSeconds(wait));
                    continue;
                }
                if (code == 401) Debug.LogWarning("[Jev] 401: invalid TYPESAFE_API_KEY — using rule fallback.");
                return null;
            }
            return null;
        }
    }
}

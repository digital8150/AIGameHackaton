using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DuskTwilight
{
    /// <summary>
    /// A small language model running on this machine (llama.cpp server + Kanana 1.5 2.1B, Apache-2.0) — no key,
    /// no network. The game starts the server next to itself and asks it to pick one of a few fixed answers
    /// (a grammar forces the answer; the probability of the first token is the confidence).
    /// Files live in StreamingAssets/LLM (git-ignored, Tools/download_assets.bat). Missing files → Ready stays false
    /// and callers fall back to their word lists.
    /// </summary>
    public class LocalLLM : MonoBehaviour
    {
        public static LocalLLM I { get; private set; }

        [Header("Server (paths relative to StreamingAssets)")]
        public string serverExe = "LLM/llama-server.exe";
        public string modelFile = "LLM/kanana-2.1b-Q4_K_M.gguf";
        public int port = 18091;
        [Tooltip("Layers on the GPU (99 = all)")]
        public int gpuLayers = 99;
        public int contextSize = 2048;
        public bool startOnAwake = true;
        [Tooltip("Seconds to wait for the model to load")]
        public float startTimeout = 40f;

        public bool Ready { get; private set; }
        public string Status { get; private set; } = "off";
        public float LastLatency { get; private set; }

        static readonly HttpClient http = new HttpClient();
        Process proc;
        string Base => $"http://127.0.0.1:{port}";

        void Awake()
        {
            if (I && I != this) { Destroy(this); return; }
            I = this;
            if (startOnAwake) StartServer();
        }

        public async void StartServer()
        {
            if (Ready || proc != null) return;
            if (await Healthy()) { await WarmUp(); return; }
            string exe = Path.Combine(Application.streamingAssetsPath, serverExe);
            string model = Path.Combine(Application.streamingAssetsPath, modelFile);
            if (!File.Exists(exe) || !File.Exists(model)) { Status = "no model files (run Tools/download_assets.bat)"; return; }
            try
            {
                var psi = new ProcessStartInfo(exe,
                    $"-m \"{model}\" -ngl {gpuLayers} -c {contextSize} --port {port} --host 127.0.0.1 -fa off --log-disable")
                {
                    UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(exe),
                };
                proc = Process.Start(psi);
                Status = "loading";
            }
            catch (Exception e) { Status = "start failed: " + e.Message; Debug.LogWarning("[LocalLLM] " + Status); return; }
            float until = Time.realtimeSinceStartup + startTimeout;
            while (this && Time.realtimeSinceStartup < until)
            {
                if (await Healthy()) { await WarmUp(); return; }
                if (proc == null || proc.HasExited) { Status = "server exited"; return; }
                await Task.Delay(250);
            }
            if (!Ready) Status = "load timeout";
        }

        // the first requests after loading are slow (GPU shaders compile): ask twice before telling anyone we're ready
        async Task WarmUp()
        {
            Status = "warming up";
            Ready = true;
            // with the real question, so its long instructions are already cached for the first player word
            for (int i = 0; i < 2; i++) await WordJudge.PrimeAsync();
            Status = "ready";
            Ready = true;
        }

        async Task<bool> Healthy()
        {
            try
            {
                using var cts = new CancellationTokenSource(500);
                var r = await http.GetAsync(Base + "/health", cts.Token);
                return r.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        /// <summary>
        /// Ask the model to answer with exactly one of <paramref name="answers"/>. Returns null when the model is not
        /// ready, too slow or fails. <paramref name="shots"/> are worked examples (user text, answer).
        /// </summary>
        public async Task<(string answer, float probability)?> ChooseAsync(string system, (string user, string answer)[] shots,
                                                                            string user, string[] answers, float timeoutSeconds)
        {
            if (!Ready) return null;
            var msgs = new JArray { new JObject { ["role"] = "system", ["content"] = system } };
            if (shots != null)
                foreach (var (u, a) in shots)
                {
                    msgs.Add(new JObject { ["role"] = "user", ["content"] = u });
                    msgs.Add(new JObject { ["role"] = "assistant", ["content"] = a });
                }
            msgs.Add(new JObject { ["role"] = "user", ["content"] = user });
            var sb = new StringBuilder("root ::= ");
            for (int i = 0; i < answers.Length; i++) sb.Append(i > 0 ? " | " : "").Append('"').Append(answers[i]).Append('"');
            var body = new JObject
            {
                ["messages"] = msgs, ["max_tokens"] = 8, ["temperature"] = 0, ["grammar"] = sb.ToString(),
                ["logprobs"] = true, ["top_logprobs"] = 1,
            };
            float t0 = Time.realtimeSinceStartup;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                var res = await http.PostAsync(Base + "/v1/chat/completions",
                    new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json"), cts.Token);
                var json = JObject.Parse(await res.Content.ReadAsStringAsync());
                LastLatency = Time.realtimeSinceStartup - t0;
                var choice = json["choices"]?[0];
                string text = (string)choice?["message"]?["content"] ?? "";
                text = text.Replace("<|eot_id|>", "").Trim();
                float p = 1f;
                var lp = choice?["logprobs"]?["content"]?[0]?["logprob"];
                if (lp != null) p = Mathf.Exp((float)lp);
                return Array.IndexOf(answers, text) >= 0 ? (text, p) : ((string, float)?)null;
            }
            catch (Exception e)
            {
                LastLatency = Time.realtimeSinceStartup - t0;
                Status = "ask failed: " + e.GetType().Name;
                return null;
            }
        }

        void OnDestroy() => Stop();
        void OnApplicationQuit() => Stop();

        void Stop()
        {
            Ready = false;
            try { if (proc != null && !proc.HasExited) proc.Kill(); } catch { }
            proc = null;
            if (I == this) I = null;
        }
    }
}

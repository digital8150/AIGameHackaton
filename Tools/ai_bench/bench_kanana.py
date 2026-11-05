"""Heavy / light word judge with a local LLM served by llama.cpp (llama-server), standard library only.

Start the server first (GPU, Vulkan build):
  Tools\\_models\\llama\\llama-server.exe -m Tools\\_models\\kanana\\kanana-2.1b-Q4_K_M.gguf -ngl 99 -c 2048 --port 8091
Then:
  python -I Tools/ai_bench/bench_kanana.py
The answer is forced by a grammar to one of three labels; confidence comes from the token probabilities.
"""
import json, os, sys, time, urllib.request

URL = "http://127.0.0.1:8091/v1/chat/completions"
HERE = os.path.dirname(os.path.abspath(__file__))
LABELS = {"무거움": "heavy", "가벼움": "light", "보통": "mid"}
SYSTEM = ("너는 동화 속 퍼즐의 심판이다. 플레이어가 말한 단어가 가리키는 것의 무게를 판정한다. "
          "무거운 것이면 '무거움', 가벼운 것이면 '가벼움', 무겁지도 가볍지도 않거나 애매하면 '보통'이라고만 답한다. "
          "추상적인 말은 그 말이 주는 느낌으로 판정한다.")
GRAMMAR = 'root ::= "무거움" | "가벼움" | "보통"'


def ask(word):
    body = {"messages": [{"role": "system", "content": SYSTEM}, {"role": "user", "content": f"단어: {word}"}],
            "max_tokens": 6, "temperature": 0, "grammar": GRAMMAR, "logprobs": True, "top_logprobs": 3}
    req = urllib.request.Request(URL, data=json.dumps(body).encode(), headers={"Content-Type": "application/json"})
    t0 = time.perf_counter()
    d = json.load(urllib.request.urlopen(req, timeout=30))
    dt = time.perf_counter() - t0
    text = d["choices"][0]["message"]["content"].strip()
    conf = None
    try:
        import math
        first = d["choices"][0]["logprobs"]["content"][0]
        conf = math.exp(first["logprob"])
    except Exception:
        pass
    return LABELS.get(text, text), conf, dt


def main():
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    rows = [l.rstrip("\n").split("\t") for l in open(os.path.join(HERE, "words.tsv"), encoding="utf-8") if l.strip() and not l.startswith("#")]
    ask("깃털")  # warm-up
    ok = total = 0
    times = []
    for word, exp in rows:
        got, conf, dt = ask(word)
        times.append(dt)
        mark = "" if exp == "?" else ("O" if got == exp else "X")
        if exp != "?":
            total += 1; ok += got == exp
        print(f"{mark:1} {word:10} → {got:6} {'' if conf is None else f'{conf:.0%}':>5}  {dt*1000:5.0f}ms  (expected {exp})")
    times.sort()
    print(f"\naccuracy {ok}/{total} = {ok/total:.0%} · median {times[len(times)//2]*1000:.0f}ms · max {times[-1]*1000:.0f}ms")


if __name__ == "__main__":
    main()

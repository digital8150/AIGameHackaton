"""Riddle altar judge (fits / near / wrong) with the local Kanana server — quick feasibility check."""
import json, math, sys, time, urllib.request
URL = "http://127.0.0.1:8091/v1/chat/completions"
SYSTEM = ("너는 동화 속 수수께끼 제단이다. 수수께끼의 뜻에 플레이어의 대답이 맞으면 '맞음', 관련은 있지만 맞지 않으면 '가까움', "
          "맞지 않거나 단어가 아니면 '틀림'이라고만 답한다. 동화 속 상식으로 너그럽게 판단한다.")
LAB = {"맞음": "fits", "가까움": "near", "틀림": "wrong"}
SHOTS = [("하늘에서 떨어지는 차가운 것", "눈", "맞음"), ("하늘에서 떨어지는 차가운 것", "겨울", "가까움"), ("하늘에서 떨어지는 차가운 것", "의자", "틀림")]
CASES = [("밤이 오면 사라지는 것", w, e) for w, e in [("노을", "fits"), ("해", "fits"), ("그림자", "fits"), ("햇빛", "fits"), ("무지개", "fits"), ("별", "near"), ("달", "near"), ("의자", "wrong"), ("고양이", "wrong")]] + \
        [("물에 비치지만 잡히지 않는 것", w, e) for w, e in [("달", "fits"), ("별", "fits"), ("하늘", "fits"), ("노을", "fits"), ("물고기", "wrong"), ("돌", "wrong"), ("거울", "near")]] + \
        [("두 사람을 이어 주는 것", w, e) for w, e in [("다리", "fits"), ("손", "fits"), ("약속", "fits"), ("편지", "fits"), ("노래", "fits"), ("바위", "wrong"), ("이별", "near")]]
def ask(riddle, word):
    msgs = [{"role": "system", "content": SYSTEM}]
    for r, w, a in SHOTS: msgs += [{"role": "user", "content": f"수수께끼: {r}\n대답: {w}"}, {"role": "assistant", "content": a}]
    msgs.append({"role": "user", "content": f"수수께끼: {riddle}\n대답: {word}"})
    body = {"messages": msgs, "max_tokens": 6, "temperature": 0, "grammar": 'root ::= "맞음" | "가까움" | "틀림"', "logprobs": True}
    t0 = time.perf_counter(); d = json.load(urllib.request.urlopen(urllib.request.Request(URL, data=json.dumps(body).encode(), headers={"Content-Type": "application/json"}), timeout=30))
    t = d["choices"][0]["message"]["content"].replace("<|eot_id|>", "").strip()
    return LAB.get(t, t), math.exp(d["choices"][0]["logprobs"]["content"][0]["logprob"]), time.perf_counter() - t0
sys.stdout.reconfigure(encoding="utf-8"); ok = 0
for r, w, e in CASES:
    g, c, dt = ask(r, w); ok += g == e
    print(f"{'O' if g == e else 'X'} [{r}] {w:5} → {g:5} {c:.0%} {dt*1000:.0f}ms (expected {e})")
print(f"accuracy {ok}/{len(CASES)} = {ok/len(CASES):.0%}")

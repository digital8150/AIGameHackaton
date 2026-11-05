"""Heavy / light word judge with a small multilingual sentence-embedding model (multilingual-e5-small, ONNX, CPU).

Needs: pip install onnxruntime tokenizers   (numpy is already there)
Model files (git-ignored): Tools/_models/e5/model_quantized.onnx, tokenizer.json
  python -I Tools/ai_bench/bench_e5.py

How it judges: the word's embedding is compared with example words that are clearly heavy and clearly light
(none of them are in the test list). Closer to the heavy examples → heavy; too close to call → mid.
"""
import os, sys, time
import numpy as np
import onnxruntime as ort
from tokenizers import Tokenizer

HERE = os.path.dirname(os.path.abspath(__file__))
MODEL = os.path.join(HERE, "..", "_models", "e5")
HEAVY = ["돌덩이", "무쇠", "강철", "바윗덩어리", "트럭", "코뿔소", "쇠사슬", "대리석", "건물", "배"]
LIGHT = ["새털", "공기", "물방울", "잎사귀", "솜털", "실", "구슬", "햇살", "향기", "휴지"]
MARGIN = 0.004  # |score| below this = mid

tok = Tokenizer.from_file(os.path.join(MODEL, "tokenizer.json"))
sess = ort.InferenceSession(os.path.join(MODEL, "model_quantized.onnx"), providers=["CPUExecutionProvider"])
inputs = {i.name for i in sess.get_inputs()}


def embed(texts):
    enc = tok.encode_batch([f"query: {t}" for t in texts])
    n = max(len(e.ids) for e in enc)
    ids = np.array([e.ids + [0] * (n - len(e.ids)) for e in enc], dtype=np.int64)
    mask = np.array([[1] * len(e.ids) + [0] * (n - len(e.ids)) for e in enc], dtype=np.int64)
    feed = {"input_ids": ids, "attention_mask": mask}
    if "token_type_ids" in inputs:
        feed["token_type_ids"] = np.zeros_like(ids)
    out = sess.run(None, feed)[0]
    v = (out * mask[..., None]).sum(1) / mask.sum(1, keepdims=True)
    return v / np.linalg.norm(v, axis=1, keepdims=True)


def main():
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    h, l = embed(HEAVY), embed(LIGHT)
    rows = [x.rstrip("\n").split("\t") for x in open(os.path.join(HERE, "words.tsv"), encoding="utf-8") if x.strip() and not x.startswith("#")]
    ok = total = 0
    times = []
    for word, exp in rows:
        t0 = time.perf_counter()
        v = embed([word])[0]
        score = float((h @ v).mean() - (l @ v).mean())
        dt = time.perf_counter() - t0
        times.append(dt)
        got = "mid" if abs(score) < MARGIN else ("heavy" if score > 0 else "light")
        mark = "" if exp == "?" else ("O" if got == exp else "X")
        if exp != "?":
            total += 1; ok += got == exp
        print(f"{mark:1} {word:10} → {got:6} {score:+.4f}  {dt*1000:5.1f}ms  (expected {exp})")
    times.sort()
    print(f"\naccuracy {ok}/{total} = {ok/total:.0%} · median {times[len(times)//2]*1000:.1f}ms")


if __name__ == "__main__":
    main()

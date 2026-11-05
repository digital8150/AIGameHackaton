"""NovelAI V5 environment-asset generator (outside the editor, like gen_characters.py).

Prompt = preset prompt + [transparent background] + "Art Prompts/[Base Prompts].txt"
Negative = preset negative + "Art Prompts/[Negative Prompts].txt"
Presets live in Tools/nai_env_presets.json.

Usage (token from env NAI_API_KEY, never written to disk):
  python -I Tools/gen_env_nai.py --list
  python -I Tools/gen_env_nai.py fg_grass mid_tree --n 2 [--seed 123]

Output: Tools/_nai_out/<preset>/<preset>_<seed>.png (raw RGBA from NAI)
        Tools/_nai_out/<preset>/<preset>_<seed>_clean.png (alpha fringe cleaned + trimmed)
        Tools/_nai_out/<preset>/<preset>_<seed>.json (exact request minus token)
Curate, then copy the chosen *_clean.png into Assets/_Project/Art/Env/.
"""
import argparse, io, json, os, random, sys, time, urllib.error, urllib.request, zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PRESETS = os.path.join(ROOT, "Tools", "nai_env_presets.json")
BASE = os.path.join(ROOT, "Art Prompts", "[Base Prompts].txt")
NEG = os.path.join(ROOT, "Art Prompts", "[Negative Prompts].txt")
OUT = os.path.join(ROOT, "Tools", "_nai_out")
URL = "https://image.novelai.net/ai/generate-image"
UA = "AIGameHackaton-asset-tool/0.1"  # urllib's default UA is rejected by Cloudflare (1010)
MAX_FREE_PIXELS = 1024 * 1024        # Opus free-generation limit


def read(path):
    with open(path, encoding="utf-8") as f:
        return f.read().strip().rstrip(",")


def join(*parts):
    return ", ".join(p.strip().strip(",") for p in parts if p and p.strip())


def build(preset, seed, base, neg):
    transparent = preset.get("transparent", True)
    prompt = join(preset["prompt"],
                  "2.0::transparent background::" if transparent else "",
                  base)
    negative = join(preset.get("negative", ""), neg)
    w, h = preset.get("size", [1024, 1024])
    assert w % 64 == 0 and h % 64 == 0, "size must be multiples of 64"
    if w * h > MAX_FREE_PIXELS:
        print(f"  ! {w}x{h} exceeds the free-generation pixel budget (costs Anlas)")
    params = {
        "params_version": 3,
        "width": w, "height": h,
        "scale": preset.get("scale", 5.0),
        "sampler": "k_euler_ancestral",
        "steps": preset.get("steps", 23),
        "n_samples": 1,
        "seed": seed,
        "noise_schedule": "karras",
        "negative_prompt": negative,
        "v4_prompt": {"caption": {"base_caption": prompt, "char_captions": []},
                      "use_coords": False, "use_order": True},
        "v4_negative_prompt": {"caption": {"base_caption": negative, "char_captions": []},
                               "legacy_uc": False},
    }
    if transparent:
        # V5 web-client behaviour: the tag alone or these flags alone are not enough.
        params["straight_alpha"] = True
        params["tag_hint_transparent_background"] = True
    return {"input": prompt, "model": preset.get("model", "nai-diffusion-5-full"),
            "action": "generate", "parameters": params}


def request(body, token):
    req = urllib.request.Request(URL, data=json.dumps(body).encode(), headers={
        "Authorization": f"Bearer {token}",
        "Content-Type": "application/json",
        "User-Agent": UA,
    })
    for attempt in range(4):
        try:
            data = urllib.request.urlopen(req, timeout=240).read()
            with zipfile.ZipFile(io.BytesIO(data)) as z:
                return z.read(z.namelist()[0])
        except urllib.error.HTTPError as e:
            msg = e.read().decode(errors="replace")[:500]
            if e.code == 429 and attempt < 3:
                print("  429 busy, retrying...")
                time.sleep(5 * (attempt + 1))
                continue
            raise SystemExit(f"HTTP {e.code}: {msg}")


def clean(png_bytes, alpha_floor, pad):
    """Kill faint alpha noise, zero RGB under full transparency, trim to content."""
    from PIL import Image
    im = Image.open(io.BytesIO(png_bytes)).convert("RGBA")
    r, g, b, a = im.split()
    a = a.point(lambda v: 0 if v < alpha_floor else v)
    mask = a.point(lambda v: 255 if v > 0 else 0)
    black = Image.new("L", im.size, 0)
    r, g, b = (Image.composite(c, black, mask) for c in (r, g, b))
    im = Image.merge("RGBA", (r, g, b, a))
    box = a.getbbox()
    if box:
        x0, y0, x1, y1 = box
        im = im.crop((max(0, x0 - pad), max(0, y0 - pad),
                      min(im.width, x1 + pad), min(im.height, y1 + pad)))
    stats = a.histogram()
    total = sum(stats)
    return im, stats[0] / total


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("presets", nargs="*")
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--n", type=int, default=1, help="images per preset")
    ap.add_argument("--seed", type=int, help="first seed (then +1 per image)")
    ap.add_argument("--alpha-floor", type=int, default=24)
    ap.add_argument("--pad", type=int, default=4)
    args = ap.parse_args()

    with open(PRESETS, encoding="utf-8") as f:
        presets = json.load(f)["presets"]
    if args.list or not args.presets:
        for k, p in presets.items():
            print(f"{k:22} {p.get('size', [1024, 1024])}  {p.get('note', '')}")
        return

    token = os.environ.get("NAI_API_KEY")
    if not token:
        raise SystemExit("NAI_API_KEY is not set")
    base, neg = read(BASE), read(NEG)

    for name in args.presets:
        preset = presets[name]
        os.makedirs(os.path.join(OUT, name), exist_ok=True)
        for i in range(args.n):
            seed = (args.seed + i) if args.seed is not None else random.randint(1, 2**32 - 1)
            body = build(preset, seed, base, neg)
            print(f"{name} seed={seed} ...", flush=True)
            png = request(body, token)
            stem = os.path.join(OUT, name, f"{name}_{seed}")
            with open(stem + ".png", "wb") as f:
                f.write(png)
            with open(stem + ".json", "w", encoding="utf-8") as f:
                json.dump(body, f, ensure_ascii=False, indent=1)
            if preset.get("transparent", True):
                im, clear = clean(png, args.alpha_floor, args.pad)
                im.save(stem + "_clean.png")
                print(f"  -> {stem}_clean.png {im.size} transparent={clear:.0%}")
            else:
                print(f"  -> {stem}.png")


if __name__ == "__main__":
    main()

"""NovelAI V5 asset generator (outside the editor, like gen_characters.py).

The prompts are NOT stored here. They are read straight from "Art Prompts/":
  "[...] NN_*.txt"         one prompt per "## N." / "### N-A." section
  "[Base Prompts].txt"     appended to every prompt
  "[Negative Prompts].txt" used as the negative prompt
The only change made to a section prompt: "{{black background, simple background}}" /
"on a solid black background" become a real transparent background (V5 alpha output).

Size comes from an aspect hint in the section header (or its parent "##" header):
  16:9, 21:9, 3:2, 1:1, 2:3, 9:16, 9:21   (default 1:1; override with --size WxH)

Usage (token from env NAI_API_KEY, never written to disk):
  python -I Tools/gen_env_nai.py --list
  python -I Tools/gen_env_nai.py 04:1-B 05:1-A --n 2 [--seed 123] [--size 832x1216]

Output: Tools/_nai_out/<key>/<key>_<seed>.png        raw image from NAI
        Tools/_nai_out/<key>/<key>_<seed>_clean.png  alpha fringe cleaned + trimmed (transparent only)
        Tools/_nai_out/<key>/<key>_<seed>.json       exact request (no token)
Curate, then copy the chosen file into Assets/_Project/Art/Env/.
"""
import argparse, glob, io, json, os, random, re, sys, time, urllib.error, urllib.request, zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROMPT_DIR = os.path.join(ROOT, "Art Prompts")
BASE = os.path.join(PROMPT_DIR, "[Base Prompts].txt")
NEG = os.path.join(PROMPT_DIR, "[Negative Prompts].txt")
OUT = os.path.join(ROOT, "Tools", "_nai_out")
URL = "https://image.novelai.net/ai/generate-image"
UA = "AIGameHackaton-asset-tool/0.1"  # urllib's default UA is rejected by Cloudflare (1010)
MAX_FREE_PIXELS = 1024 * 1024        # Opus free-generation limit

ASPECTS = {"21:9": (1472, 704), "16:9": (1344, 768), "3:2": (1216, 832), "1:1": (1024, 1024),
           "2:3": (832, 1216), "9:16": (768, 1344), "9:21": (704, 1472)}
HEADER = re.compile(r"^(#{2,3})\s+(\d+(?:-[A-Za-z])?)\.\s*(.*)$")
BLACK_TAG = re.compile(r"\{*\s*black background\s*,\s*simple background\s*\}*\s*,?\s*")
BLACK_PHRASE = re.compile(r"(?:isolated\s+)?(?:cleanly\s+)?on a solid black background(?: for easy alpha extraction)?")


def read(path):
    with open(path, encoding="utf-8") as f:
        return f.read().strip().rstrip(",")


def join(*parts):
    return ", ".join(p.strip().strip(",") for p in parts if p and p.strip())


def aspect_of(title):
    for key in sorted(ASPECTS, key=len, reverse=True):
        if re.search(rf"(?<!\d){re.escape(key)}(?!\d)", title):
            return ASPECTS[key]
    return None


def load_sections():
    """{ '04:1-A': {title, prompt, size, file} } from every '[..] NN_*.txt' in Art Prompts."""
    sections = {}
    for path in sorted(glob.glob(os.path.join(glob.escape(PROMPT_DIR), "*.txt"))):
        m = re.search(r"\]\s*(\d\d)_", os.path.basename(path))
        if not m:
            continue
        num, cur, parent_size = m.group(1), None, None
        with open(path, encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                h = HEADER.match(line)
                if h:
                    size = aspect_of(h.group(3))
                    if h.group(1) == "##":
                        parent_size = size
                    cur = {"title": h.group(3), "lines": [], "size": size or parent_size or ASPECTS["1:1"],
                           "file": os.path.basename(path)}
                    sections[f"{num}:{h.group(2)}"] = cur
                elif cur is not None and line and not line.startswith("#") and not line.startswith("---"):
                    cur["lines"].append(line)
    out = {}
    for key, s in sections.items():
        if s["lines"]:  # parents of sub-sections carry no prompt
            s["prompt"] = " ".join(s["lines"])
            out[key] = s
    return out


def build(section, size, seed, base, neg):
    prompt = section["prompt"]
    transparent = bool(BLACK_TAG.search(prompt))
    if transparent:
        prompt = BLACK_TAG.sub("", prompt)
        prompt = BLACK_PHRASE.sub("on a transparent background", prompt)
    prompt = join(prompt, "2.0::transparent background::" if transparent else "", base)
    w, h = size
    assert w % 64 == 0 and h % 64 == 0, "size must be multiples of 64"
    if w * h > MAX_FREE_PIXELS:
        print(f"  ! {w}x{h} exceeds the free-generation pixel budget (costs Anlas)")
    params = {
        "params_version": 3,
        "width": w, "height": h,
        "scale": 5.0,
        "sampler": "k_euler_ancestral",
        "steps": 23,
        "n_samples": 1,
        "seed": seed,
        "noise_schedule": "karras",
        "negative_prompt": neg,
        "v4_prompt": {"caption": {"base_caption": prompt, "char_captions": []},
                      "use_coords": False, "use_order": True},
        "v4_negative_prompt": {"caption": {"base_caption": neg, "char_captions": []},
                               "legacy_uc": False},
    }
    if transparent:
        # V5 web-client behaviour: the tag alone or these flags alone are not enough.
        params["straight_alpha"] = True
        params["tag_hint_transparent_background"] = True
    body = {"input": prompt, "model": "nai-diffusion-5-full", "action": "generate", "parameters": params}
    return body, transparent


def request(body, token):
    req = urllib.request.Request(URL, data=json.dumps(body).encode(), headers={
        "Authorization": f"Bearer {token}",
        "Content-Type": "application/json",
        "User-Agent": UA,
    })
    for attempt in range(5):
        try:
            data = urllib.request.urlopen(req, timeout=240).read()
            with zipfile.ZipFile(io.BytesIO(data)) as z:
                return z.read(z.namelist()[0])
        except urllib.error.HTTPError as e:
            msg = e.read().decode(errors="replace")[:500]
            if e.code == 429 and attempt < 4:
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
    return im, stats[0] / sum(stats)


def main():
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")  # headers are Korean; cp949 consoles choke
    ap = argparse.ArgumentParser()
    ap.add_argument("keys", nargs="*", help="section keys like 04:1-B 05:2")
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--n", type=int, default=1, help="images per section")
    ap.add_argument("--seed", type=int, help="first seed (then +1 per image)")
    ap.add_argument("--size", help="WxH override, e.g. 832x1216")
    ap.add_argument("--alpha-floor", type=int, default=24)
    ap.add_argument("--pad", type=int, default=4)
    args = ap.parse_args()

    sections = load_sections()
    if args.list or not args.keys:
        for k, s in sections.items():
            print(f"{k:8} {s['size'][0]}x{s['size'][1]:<5} {s['title']}")
        return

    token = os.environ.get("NAI_API_KEY")
    if not token:
        raise SystemExit("NAI_API_KEY is not set")
    base, neg = read(BASE), read(NEG)

    for key in args.keys:
        section = sections[key]
        size = tuple(int(v) for v in args.size.lower().split("x")) if args.size else section["size"]
        name = key.replace(":", "_")
        os.makedirs(os.path.join(OUT, name), exist_ok=True)
        for i in range(args.n):
            seed = (args.seed + i) if args.seed is not None else random.randint(1, 2**32 - 1)
            body, transparent = build(section, size, seed, base, neg)
            print(f"{key} seed={seed} {size[0]}x{size[1]} ...", flush=True)
            png = request(body, token)
            stem = os.path.join(OUT, name, f"{name}_{seed}")
            with open(stem + ".png", "wb") as f:
                f.write(png)
            with open(stem + ".json", "w", encoding="utf-8") as f:
                json.dump(body, f, ensure_ascii=False, indent=1)
            if transparent:
                im, clear = clean(png, args.alpha_floor, args.pad)
                im.save(stem + "_clean.png")
                print(f"  -> {stem}_clean.png {im.size} transparent={clear:.0%}")
            else:
                print(f"  -> {stem}.png")


if __name__ == "__main__":
    main()

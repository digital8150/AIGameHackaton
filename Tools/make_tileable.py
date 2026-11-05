"""Make an image wrap seamlessly by cross-fading it with a half-shifted copy of itself.

The half-shifted copy is continuous across the wrap; the original covers the copy's seam in the middle.
Works per axis, so a strip can be made tileable horizontally only.

  python -I Tools/make_tileable.py in.png out.png --axis x|y|xy [--margin 0.25] [--width 1024]
"""
import argparse
from PIL import Image, ImageChops


def ramp(n, margin):
    """weight of the original: 0 at both edges, 1 across the middle (smoothstep over `margin` of the size)."""
    m = max(1, int(n * margin))
    out = []
    for i in range(n):
        d = min(i, n - 1 - i) / m
        d = max(0.0, min(1.0, d))
        out.append(int(255 * d * d * (3 - 2 * d)))
    return out


def tile_axis(im, axis, margin):
    w, h = im.size
    shifted = ImageChops.offset(im, w // 2 if axis == "x" else 0, h // 2 if axis == "y" else 0)
    weights = ramp(w if axis == "x" else h, margin)
    line = Image.new("L", (w, 1) if axis == "x" else (1, h))
    line.putdata(weights)
    mask = line.resize((w, h), Image.NEAREST)
    return Image.composite(im, shifted, mask)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("src")
    ap.add_argument("dst")
    ap.add_argument("--axis", default="xy", choices=["x", "y", "xy"])
    ap.add_argument("--margin", type=float, default=0.25)
    ap.add_argument("--width", type=int, help="resize to this width first (keeps aspect)")
    args = ap.parse_args()

    im = Image.open(args.src).convert("RGBA")
    if args.width:
        im = im.resize((args.width, round(im.height * args.width / im.width)), Image.LANCZOS)
    for axis in args.axis:
        im = tile_axis(im, axis, args.margin)
    im.save(args.dst)
    print(args.dst, im.size)


if __name__ == "__main__":
    main()

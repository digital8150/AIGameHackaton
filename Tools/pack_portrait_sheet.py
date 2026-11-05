#!/usr/bin/env python3
"""Crops every frame of a big idle sheet down to a bust and packs them into a smaller sheet for the HUD badges.

  python Tools/pack_portrait_sheet.py <in.png> <out.png> [--crop x y size] [--cell 640] [--columns 12] [--back r g b]

Frames run left to right, top to bottom, with no gutter. Fully transparent pixels get the badge's background colour
instead of whatever the source carried (the exports have green there), so filtering cannot draw a coloured fringe.
"""
import argparse
import os

import numpy as np
from PIL import Image


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("input")
    ap.add_argument("output")
    ap.add_argument("--crop", type=int, nargs=3, default=[180, 85, 280], metavar=("X", "Y", "SIZE"),
                    help="square inside each source cell")
    ap.add_argument("--cell", type=int, default=640, help="source cell size in px")
    ap.add_argument("--columns", type=int, default=12)
    ap.add_argument("--back", type=int, nargs=3, default=[58, 44, 66], metavar=("R", "G", "B"))
    args = ap.parse_args()

    src = np.array(Image.open(args.input).convert("RGBA"))
    rows = src.shape[0] // args.cell
    x, y, size = args.crop
    out = np.zeros((rows * size, args.columns * size, 4), dtype=np.uint8)
    for row in range(rows):
        for col in range(args.columns):
            sy, sx = row * args.cell + y, col * args.cell + x
            out[row * size:(row + 1) * size, col * size:(col + 1) * size] = src[sy:sy + size, sx:sx + size]
    out[out[..., 3] == 0, :3] = args.back
    # written aside and swapped in: an open editor imports a half-written PNG otherwise
    temp = args.output + ".tmp"
    Image.fromarray(out).save(temp, format="PNG", optimize=True)
    os.replace(temp, args.output)
    print(f"{args.output}: {out.shape[1]}x{out.shape[0]}, {rows * args.columns} frames of {size}px")


if __name__ == "__main__":
    main()

"""Pixel UI pieces for the in-game HUD (original art, Kenney-compatible palette).

Outputs (PPU 18 / point filtered, drawn 2x on a 960x540 canvas):
  medallion_light.png  36x36  gold sun ring   — Dusk Princess (light sense)
  medallion_dark.png   36x36  silver moon ring — Twilight Prince (sound sense)
  panel.png            15x15  9-slice plum panel with a gold hairline (border 5)

Usage: python -I Tools/gen_ui.py <out_dir>
"""
import math
import os
import sys
from PIL import Image

OUT_WARM = (111, 62, 67, 255)
OUT_DUSK = (40, 28, 58, 255)


def disc(size, r_out, r_in, ring, ring_hi, ring_sh, inner_top, inner_bot, outline, sparkle):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    c = (size - 1) / 2.0
    for y in range(size):
        for x in range(size):
            d = math.hypot(x - c, y - c)
            if d > r_out + 0.5:
                continue
            if d > r_out - 0.5:
                img.putpixel((x, y), outline)
            elif d > r_in + 0.5:
                # ring, lit from upper-left
                lit = (x - c) * -0.7 + (y - c) * -0.7
                col = ring_hi if lit > r_out * 0.55 else ring_sh if lit < -r_out * 0.45 else ring
                img.putpixel((x, y), col)
            elif d > r_in - 0.5:
                img.putpixel((x, y), outline)
            else:
                t = (y - (c - r_in)) / (2 * r_in)
                t = max(0.0, min(1.0, t))
                # 4-step banded gradient (pixel-art, no smooth blend)
                t = round(t * 3) / 3
                col = tuple(int(inner_top[i] * (1 - t) + inner_bot[i] * t) for i in range(3)) + (255,)
                img.putpixel((x, y), col)
    for (sx, sy) in sparkle:
        img.putpixel((sx, sy), (255, 255, 255, 255))
    return img


def ray_marks(img, color, c, r, n):
    # small notches on the ring: sun rays / moon phases
    for k in range(n):
        a = k * 2 * math.pi / n
        x = int(round(c + math.cos(a) * r))
        y = int(round(c + math.sin(a) * r))
        img.putpixel((x, y), color)


def panel():
    s = 15
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    fill = (34, 20, 44, 230)
    edge = (255, 205, 120, 255)
    dark = (18, 10, 26, 255)
    for y in range(s):
        for x in range(s):
            corner = (x in (0, s - 1) and y in (0, s - 1))
            if corner:
                continue
            if x in (0, s - 1) or y in (0, s - 1):
                img.putpixel((x, y), dark)
            elif (x in (1, s - 2) or y in (1, s - 2)) and not (x in (1, s - 2) and y in (1, s - 2)):
                img.putpixel((x, y), edge if y == 1 else (180, 120, 90, 255))
            else:
                img.putpixel((x, y), fill)
    return img


def check(done):
    # 9x9 diamond: hollow (todo) or filled gold with a sparkle (done)
    s = 9
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    c = 4
    for y in range(s):
        for x in range(s):
            d = abs(x - c) + abs(y - c)
            if d == 4:
                img.putpixel((x, y), (40, 22, 40, 255))
            elif d == 3:
                img.putpixel((x, y), (255, 205, 86, 255) if done else (230, 214, 236, 255))
            elif d < 3:
                img.putpixel((x, y), ((255, 240, 170, 255) if d < 2 else (244, 172, 102, 255)) if done else (0, 0, 0, 0))
    if done:
        img.putpixel((3, 3), (255, 255, 255, 255))
    return img


def keycap():
    # 11x11 9-slice keycap (border 4): cream face, warm shade, dark outline
    s = 11
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    for y in range(s):
        for x in range(s):
            if (x in (0, s - 1)) and (y in (0, s - 1)):
                continue
            if x in (0, s - 1) or y in (0, s - 1):
                img.putpixel((x, y), (40, 22, 40, 255))
            elif y >= s - 3:
                img.putpixel((x, y), (196, 150, 128, 255))
            elif y == 1:
                img.putpixel((x, y), (255, 255, 246, 255))
            else:
                img.putpixel((x, y), (250, 236, 214, 255))
    return img


def light_plank():
    # 36x9 plank of solid light (2 tiles): gold core, white rim, sparkles
    w, h = 36, 9
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for y in range(h):
        for x in range(w):
            edge = x in (0, w - 1) or y in (0, h - 1)
            if edge and (x in (0, w - 1)) and (y in (0, h - 1)):
                continue
            if edge:
                img.putpixel((x, y), (255, 236, 170, 255))
            elif y == 1:
                img.putpixel((x, y), (255, 255, 240, 255))
            elif y >= h - 3:
                img.putpixel((x, y), (244, 150, 72, 255))
            else:
                img.putpixel((x, y), (255, 205, 86, 255))
    for sx in (5, 13, 22, 30):
        img.putpixel((sx, 3), (255, 255, 255, 255))
    return img


if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    light = disc(36, 17, 13, (255, 205, 86, 255), (255, 240, 170, 255), (244, 150, 72, 255),
                 (255, 214, 150), (232, 120, 120), OUT_WARM, [(9, 9), (10, 8)])
    ray_marks(light, (255, 248, 214, 255), 17.5, 15, 12)
    light.save(os.path.join(out, "medallion_light.png"))
    dark = disc(36, 17, 13, (172, 190, 214, 255), (230, 236, 250, 255), (96, 108, 140, 255),
                (24, 30, 64), (6, 8, 22), OUT_DUSK, [(9, 9)])
    ray_marks(dark, (255, 240, 170, 255), 17.5, 15, 4)
    dark.save(os.path.join(out, "medallion_dark.png"))
    panel().save(os.path.join(out, "panel.png"))
    check(False).save(os.path.join(out, "check_empty.png"))
    check(True).save(os.path.join(out, "check_done.png"))
    keycap().save(os.path.join(out, "keycap.png"))
    light_plank().save(os.path.join(out, "light_plank.png"))
    print("ok")

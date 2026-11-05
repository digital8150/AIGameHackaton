"""Generates the two lead sprite sheets (Twilight Prince / Dusk Princess).

Original pixel art for this project, drawn procedurally so it matches the
Kenney Pixel Platformer look: 24x24 cells, flat colours, 1px warm outline,
light from upper-left.

Sheet layout (8 frames, 24px each, 192x24):
  0-1 idle, 2-5 run, 6 jump, 7 fall

Usage: python -I Tools/gen_characters.py <out_dir>
"""
import os
import sys
from PIL import Image

W = H = 24

# --- shared palette (sampled from Kenney Pixel Platformer) -----------------
OUTLINE_WARM = (111, 62, 67, 255)
OUTLINE_DUSK = (62, 39, 72, 255)
SKIN = (255, 214, 170, 255)
SKIN_SH = (232, 170, 136, 255)
BLUSH = (255, 160, 150, 255)
WHITE = (255, 255, 255, 255)
CLOUD = (223, 246, 245, 255)
CLOUD_SH = (172, 205, 214, 255)
SKY = (44, 197, 246, 255)
SKY_DK = (20, 144, 195, 255)
SLATE = (67, 74, 95, 255)
BOOT = (159, 90, 82, 255)
GOLD = (255, 205, 86, 255)
GOLD_DK = (244, 150, 72, 255)
HAIR_GOLD = (255, 231, 150, 255)
HAIR_GOLD_HI = (255, 248, 214, 255)
HAIR_GOLD_SH = (232, 186, 102, 255)
HAIR_V = (86, 58, 132, 255)
HAIR_V_HI = (134, 102, 186, 255)
HAIR_V_SH = (58, 38, 96, 255)
PLUM = (132, 52, 104, 255)
PLUM_HI = (186, 88, 140, 255)
PLUM_SH = (92, 34, 78, 255)
SILVER = (230, 236, 250, 255)
MOON = (255, 240, 170, 255)
EYE = (46, 30, 60, 255)


class Canvas:
    def __init__(self):
        self.px = {}

    def p(self, x, y, c):
        if 0 <= x < W and 0 <= y < H:
            self.px[(x, y)] = c

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.p(x, y, c)

    def row(self, y, x0, x1, c):
        self.rect(x0, y, x1, y, c)

    def image(self, outline):
        img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        for (x, y), c in self.px.items():
            img.putpixel((x, y), c)
        # 1px outline around the silhouette (4-neighbourhood), Kenney style
        out = img.copy()
        for y in range(H):
            for x in range(W):
                if img.getpixel((x, y))[3]:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < W and 0 <= ny < H and img.getpixel((nx, ny))[3]:
                        out.putpixel((x, y), outline)
                        break
        return out


# ---------------------------------------------------------------------------
# Twilight Prince — from the kingdom of light; cannot see (eyes closed).
# White-and-sky tunic, sky cape, pale-gold hair, small gold crown.
# ---------------------------------------------------------------------------
def prince(kind, phase):
    c = Canvas()
    bob = 0
    if kind == "idle":
        bob = phase  # breathe
    elif kind == "run":
        bob = 1 if phase in (1, 3) else 0
    oy = bob

    # legs ---------------------------------------------------------------
    if kind == "idle":
        legs = [(10, 20, 22), (13, 20, 22)]
    elif kind == "run":
        legs = [
            [(9, 20, 21), (14, 20, 22)],
            [(11, 20, 22), (12, 20, 21)],
            [(14, 20, 21), (9, 20, 22)],
            [(12, 20, 22), (11, 20, 21)],
        ][phase]
    elif kind == "jump":
        legs = [(9, 19, 20), (13, 20, 21)]
    else:  # fall
        legs = [(9, 20, 22), (14, 20, 21)]
    for (lx, y0, y1) in legs:
        c.rect(lx, y0, lx + 1, y1 - 1, SLATE)
        c.rect(lx, y1, lx + 1, y1, BOOT)

    # cape (behind, left side since he faces right) -------------------------
    flutter = {"idle": 0, "run": 1 + (phase % 2), "jump": 2, "fall": 3}[kind]
    for y in range(14 + oy, 21 + oy - (1 if kind == "fall" else 0)):
        depth = (y - 14 - oy)
        x0 = 7 - min(depth // 2, flutter)
        c.row(y, x0, 8, SKY_DK if depth % 3 else SKY)
    if kind == "fall":
        c.rect(5, 11 + oy, 7, 13 + oy, SKY)

    # tunic --------------------------------------------------------------
    c.rect(8, 14 + oy, 15, 19 + oy, CLOUD)
    c.row(19 + oy, 8, 15, CLOUD_SH)
    c.rect(15, 15 + oy, 15, 19 + oy, CLOUD_SH)
    # sash
    for i in range(5):
        c.p(9 + i, 14 + oy + i, SKY)
        c.p(10 + i, 14 + oy + i, SKY_DK)
    c.row(18 + oy, 8, 15, GOLD_DK)  # belt
    c.p(12, 18 + oy, GOLD)

    # arms ---------------------------------------------------------------
    if kind in ("jump", "fall"):
        c.rect(16, 12 + oy, 16, 14 + oy, CLOUD)
        c.p(16, 11 + oy, SKIN)
        c.rect(7, 13 + oy, 7, 14 + oy, CLOUD)
    else:
        swing = 0 if kind == "idle" else (1 if phase in (0, 1) else -1)
        c.rect(16, 15 + oy, 16, 16 + oy + swing, CLOUD)
        c.p(16, 17 + oy + swing, SKIN)
        c.rect(7, 15 + oy, 7, 16 + oy - swing, CLOUD_SH)

    # head ---------------------------------------------------------------
    c.rect(8, 6 + oy, 15, 13 + oy, SKIN)
    c.rect(8, 13 + oy, 15, 13 + oy, SKIN_SH)
    # hair mass
    c.rect(7, 4 + oy, 16, 7 + oy, HAIR_GOLD)
    c.rect(7, 8 + oy, 9, 12 + oy, HAIR_GOLD)  # back of head
    c.rect(7, 10 + oy, 8, 13 + oy, HAIR_GOLD_SH)
    c.p(16, 8 + oy, HAIR_GOLD)
    c.p(15, 8 + oy, HAIR_GOLD)
    c.p(12, 8 + oy, HAIR_GOLD)  # fringe tuft
    c.row(4 + oy, 9, 13, HAIR_GOLD_HI)
    c.p(8, 5 + oy, HAIR_GOLD_HI)
    c.row(7 + oy, 10, 16, HAIR_GOLD_SH)
    c.p(13, 7 + oy, HAIR_GOLD)
    # closed eyes (gentle downward arcs) + blush
    for ex in (11, 14):
        c.p(ex, 10 + oy, OUTLINE_WARM)
        c.p(ex + 1, 10 + oy, OUTLINE_WARM)
        c.p(ex - 1, 9 + oy, SKIN_SH)
    c.p(11, 12 + oy, BLUSH)
    c.p(15, 12 + oy, BLUSH)
    c.p(13, 12 + oy, SKIN_SH)

    # crown --------------------------------------------------------------
    c.row(3 + oy, 9, 14, GOLD)
    c.p(9, 2 + oy, GOLD)
    c.p(11, 1 + oy, GOLD)
    c.p(12, 2 + oy, GOLD)
    c.p(14, 2 + oy, GOLD)
    c.row(3 + oy, 10, 10, GOLD_DK)
    c.p(12, 3 + oy, SKY)  # gem
    return c.image(OUTLINE_WARM)


# ---------------------------------------------------------------------------
# Dusk Princess — from the kingdom of darkness; cannot hear.
# Long violet hair, plum bell dress, silver crescent tiara, wide open eyes.
# ---------------------------------------------------------------------------
def princess(kind, phase):
    c = Canvas()
    oy = 0
    if kind == "idle":
        oy = phase
    elif kind == "run":
        oy = 1 if phase in (1, 3) else 0
    sway = {"idle": 0, "run": [1, 0, -1, 0][phase], "jump": -1, "fall": 1}[kind]

    # long hair behind ----------------------------------------------------
    hair_len = 19 if kind != "fall" else 16
    trail = {"idle": 0, "run": 1 + (phase % 2), "jump": 1, "fall": 2}[kind]
    for y in range(7 + oy, hair_len + oy + 1):
        d = (y - 7 - oy)
        x0 = 6 - min(d // 3, trail)
        c.row(y, x0, 9, HAIR_V if d % 4 else HAIR_V_SH)
    if kind == "fall":
        c.rect(4, 5 + oy, 6, 8 + oy, HAIR_V)

    # feet ---------------------------------------------------------------
    if kind == "run":
        f = [[(9, 22), (13, 21)], [(11, 22), (12, 22)], [(13, 22), (9, 21)], [(12, 22), (11, 22)]][phase]
        for fx, fy in f:
            c.rect(fx, fy, fx + 1, fy, EYE)
    elif kind == "idle":
        c.rect(10, 22, 11, 22, EYE)
        c.rect(13, 22, 14, 22, EYE)

    # skirt (bell) ---------------------------------------------------------
    sk_top = 17 + oy
    sk_bot = 21 if kind != "jump" else 20
    for y in range(sk_top, sk_bot + 1):
        w = (y - sk_top) // 2 + 3
        s = sway if y >= sk_top + 2 else 0
        x0, x1 = 12 - w + s, 11 + w + s + 1
        c.row(y, x0, x1, PLUM)
        c.p(x1, y, PLUM_SH)
        c.p(x1 - 1, y, PLUM_SH)
        c.p(x0, y, PLUM_HI)
    # hem trim
    w = (sk_bot - sk_top) // 2 + 3
    c.row(sk_bot, 12 - w + sway, 12 + w + sway, PLUM_HI)
    for x in range(12 - w + sway, 13 + w + sway, 2):
        c.p(x, sk_bot, SILVER)

    # bodice -------------------------------------------------------------
    c.rect(9, 14 + oy, 14, 16 + oy, PLUM)
    c.rect(14, 14 + oy, 14, 16 + oy, PLUM_SH)
    c.p(11, 15 + oy, SILVER)
    c.p(12, 15 + oy, SILVER)
    c.row(16 + oy, 9, 14, PLUM_SH)

    # arms ---------------------------------------------------------------
    if kind in ("jump", "fall"):
        c.rect(15, 12 + oy, 15, 14 + oy, PLUM)
        c.p(15, 11 + oy, SKIN)
        c.rect(8, 12 + oy, 8, 13 + oy, PLUM)
    else:
        sw = 0 if kind == "idle" else (1 if phase in (0, 1) else -1)
        c.rect(15, 14 + oy, 15, 15 + oy + sw, PLUM)
        c.p(15, 16 + oy + sw, SKIN)
        c.rect(8, 14 + oy, 8, 15 + oy - sw, PLUM_SH)

    # head ---------------------------------------------------------------
    c.rect(9, 6 + oy, 15, 13 + oy, SKIN)
    c.row(13 + oy, 9, 15, SKIN_SH)
    # fringe + top
    c.rect(7, 4 + oy, 16, 7 + oy, HAIR_V)
    c.rect(7, 8 + oy, 9, 13 + oy, HAIR_V)
    c.p(16, 8 + oy, HAIR_V)
    c.p(16, 9 + oy, HAIR_V)
    c.p(13, 8 + oy, HAIR_V)
    c.row(5 + oy, 9, 12, HAIR_V_HI)
    c.p(8, 6 + oy, HAIR_V_HI)
    c.row(7 + oy, 10, 15, HAIR_V_SH)
    c.p(12, 7 + oy, HAIR_V)
    # wide-open eyes with highlights
    for ex in (11, 14):
        c.rect(ex, 9 + oy, ex, 11 + oy, EYE)
        c.p(ex, 9 + oy, WHITE)
    c.p(10, 12 + oy, BLUSH)
    c.p(15, 12 + oy, BLUSH)
    c.p(13, 12 + oy, SKIN_SH)

    # crescent tiara -----------------------------------------------------
    c.row(3 + oy, 9, 14, SILVER)
    c.p(12, 2 + oy, MOON)
    c.p(13, 1 + oy, MOON)
    c.p(13, 2 + oy, MOON)
    c.p(12, 1 + oy, SILVER)
    return c.image(OUTLINE_DUSK)


FRAMES = [("idle", 0), ("idle", 1), ("run", 0), ("run", 1), ("run", 2), ("run", 3), ("jump", 0), ("fall", 0)]


def sheet(fn):
    img = Image.new("RGBA", (W * len(FRAMES), H), (0, 0, 0, 0))
    for i, (k, p) in enumerate(FRAMES):
        img.paste(fn(k, p), (i * W, 0))
    return img


if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    sheet(prince).save(os.path.join(out, "prince.png"))
    sheet(princess).save(os.path.join(out, "princess.png"))
    print("ok")

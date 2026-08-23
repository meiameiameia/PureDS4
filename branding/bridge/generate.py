#!/usr/bin/env python3
"""
"A Ponte" (The Bridge) - DS4Windows Reworked icon family generator.

Everything is axis-aligned and drawn on an integer pixel grid, so every raster
size is exact: no resampling, no antialiasing, no blur. 16/20/24/32 use
hand-tuned grids; 40 and above are derived from the 24-unit master by rounding
edges to whole pixels (still crisp, because every edge is axis-aligned).

Master geometry, in 24-unit space, half-open [x0,x1) x [y0,y1):
    solid anchor    ( 1,  9,  7, 15)   physical device  - filled
    gap             (  x = 7 )
    left span       ( 8, 11, 11, 13)
    pillar          (11,  6, 13, 18)   the app          - the intervention
    right span      (13, 11, 16, 13)
    gap             (  x = 16 )
    hollow anchor   (17,  9, 23, 15)   virtual device   - outlined, 2u stroke
Everything centers on (12, 12). Margin is 1u left and right.
"""

import os
import struct
import zlib

OUT = os.path.dirname(os.path.abspath(__file__))

# ---------------------------------------------------------------- palette ---
PLATE_BG      = (0x22, 0x30, 0x3D, 255)   # app tile
PLATE_MARK    = (0xE9, 0xF0, 0xF6, 255)
PLATE_PILLAR  = (0xE0, 0xA4, 0x3C, 255)

FLAT_MARK     = (0x2E, 0x5F, 0x80, 255)   # flat colored, transparent bg
FLAT_PILLAR   = (0xC8, 0x84, 0x2A, 255)

MONO_DARK     = (0x20, 0x27, 0x2C, 255)   # for a LIGHT Windows tray
MONO_LIGHT    = (0xF4, 0xF7, 0xFB, 255)   # for a DARK  Windows tray

GAUGE_FULL    = (0x3F, 0x9D, 0x5C, 255)
GAUGE_MID     = (0xD0, 0x8A, 0x2A, 255)
GAUGE_LOW     = (0xC4, 0x48, 0x3F, 255)

SIZES     = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
TRAY_SIZES = [16, 20, 24, 32, 40, 48]

# --------------------------------------------------------------- geometry ---
# Master, in 24-unit space.
M = {
    "solid":  (1,  9,  7, 15),
    "lspan":  (8, 11, 11, 13),
    "pillar": (11, 6, 13, 18),
    "rspan":  (13, 11, 16, 13),
    "hollow": (17, 9, 23, 15),
    "stroke": 2,
    "chamfer": 2,
}

# Hand-tuned grids, on a progressive-detail ladder.
#
# The 1-unit gap on each side of the pillar is kept at EVERY size: it is what
# separates the pillar from the solid anchor (without it they fuse into one
# blob in monochrome at 16) and it is what carries "assembled, therefore
# removable". The deck spans are the part that is dropped below 24px - at that
# size they only add mud, and the solid-vs-hollow contrast is what has to
# survive. 16 also thins the hollow stroke to 1 so its hole stays 3x4.
HAND = {
    16: {
        "solid":  (1, 5, 6, 11),
        "lspan":  None,
        "pillar": (7, 2, 9, 14),
        "rspan":  None,
        "hollow": (10, 5, 15, 11),
        "stroke": 1,
        "chamfer": 0,
    },
    20: {
        "solid":  (2, 7, 8, 13),
        "lspan":  None,
        "pillar": (9, 4, 11, 16),
        "rspan":  None,
        "hollow": (12, 7, 18, 13),
        "stroke": 2,
        "chamfer": 0,
    },
    24: dict(M),
    32: {
        "solid":  (2, 12, 10, 20),
        "lspan":  (11, 15, 15, 17),
        "pillar": (15, 8, 17, 24),
        "rspan":  (17, 15, 21, 17),
        "hollow": (22, 12, 30, 20),
        "stroke": 2,
        "chamfer": 3,
    },
}


def geometry(size):
    if size in HAND:
        return dict(HAND[size])
    k = size / 24.0
    g = {}
    for key in ("solid", "lspan", "pillar", "rspan", "hollow"):
        x0, y0, x1, y1 = M[key]
        g[key] = (round(x0 * k), round(y0 * k), round(x1 * k), round(y1 * k))
    g["stroke"] = max(1, round(M["stroke"] * k))
    g["chamfer"] = max(0, round(M["chamfer"] * k))
    return g


# ---------------------------------------------------------------- raster ----
class Canvas:
    def __init__(self, size):
        self.size = size
        self.px = [[(0, 0, 0, 0)] * size for _ in range(size)]

    def rect(self, box, color):
        if box is None:
            return
        x0, y0, x1, y1 = box
        for y in range(max(0, y0), min(self.size, y1)):
            row = self.px[y]
            for x in range(max(0, x0), min(self.size, x1)):
                row[x] = color

    def ring(self, box, stroke, color):
        """Outlined rectangle: the hollow anchor."""
        x0, y0, x1, y1 = box
        self.rect((x0, y0, x1, y0 + stroke), color)
        self.rect((x0, y1 - stroke, x1, y1), color)
        self.rect((x0, y0, x0 + stroke, y1), color)
        self.rect((x1 - stroke, y0, x1, y1), color)

    def clear_rect(self, box):
        self.rect(box, (0, 0, 0, 0))

    def chamfer_tl(self, box, c):
        """Stepped 45-degree cut on the top-left corner of the solid anchor."""
        if c <= 0:
            return
        x0, y0, _, _ = box
        for r in range(c):
            for x in range(x0, x0 + (c - r)):
                if 0 <= y0 + r < self.size and 0 <= x < self.size:
                    self.px[y0 + r][x] = (0, 0, 0, 0)

    def plate(self, color, radius):
        s = self.size
        for y in range(s):
            for x in range(s):
                if self._in_round_rect(x, y, s, radius):
                    self.px[y][x] = color

    @staticmethod
    def _in_round_rect(x, y, s, r):
        if r <= 0:
            return True
        cx = x + 0.5
        cy = y + 0.5
        for ox, oy in ((r, r), (s - r, r), (r, s - r), (s - r, s - r)):
            in_x = cx < r if ox == r else cx > s - r
            in_y = cy < r if oy == r else cy > s - r
            if in_x and in_y:
                return (cx - ox) ** 2 + (cy - oy) ** 2 <= r * r
        return True

    def composite_over(self, other):
        """Draw `other` (same size) on top, simple source-over for opaque art."""
        for y in range(self.size):
            for x in range(self.size):
                r, g, b, a = other.px[y][x]
                if a:
                    self.px[y][x] = (r, g, b, a)


def draw_mark(size, mark_color, pillar_color, *, degraded=False,
              battery=None, charging=False, gauge_color=None):
    """
    battery: None for the plain mark, else 0..4 fill steps. Battery variants
    thin the hollow anchor's stroke to 1 unit so its interior is tall enough to
    read as a gauge.
    """
    g = geometry(size)
    c = Canvas(size)

    c.rect(g["solid"], mark_color)
    c.chamfer_tl(g["solid"], g["chamfer"])
    c.rect(g["lspan"], mark_color)
    c.rect(g["rspan"], mark_color)

    px0, py0, px1, py1 = g["pillar"]
    if degraded:
        gap = max(2, round(2 * size / 24.0))
        mid = (py0 + py1) // 2
        c.rect((px0, py0, px1, mid - gap // 2), pillar_color)
        c.rect((px0, mid + (gap - gap // 2), px1, py1), pillar_color)
    else:
        c.rect(g["pillar"], pillar_color)

    hx0, hy0, hx1, hy1 = g["hollow"]
    if battery is None:
        c.ring(g["hollow"], g["stroke"], mark_color)
    else:
        st = max(1, round(1 * size / 24.0))
        c.ring(g["hollow"], st, mark_color)
        ix0, iy0, ix1, iy1 = hx0 + st, hy0 + st, hx1 - st, hy1 - st
        rows = iy1 - iy0
        filled = int(round(battery / 4.0 * rows))
        if filled > 0:
            c.rect((ix0, iy1 - filled, ix1, iy1), gauge_color or mark_color)
        if charging:
            nub_w = max(1, round(2 * size / 24.0))
            nub_h = max(1, round(1 * size / 24.0))
            nx = (hx0 + hx1) // 2 - nub_w // 2
            c.rect((nx, hy0 - nub_h, nx + nub_w, hy0), mark_color)
    return c


def draw_plate_icon(size, **kw):
    c = Canvas(size)
    c.plate(PLATE_BG, max(1, round(2 * size / 24.0)))
    c.composite_over(draw_mark(size, PLATE_MARK, PLATE_PILLAR, **kw))
    return c


# ------------------------------------------------------------------ PNG -----
def png_bytes(canvas):
    s = canvas.size
    raw = bytearray()
    for row in canvas.px:
        raw.append(0)
        for r, g, b, a in row:
            raw += bytes((r, g, b, a))

    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", s, s, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(bytes(raw), 9))
            + chunk(b"IEND", b""))


# ------------------------------------------------------------------ ICO -----
def bmp_bytes(canvas):
    """BITMAPINFOHEADER + bottom-up BGRA + 1bpp AND mask (legacy-safe)."""
    s = canvas.size
    header = struct.pack("<IiiHHIIiiII", 40, s, s * 2, 1, 32, 0, 0, 0, 0, 0, 0)
    xor = bytearray()
    for y in range(s - 1, -1, -1):
        for r, g, b, a in canvas.px[y]:
            xor += bytes((b, g, r, a))
    stride = ((s + 31) // 32) * 4
    mask = bytearray()
    for y in range(s - 1, -1, -1):
        bits = bytearray(stride)
        for x in range(s):
            if canvas.px[y][x][3] == 0:
                bits[x >> 3] |= 0x80 >> (x & 7)
        mask += bits
    return header + bytes(xor) + bytes(mask)


def write_ico(path, canvases):
    entries, blobs = [], []
    offset = 6 + 16 * len(canvases)
    for c in canvases:
        blob = png_bytes(c) if c.size >= 256 else bmp_bytes(c)
        w = 0 if c.size >= 256 else c.size
        entries.append(struct.pack("<BBBBHHII", w, w, 0, 0, 1, 32,
                                   len(blob), offset))
        offset += len(blob)
        blobs.append(blob)
    with open(path, "wb") as fh:
        fh.write(struct.pack("<HHH", 0, 1, len(canvases)))
        for e in entries:
            fh.write(e)
        for b in blobs:
            fh.write(b)


# ----------------------------------------------------------------- build ----
def build():
    ico = os.path.join(OUT, "ico")
    prev = os.path.join(OUT, "preview")
    os.makedirs(ico, exist_ok=True)
    os.makedirs(prev, exist_ok=True)

    families = []

    families.append(("Bridge.ico", SIZES,
                     lambda s: draw_plate_icon(s)))
    families.append(("Bridge-Flat.ico", SIZES,
                     lambda s: draw_mark(s, FLAT_MARK, FLAT_PILLAR)))
    families.append(("Bridge-Light.ico", TRAY_SIZES,
                     lambda s: draw_mark(s, MONO_DARK, MONO_DARK)))
    families.append(("Bridge-Dark.ico", TRAY_SIZES,
                     lambda s: draw_mark(s, MONO_LIGHT, MONO_LIGHT)))
    families.append(("Bridge-Degraded-Light.ico", TRAY_SIZES,
                     lambda s: draw_mark(s, MONO_DARK, MONO_DARK, degraded=True)))
    families.append(("Bridge-Degraded-Dark.ico", TRAY_SIZES,
                     lambda s: draw_mark(s, MONO_LIGHT, MONO_LIGHT, degraded=True)))

    # Battery: the mark stays in the tray theme colour (so it is legible on the
    # user's actual taskbar) and only the GAUGE FILL carries the state colour.
    # Fill height carries the same information, so the icon still works in
    # grayscale, in high contrast, and for colour-blind users.
    levels = [("Critical", 0, GAUGE_LOW), ("Low", 1, GAUGE_LOW),
              ("Half", 2, GAUGE_MID), ("Good", 3, GAUGE_FULL),
              ("Full", 4, GAUGE_FULL)]
    for theme, col in (("Light", MONO_DARK), ("Dark", MONO_LIGHT)):
        for name, lvl, gcol in levels:
            families.append((
                f"Bridge-Battery-{name}-{theme}.ico", TRAY_SIZES,
                (lambda c, l, g: lambda s: draw_mark(s, c, c, battery=l,
                                                     gauge_color=g))(col, lvl, gcol)))
            # Strict-monochrome alternative for high-contrast / no-colour trays.
            families.append((
                f"Bridge-Battery-{name}-{theme}-Mono.ico", TRAY_SIZES,
                (lambda c, l: lambda s: draw_mark(s, c, c, battery=l,
                                                  gauge_color=c))(col, lvl)))
        families.append((
            f"Bridge-Battery-Charging-{theme}.ico", TRAY_SIZES,
            (lambda c: lambda s: draw_mark(s, c, c, battery=4, charging=True,
                                           gauge_color=GAUGE_FULL))(col)))
        families.append((
            f"Bridge-Battery-Charging-{theme}-Mono.ico", TRAY_SIZES,
            (lambda c: lambda s: draw_mark(s, c, c, battery=4, charging=True,
                                           gauge_color=c))(col)))

    for filename, sizes, fn in families:
        write_ico(os.path.join(ico, filename), [fn(s) for s in sizes])

    # Flat PNG exports for docs / README.
    for s in (48, 128, 256):
        with open(os.path.join(prev, f"bridge-flat-{s}.png"), "wb") as fh:
            fh.write(png_bytes(draw_mark(s, FLAT_MARK, FLAT_PILLAR)))
        with open(os.path.join(prev, f"bridge-app-{s}.png"), "wb") as fh:
            fh.write(png_bytes(draw_plate_icon(s)))

    return [f for f, _, _ in families]


if __name__ == "__main__":
    made = build()
    print(f"{len(made)} ico files")
    for m in made:
        print("  " + m)

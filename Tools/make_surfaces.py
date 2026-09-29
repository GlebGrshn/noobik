"""Generates the tileable surface set into Assets/Game/Textures/Surfaces.png.

A 5 x 4 grid of 256 px tiles, imported by Unity as a 20-layer Texture2DArray (Nubik -> Prepare prototype).
Layer = the material's _Surface - 1. Channels: R/G - tangent-space normal (x, y), B - albedo multiplier
(128 = x1.0, range x0..x2), A - mask read by the shader per surface (board gaps, mortar, chipped paint,
wallpaper and rug ornament, brick mortar, scratches, sparkles). U runs along grain, boards and siding; V is across.
Standard library only:
    py -3 Tools/make_surfaces.py
"""
import math
import os
import random
import struct
import zlib

SIZE = 256
COLS, ROWS = 5, 4
OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Game', 'Textures', 'Surfaces.png')
TAU = math.pi * 2


def smooth(t):
    return t * t * (3 - 2 * t)


def clamp(x, lo=0.0, hi=1.0):
    return lo if x < lo else hi if x > hi else x


def smoothstep(a, b, x):
    return smooth(clamp((x - a) / (b - a)))


class Noise:
    """Periodic value noise with separate periods along u and v, sampled at u, v in [0, 1)."""

    def __init__(self, rng, pu, pv=None):
        self.pu, self.pv = pu, pv or pu
        self.grid = [[rng.random() for _ in range(self.pu)] for _ in range(self.pv)]

    def __call__(self, u, v):
        x, y = u * self.pu, v * self.pv
        ix, iy = math.floor(x), math.floor(y)
        fx, fy = smooth(x - ix), smooth(y - iy)
        x0, x1, y0, y1 = ix % self.pu, (ix + 1) % self.pu, iy % self.pv, (iy + 1) % self.pv
        g = self.grid
        a = g[y0][x0] + (g[y0][x1] - g[y0][x0]) * fx
        b = g[y1][x0] + (g[y1][x1] - g[y1][x0]) * fx
        return a + (b - a) * fy


class Fbm:
    def __init__(self, rng, octaves):
        """octaves: (period u, period v, weight); the result is normalised to about 0..1."""
        self.layers = [(Noise(rng, pu, pv), w) for pu, pv, w in octaves]
        self.total = sum(w for _, w in self.layers)

    def __call__(self, u, v):
        return sum(n(u, v) * w for n, w in self.layers) / self.total


class Cells:
    """Periodic jittered cells: nearest and second-nearest distances in cell units, and the nearest cell id."""

    def __init__(self, rng, count, jitter=0.8):
        self.n = count
        self.points = [[(0.5 + (rng.random() - 0.5) * jitter, 0.5 + (rng.random() - 0.5) * jitter, rng.random())
                        for _ in range(count)] for _ in range(count)]

    def __call__(self, u, v):
        x, y = u * self.n, v * self.n
        ix, iy = int(math.floor(x)), int(math.floor(y))
        first, second, cell, near = 9.0, 9.0, 0.0, (0.0, 0.0)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                gx, gy = ix + dx, iy + dy
                ox, oy, value = self.points[gy % self.n][gx % self.n]
                px, py = gx + ox, gy + oy
                d = math.hypot(px - x, py - y)
                if d < first:
                    second, first, cell, near = first, d, value, (px - x, py - y)
                elif d < second:
                    second = d
        return first, second, cell, near


def wrap(d):
    """Shortest periodic difference in [0, 1)."""
    return d - round(d)


def layer(fn):
    """Runs a per-pixel function returning (height, albedo, mask) into three arrays."""
    height, albedo, mask = [], [], []
    for py in range(SIZE):
        v = 1 - (py + 0.5) / SIZE  # image rows go down, texture v goes up
        rh, ra, rm = [], [], []
        for px in range(SIZE):
            h, a, m = fn((px + 0.5) / SIZE, v)
            rh.append(h); ra.append(a); rm.append(m)
        height.append(rh); albedo.append(ra); mask.append(rm)
    return height, albedo, mask


# ---------- Materials ----------

def wood(rng):
    warp = Fbm(rng, [(2, 6, 0.6), (4, 12, 0.3), (8, 24, 0.1)])
    fibers = Noise(rng, 6, 160)
    tone = Fbm(rng, [(2, 4, 1), (4, 8, 0.5)])
    pores = Noise(rng, 24, 256)
    knots = [(rng.random(), rng.random(), 0.06 + rng.random() * 0.04) for _ in range(2)]

    def px(u, v):
        bend = 0.0
        core = 0.0
        for kx, ky, r in knots:
            dx, dy = wrap(u - kx) * 0.45, wrap(v - ky)
            d = math.hypot(dx, dy)
            bend += r * r / (d * d + r * r * 0.6) * 0.18 * (1 if dy > 0 else -1)
            core = max(core, 1 - smoothstep(r * 0.25, r * 0.45, d))
        g = math.sin(TAU * (v * 12 + warp(u, v) * 1.6 + bend))
        band = ((g * 0.5 + 0.5) ** 3)
        f = fibers(u, v)
        height = -band * 0.45 + f * 0.25 - core * 0.3
        albedo = 1.1 - band * 0.3 + (f - 0.5) * 0.14 + (tone(u, v) - 0.5) * 0.18 - core * 0.35
        return height, albedo, 1.0 if pores(u, v) > 0.83 else 0.0
    return px


def masonry(rng):
    rows = 5
    splits = []
    for r in range(rows):
        cuts, x = [], 0.0
        while True:
            w = 0.2 + rng.random() * 0.2
            if x + w > 0.9:
                cuts.append(1.0)
                break
            x += w
            cuts.append(x)
        shift = rng.random()
        tones = [0.82 + rng.random() * 0.3 for _ in cuts]
        splits.append((cuts, shift, tones))
    grain = Fbm(rng, [(8, 8, 0.5), (16, 16, 0.3), (32, 32, 0.2)])
    chips = Noise(rng, 24)

    def px(u, v):
        r = int(v * rows) % rows
        ly = v * rows - math.floor(v * rows)
        cuts, shift, tones = splits[r]
        x = (u + shift) % 1.0
        prev = 0.0
        index = 0
        for i, c in enumerate(cuts):
            if x < c:
                index = i
                break
            prev = c
        nxt = cuts[index]
        dx = min(x - prev, nxt - x)
        dy = min(ly, 1 - ly) / rows
        edge = min(dx, dy) + (chips(u, v) - 0.5) * 0.012
        mortar = 1 - smoothstep(0.006, 0.014, edge)
        bulge = smoothstep(0.0, 0.05, edge)
        g = grain(u, v)
        height = bulge * 0.55 + g * 0.35 - mortar * 0.25
        albedo = tones[index] * (1 + (g - 0.5) * 0.3)
        return height, albedo, mortar
    return px


def metal(rng):
    streak = Noise(rng, 3, 200)
    dents = Fbm(rng, [(3, 3, 0.6), (6, 6, 0.4)])
    stain = Fbm(rng, [(2, 2, 0.5), (5, 5, 0.3), (11, 11, 0.2)])
    scratches = []
    for _ in range(28):
        x, y, a = rng.random(), rng.random(), (rng.random() - 0.5) * 0.9
        length = 0.03 + rng.random() * 0.1
        scratches.append((x, y, math.cos(a), math.sin(a), length))

    def px(u, v):
        s = 0.0
        for x, y, cx, cy, length in scratches:
            dx, dy = wrap(u - x), wrap(v - y)
            along = dx * cx + dy * cy
            if abs(along) > length:
                continue
            across = abs(-dx * cy + dy * cx)
            s = max(s, (1 - smoothstep(0.0008, 0.003, across)) * (1 - abs(along) / length))
        st = streak(u, v)
        height = dents(u, v) * 0.35 + st * 0.06 - s * 0.05
        albedo = 1 + (st - 0.5) * 0.14 + (stain(u, v) - 0.5) * 0.22
        return height, albedo, s
    return px


def cloth(rng):
    threads = 40
    fuzz = Noise(rng, 64)
    tone = Fbm(rng, [(4, 4, 0.6), (8, 8, 0.4)])

    def px(u, v):
        x, y = u * threads, v * threads
        cx, cy = math.floor(x), math.floor(y)
        fx, fy = x - cx, y - cy
        if (cx + cy) % 2 == 0:
            h = math.sin(math.pi * fx) * (0.55 + 0.45 * math.sin(math.pi * fy))
        else:
            h = math.sin(math.pi * fy) * (0.55 + 0.45 * math.sin(math.pi * fx))
        f = fuzz(u, v)
        height = h * 0.8 + f * 0.2
        albedo = 0.86 + h * 0.2 + (f - 0.5) * 0.1 + (tone(u, v) - 0.5) * 0.12
        return height, albedo, 0.0
    return px


def crystal(rng):
    cells = Cells(rng, 5, 0.9)
    slopes = {}
    fine = Noise(rng, 32)

    def px(u, v):
        f1, f2, cell, near = cells(u, v)
        key = round(cell, 6)
        if key not in slopes:
            r = random.Random(key)
            slopes[key] = ((r.random() - 0.5) * 1.6, (r.random() - 0.5) * 1.6, 0.8 + r.random() * 0.4)
        sx, sy, tone = slopes[key]
        # A flat facet: height changes linearly across the cell.
        height = -(near[0] * sx + near[1] * sy) * 0.5 + fine(u, v) * 0.03
        edge = 1 - smoothstep(0.0, 0.06, f2 - f1)
        return height, tone + edge * 0.25, edge
    return px


def skin(rng):
    cells = Cells(rng, 12, 0.85)
    spots = Fbm(rng, [(3, 3, 0.6), (6, 6, 0.4)])
    pores = Noise(rng, 96)

    def px(u, v):
        f1, f2, cell, _ = cells(u, v)
        bump = 1 - smoothstep(0.0, 0.62, f1)
        crease = 1 - smoothstep(0.0, 0.08, f2 - f1)
        p = pores(u, v)
        height = bump * 0.6 - crease * 0.35 + p * 0.08
        albedo = 0.9 + bump * 0.16 - crease * 0.28 + (cell - 0.5) * 0.12
        return height, albedo, smoothstep(0.6, 0.66, spots(u, v))
    return px


def plaster(rng):
    trowel = Fbm(rng, [(3, 3, 0.45), (6, 6, 0.3), (12, 12, 0.15), (24, 24, 0.1)])
    sand = Noise(rng, 128)

    def px(u, v):
        t = trowel(u, v)
        swirl = smoothstep(0.46, 0.54, t)
        s = sand(u, v)
        height = t * 0.5 + swirl * 0.2 + s * 0.12
        albedo = 1 + (t - 0.5) * 0.1 + (s - 0.5) * 0.05
        return height, albedo, 0.0
    return px


def roof(rng):
    rows, across = 5, 6
    tones = [[0.84 + rng.random() * 0.3 for _ in range(across)] for _ in range(rows)]
    weather = Fbm(rng, [(4, 4, 0.5), (8, 8, 0.3), (16, 16, 0.2)])

    def px(u, v):
        r = int(math.floor(v * rows)) % rows
        ly = v * rows - math.floor(v * rows)  # 0 at the visible lower edge, 1 under the row above
        x = (u * across + (0.5 if r % 2 else 0.0)) % across
        c = int(x)
        lx = x - c
        rounded = math.sin(math.pi * lx) ** 0.6
        gap = 1 - smoothstep(0.015, 0.05, min(lx, 1 - lx))
        shadow = smoothstep(0.72, 1.0, ly)
        w = weather(u, v)
        height = (1 - ly) * 0.55 + rounded * 0.3 - gap * 0.3
        albedo = tones[r][c] * (1 + (w - 0.5) * 0.3) * (1 - gap * 0.4)
        return height, albedo, max(shadow, gap)
    return px


def planks(rng):
    boards = 6
    joints = []
    # One butt joint per board per tile: long boards, staggered like a real floor or siding.
    for b in range(boards):
        joints.append([(b * 0.37 + rng.random() * 0.2) % 1.0])
    board_tone = {}
    warp = Fbm(rng, [(2, 12, 0.6), (4, 24, 0.4)])
    fibers = Noise(rng, 8, 220)
    wear = Fbm(rng, [(3, 3, 0.6), (7, 7, 0.4)])

    def px(u, v):
        b = int(math.floor(v * boards)) % boards
        lv = v * boards - math.floor(v * boards)
        cuts = joints[b]
        seg = 0
        dist = 1.0
        for i, c in enumerate(cuts):
            d = abs(wrap(u - c))
            dist = min(dist, d)
            if u >= c:
                seg = i + 1
        key = (b, seg % max(1, len(cuts)))
        if key not in board_tone:
            board_tone[key] = (0.8 + rng.random() * 0.34, rng.random())
        tone, phase = board_tone[key]
        gap_side = min(lv, 1 - lv) / boards
        gap = 1 - smoothstep(0.003, 0.007, min(gap_side, dist * 0.6))
        g = math.sin(TAU * (lv * 3.5 + warp(u, v) * 1.4 + phase))
        band = (g * 0.5 + 0.5) ** 3
        f = fibers(u, v)
        # Two nails next to every joint.
        nail = 0.0
        for c in cuts:
            for ny in (0.28, 0.72):
                d = math.hypot(wrap(u - c - 0.012) * 1.0, (lv - ny) / boards)
                nail = max(nail, 1 - smoothstep(0.003, 0.0055, d))
        crown = math.sin(math.pi * lv) * 0.3
        height = crown - band * 0.18 + f * 0.12 - gap * 0.6 - nail * 0.1
        albedo = tone * (1.06 - band * 0.22 + (f - 0.5) * 0.1 + (wear(u, v) - 0.5) * 0.12) * (1 - nail * 0.55)
        return height, albedo, gap
    return px


def painted(rng):
    peel = Noise(rng, 48)
    chips = Fbm(rng, [(4, 4, 0.45), (8, 8, 0.3), (16, 16, 0.15), (32, 32, 0.1)])
    streak = Noise(rng, 3, 90)

    def px(u, v):
        c = chips(u, v)
        chip = smoothstep(0.78, 0.8, c)
        rim = smoothstep(0.75, 0.78, c) * (1 - chip)
        p = peel(u, v)
        height = (1 - chip) * 0.4 + rim * 0.12 + p * 0.06
        albedo = 1 + (p - 0.5) * 0.06 + (streak(u, v) - 0.5) * 0.05 - rim * 0.15
        return height, albedo, chip
    return px


def rubber(rng):
    rows = 8
    dust = Noise(rng, 64)

    def px(u, v):
        a = abs(((u + v) * rows) % 1.0 - 0.5)
        b = abs(((u - v) * rows) % 1.0 - 0.5)
        pyramid = 1 - max(a, b) * 2
        d = dust(u, v)
        height = pyramid * 0.8 + d * 0.1
        albedo = 0.88 + pyramid * 0.16 + (d - 0.5) * 0.06
        return height, albedo, 0.0
    return px


def wallpaper(rng):
    paper = Noise(rng, 96)
    fibre = Noise(rng, 4, 128)
    stripes = 5
    flowers = 4

    def flower(x, y):
        r = math.hypot(x, y)
        a = math.atan2(y, x)
        petals = 0.1 + 0.05 * math.cos(5 * a)
        return 1 - smoothstep(petals - 0.006, petals + 0.006, r)

    def px(u, v):
        s = (u * stripes) % 1.0
        line = 1 - smoothstep(0.012, 0.022, min(abs(s - 0.1), abs(s - 0.14)))
        fx, fy = u * flowers, v * flowers
        cy = math.floor(fy)
        fx += 0.5 * (cy % 2)
        lx, ly = fx - math.floor(fx) - 0.5, fy - cy - 0.5
        m = max(line, flower(lx * 0.9, ly * 0.9))
        p = paper(u, v)
        height = m * 0.35 + p * 0.1 + fibre(u, v) * 0.05
        albedo = 1 + (p - 0.5) * 0.05
        return height, albedo, m
    return px


def rug(rng):
    pile = Noise(rng, 128)
    tone = Fbm(rng, [(3, 3, 0.6), (6, 6, 0.4)])
    cells = 3

    def px(u, v):
        x, y = u * cells, v * cells
        lx, ly = x - math.floor(x) - 0.5, y - math.floor(y) - 0.5
        diamond = abs(lx) + abs(ly)
        ring = 1 - smoothstep(0.02, 0.04, abs(diamond - 0.34))
        inner = 1 - smoothstep(0.1, 0.12, diamond)
        cross = (1 - smoothstep(0.015, 0.03, min(abs(lx), abs(ly)))) * (1 - smoothstep(0.15, 0.2, diamond)) * (diamond > 0.1)
        corner = 1 - smoothstep(0.07, 0.09, min(abs(abs(lx) - 0.5) + abs(abs(ly) - 0.5), 1))
        tri = abs((lx + 0.5) * 8 % 1.0 - 0.5) * 2
        zig = 1 - smoothstep(0.012, 0.024, abs(abs(ly) - (0.42 + tri * 0.05)))
        m = max(ring, inner, cross, corner, zig)
        p = pile(u, v)
        height = p * 0.6 + m * 0.15
        albedo = 0.9 + (p - 0.5) * 0.18 + (tone(u, v) - 0.5) * 0.12
        return height, albedo, m
    return px


def leather(rng):
    cells = Cells(rng, 28, 0.9)
    creases = Fbm(rng, [(3, 3, 0.5), (6, 6, 0.3), (12, 12, 0.2)])
    tone = Fbm(rng, [(2, 2, 0.6), (5, 5, 0.4)])

    def px(u, v):
        f1, f2, _, _ = cells(u, v)
        pebble = 1 - smoothstep(0.0, 0.55, f1)
        c = creases(u, v)
        crease = 1 - smoothstep(0.0, 0.025, abs(c - 0.5))
        height = pebble * 0.35 - crease * 0.5 + c * 0.2
        albedo = 0.92 + pebble * 0.08 - crease * 0.22 + (tone(u, v) - 0.5) * 0.25
        return height, albedo, 0.0
    return px


def bark(rng):
    ridges = Fbm(rng, [(2, 7, 0.5), (3, 14, 0.3), (6, 28, 0.2)])
    plates = Noise(rng, 4, 10)
    moss = Fbm(rng, [(3, 3, 0.6), (6, 6, 0.4)])

    def px(u, v):
        # Deep furrows along the trunk (u), broken now and then by cross splits.
        r = ridges(u, v)
        ridge = abs((r * 7) % 1.0 - 0.5) * 2
        crest = ridge ** 0.7
        split = 1 - smoothstep(0.0, 0.025, abs(plates(u, v) - 0.5))
        height = crest * 0.9 - split * 0.35
        albedo = 0.62 + crest * 0.5 - split * 0.2 + (moss(u, v) - 0.5) * 0.2
        return height, albedo, 0.0
    return px


def brick(rng):
    rows, across = 8, 4
    tones = [[0.8 + rng.random() * 0.35 for _ in range(across)] for _ in range(rows)]
    grain = Fbm(rng, [(16, 16, 0.5), (32, 32, 0.3), (64, 64, 0.2)])
    chips = Noise(rng, 32)

    def px(u, v):
        r = int(math.floor(v * rows)) % rows
        ly = v * rows - math.floor(v * rows)
        x = (u * across + (0.5 if r % 2 else 0.0)) % across
        c = int(x)
        lx = x - c
        edge = min(min(lx, 1 - lx) / across, min(ly, 1 - ly) / rows) + (chips(u, v) - 0.5) * 0.006
        mortar = 1 - smoothstep(0.005, 0.01, edge)
        g = grain(u, v)
        height = smoothstep(0.0, 0.03, edge) * 0.5 + g * 0.3 - mortar * 0.2
        albedo = tones[r][c] * (1 + (g - 0.5) * 0.35)
        return height, albedo, mortar
    return px


def grass(rng):
    # Seen from above: many short blades pointing every way, tips lighter, gaps between the tufts darker.
    blades = []
    for _ in range(2600):
        x, y, a = rng.random(), rng.random(), rng.random() * math.pi
        blades.append((x, y, math.cos(a), math.sin(a), .016 + rng.random() * .026, rng.random()))
    grid = {}
    for b in blades:
        grid.setdefault((int(b[0] * 16), int(b[1] * 16)), []).append(b)
    clumps = Fbm(rng, [(4, 4, .6), (8, 8, .4)])

    def px(u, v):
        h, tip = 0.0, 0.0
        cx, cy = int(u * 16), int(v * 16)
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for x, y, c, sn, length, shade in grid.get(((cx + dx) % 16, (cy + dy) % 16), ()):
                    ex, ey = wrap(u - x), wrap(v - y)
                    along = ex * c + ey * sn
                    if abs(along) > length:
                        continue
                    across = abs(-ex * sn + ey * c)
                    w = (1 - smoothstep(0.0015, 0.0045, across)) * (1 - abs(along) / length * .6)
                    if w > h:
                        h, tip = w, shade * (along / length * .5 + .5)
        c = clumps(u, v)
        height = h * .75 + c * .25
        albedo = .62 + h * .45 + tip * .15 + (c - .5) * .3
        return height, albedo, tip * h
    return px


def leaves(rng):
    cells = Cells(rng, 9, .9)
    veins = Noise(rng, 48)

    def px(u, v):
        f1, f2, cell, near = cells(u, v)
        # Each cell is a leaf: an ellipse bulging in the middle, with a central vein and dark gaps around it.
        r = random.Random(round(cell, 6))
        a = r.random() * math.pi
        x, y = near
        lx, ly = x * math.cos(a) + y * math.sin(a), -x * math.sin(a) + y * math.cos(a)
        ellipse = math.hypot(lx / .55, ly / .32)
        body = 1 - smoothstep(.75, 1.0, ellipse)
        vein = (1 - smoothstep(0.0, .03, abs(ly))) * body
        gap = 1 - smoothstep(0.0, .08, f2 - f1)
        height = body * (1 - ellipse * .4) - gap * .4 - vein * .15 + veins(u, v) * .05
        albedo = .7 + body * .35 + (r.random() - .5) * .3 - gap * .4 + vein * .15
        return height, albedo, body * r.random()
    return px


def soil(rng):
    clods = Cells(rng, 14, .9)
    grains = Noise(rng, 128)
    damp = Fbm(rng, [(3, 3, .6), (6, 6, .4)])
    pebbles = Cells(rng, 22, .8)

    def px(u, v):
        f1, f2, cell, _ = clods(u, v)
        clod = 1 - smoothstep(0.0, .7, f1)
        crack = 1 - smoothstep(0.0, .06, f2 - f1)
        p1, _, pc, _ = pebbles(u, v)
        pebble = (1 - smoothstep(.12, .2, p1)) * (1 if pc > .7 else 0)
        g = grains(u, v)
        height = clod * .5 - crack * .4 + pebble * .6 + g * .12
        albedo = .8 + clod * .15 - crack * .3 + (damp(u, v) - .5) * .35 + (g - .5) * .12 + pebble * .35
        return height, albedo, pebble
    return px


def fur(rng):
    strands = Noise(rng, 6, 180)
    tufts = Fbm(rng, [(4, 8, .6), (8, 16, .4)])

    def px(u, v):
        s1 = strands(u, v)
        t = tufts(u, v)
        height = s1 * .7 + t * .3
        albedo = .82 + (s1 - .5) * .35 + (t - .5) * .2
        return height, albedo, 0.0
    return px


# Order matters: layer = _Surface - 1, as numbered in Shapes.Surface and NubikLit.shader.
MATERIALS = [
    ('wood', wood, 7), ('masonry', masonry, 9), ('metal', metal, 5), ('cloth', cloth, 5),
    ('crystal', crystal, 10), ('skin', skin, 7), ('plaster', plaster, 4), ('roof', roof, 8),
    ('planks', planks, 8), ('painted', painted, 6), ('rubber', rubber, 6), ('wallpaper', wallpaper, 3),
    ('rug', rug, 5), ('leather', leather, 5), ('bark', bark, 8), ('brick', brick, 8),
    ('grass', grass, 6), ('leaves', leaves, 7), ('soil', soil, 7), ('fur', fur, 4),
]


def encode(height, albedo, mask, strength):
    low = min(min(r) for r in height)
    high = max(max(r) for r in height)
    span = max(1e-6, high - low)
    pixels = []
    for py in range(SIZE):
        row = []
        for px in range(SIZE):
            left, right = height[py][(px - 1) % SIZE], height[py][(px + 1) % SIZE]
            up, down = height[(py - 1) % SIZE][px], height[(py + 1) % SIZE][px]
            # Image rows run down while texture v runs up: the v slope is (up - down).
            du, dv = (right - left) / span, (up - down) / span
            nx, ny, nz = -du * strength, -dv * strength, 1.0
            length = math.sqrt(nx * nx + ny * ny + nz * nz)
            nx, ny = nx / length, ny / length
            a = clamp(albedo[py][px] * 0.5)
            row.append((int(round((nx * 0.5 + 0.5) * 255)), int(round((ny * 0.5 + 0.5) * 255)), int(round(a * 255)), int(round(clamp(mask[py][px]) * 255))))
        pixels.append(row)
    return pixels


def main():
    atlas = [[(128, 128, 128, 0)] * (SIZE * COLS) for _ in range(SIZE * ROWS)]
    for index, (name, make, strength) in enumerate(MATERIALS):
        rng = random.Random(29092026 + index * 101)
        pixels = encode(*layer(make(rng)), strength)
        ox, oy = index % COLS * SIZE, index // COLS * SIZE
        for y in range(SIZE):
            atlas[oy + y][ox:ox + SIZE] = pixels[y]
        print('layer', index, name)

    rows = []
    for row in atlas:
        rows.append(b'\x00' + bytes(c for pixel in row for c in pixel))

    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)

    width, height = SIZE * COLS, SIZE * ROWS
    png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0))
    png += chunk(b'IDAT', zlib.compress(b''.join(rows), 9)) + chunk(b'IEND', b'')
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, 'wb') as file:
        file.write(png)
    print('wrote', os.path.normpath(OUT), len(png), 'bytes')


if __name__ == '__main__':
    main()

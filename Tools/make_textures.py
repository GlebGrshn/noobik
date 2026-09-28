"""Generates the tileable rock detail texture into Assets/Game/Textures/RockDetail.png.

Channels: R/G - tangent-space normal (x, y), B - height, A - crack mask. Standard library only:
    py -3 Tools/make_textures.py
"""
import math
import os
import random
import struct
import zlib

SIZE = 256
OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Game', 'Textures', 'RockDetail.png')


def lattice(period, rng):
    return [[rng.random() for _ in range(period)] for _ in range(period)]


def smooth(t):
    return t * t * (3 - 2 * t)


def value(grid, period, u, v):
    """Periodic value noise, u and v in [0, 1)."""
    x, y = u * period, v * period
    ix, iy = int(math.floor(x)), int(math.floor(y))
    fx, fy = smooth(x - ix), smooth(y - iy)
    x0, y0, x1, y1 = ix % period, iy % period, (ix + 1) % period, (iy + 1) % period
    a = grid[y0][x0] + (grid[y0][x1] - grid[y0][x0]) * fx
    b = grid[y1][x0] + (grid[y1][x1] - grid[y1][x0]) * fx
    return a + (b - a) * fy


def main():
    rng = random.Random(29092026)
    octaves = [(4, 0.34), (8, 0.26), (16, 0.2), (32, 0.12), (64, 0.08)]
    grids = [(period, weight, lattice(period, rng)) for period, weight in octaves]
    # Rock plates: jittered cells; cracks run where the two nearest centres are almost equally close.
    cells = 4
    # Cracks come and go: only parts of the plate edges fracture.
    fade = lattice(4, rng)
    points = [[(rng.random() * 0.8 + 0.1, rng.random() * 0.8 + 0.1, rng.random()) for _ in range(cells)] for _ in range(cells)]

    height = [[0.0] * SIZE for _ in range(SIZE)]
    crack = [[0.0] * SIZE for _ in range(SIZE)]
    for py in range(SIZE):
        v = py / SIZE
        for px in range(SIZE):
            u = px / SIZE
            h = sum(weight * value(grid, period, u, v) for period, weight, grid in grids)
            cx, cy = u * cells, v * cells
            ix, iy = int(cx), int(cy)
            first, second, plate = 9.0, 9.0, 0.0
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    gx, gy = ix + dx, iy + dy
                    ox, oy, lift = points[gy % cells][gx % cells]
                    d = math.hypot(gx + ox - cx, gy + oy - cy)
                    if d < first:
                        second, first, plate = first, d, lift
                    elif d < second:
                        second = d
            edge = second - first
            line = 1 - min(1.0, edge / 0.055)
            line = line * line * (3 - 2 * line)
            f = value(fade, 4, u, v)
            line *= smooth(max(0.0, min(1.0, (f - 0.52) / 0.18)))
            # Plates sit at slightly different heights and sink towards the cracks.
            height[py][px] = h * 0.9 + plate * 0.05 - line * 0.1
            crack[py][px] = line

    low = min(min(row) for row in height)
    high = max(max(row) for row in height)
    rows = []
    for py in range(SIZE):
        row = bytearray([0])  # PNG filter: none
        for px in range(SIZE):
            left = height[py][(px - 1) % SIZE]
            right = height[py][(px + 1) % SIZE]
            up = height[(py - 1) % SIZE][px]
            down = height[(py + 1) % SIZE][px]
            nx, ny, nz = (left - right) * 9, (up - down) * 9, 1.0
            length = math.sqrt(nx * nx + ny * ny + nz * nz)
            nx, ny = nx / length, ny / length
            h = (height[py][px] - low) / (high - low)
            row += bytes((int((nx * 0.5 + 0.5) * 255), int((ny * 0.5 + 0.5) * 255), int(h * 255), int(crack[py][px] * 255)))
        rows.append(bytes(row))

    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)

    png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', SIZE, SIZE, 8, 6, 0, 0, 0))
    png += chunk(b'IDAT', zlib.compress(b''.join(rows), 9)) + chunk(b'IEND', b'')
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, 'wb') as file:
        file.write(png)
    print('wrote', os.path.normpath(OUT), len(png), 'bytes')


if __name__ == '__main__':
    main()

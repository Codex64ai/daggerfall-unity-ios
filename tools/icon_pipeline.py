#!/usr/bin/env python3
"""Condition a generated image into a touch-HUD icon that matches the shipped set.

Why this exists: hand-authored icons and AI output both miss the house style in the same
three measurable ways. Measured against the shipped btn_*.png:

    native grid   95% of 4x4 blocks are uniform  -> the art is 56x56 upscaled 4x, nearest
    edges         ZERO semi-transparent pixels   -> hard pixel-art edges, no anti-aliasing
    palette       51-64 quantised colours        -> real shading, not flat fills

Anything soft-edged, smoothly-shaded or high-resolution reads as "not Daggerfall" no
matter how good the drawing is. This script forces all three properties, so the only thing
the generator has to get right is the DRAWING.

Usage:
    python3 icon_pipeline.py in.png out.png [--palette-from DIR] [--coverage 0.64]

Accepts RGBA (transparent already) or RGB (background removed by corner flood fill, so a
plain white/grey/green backdrop is fine).
"""
import argparse, glob, os, struct, sys, zlib
from collections import Counter

NATIVE = 56          # measured native grid
SCALE = 4            # 56 * 4 = 224
OUT = NATIVE * SCALE


# ---------------------------------------------------------------- PNG io

def _unfilter(raw, w, h, bpp):
    stride = w * bpp
    rows, prev, i = [], bytearray(stride), 0
    for _ in range(h):
        f = raw[i]; i += 1
        line = bytearray(raw[i:i + stride]); i += stride
        if f == 1:
            for x in range(bpp, stride):
                line[x] = (line[x] + line[x - bpp]) & 255
        elif f == 2:
            for x in range(stride):
                line[x] = (line[x] + prev[x]) & 255
        elif f == 3:
            for x in range(stride):
                a = line[x - bpp] if x >= bpp else 0
                line[x] = (line[x] + ((a + prev[x]) >> 1)) & 255
        elif f == 4:
            for x in range(stride):
                a = line[x - bpp] if x >= bpp else 0
                b = prev[x]; c = prev[x - bpp] if x >= bpp else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[x] = (line[x] + pr) & 255
        rows.append(bytes(line)); prev = line
    return rows


def load(path):
    d = open(path, 'rb').read()
    pos, idat = 8, b''
    w = h = ct = None
    while pos < len(d):
        ln = struct.unpack('>I', d[pos:pos + 4])[0]
        typ = d[pos + 4:pos + 8]; body = d[pos + 8:pos + 8 + ln]
        if typ == b'IHDR':
            w, h, bd, ct = struct.unpack('>IIBB', body[:10])
            if bd != 8:
                sys.exit("only 8-bit PNGs supported (got %d-bit)" % bd)
        elif typ == b'IDAT':
            idat += body
        pos += 12 + ln
    if ct not in (2, 6):
        sys.exit("need RGB or RGBA PNG (colour type %s)" % ct)
    bpp = 4 if ct == 6 else 3
    rows = _unfilter(zlib.decompress(idat), w, h, bpp)
    px = [[None] * w for _ in range(h)]
    for y in range(h):
        r = rows[y]
        for x in range(w):
            c = r[x * bpp:(x + 1) * bpp]
            px[y][x] = (c[0], c[1], c[2], c[3] if bpp == 4 else 255)
    return w, h, px


def save(path, w, h, px):
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        for x in range(w):
            raw += bytes(px[y][x])

    def chunk(t, d):
        return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)

    png = (b'\x89PNG\r\n\x1a\n'
           + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
           + chunk(b'IDAT', zlib.compress(bytes(raw), 9))
           + chunk(b'IEND', b''))
    open(path, 'wb').write(png)
    return len(png)


# ---------------------------------------------------------------- steps

def strip_background(w, h, px, tol=42):
    """Flood fill inward from the corners. Handles a plain backdrop of any colour."""
    if any(px[y][x][3] < 250 for y in range(h) for x in range(w)):
        return px                                    # already has alpha, trust it

    seeds = [(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)]
    keys = [px[y][x][:3] for x, y in seeds]
    stack = list(seeds)
    seen = [[False] * w for _ in range(h)]

    def near(c):
        return any(abs(c[0] - k[0]) + abs(c[1] - k[1]) + abs(c[2] - k[2]) <= tol for k in keys)

    while stack:
        x, y = stack.pop()
        if x < 0 or y < 0 or x >= w or y >= h or seen[y][x]:
            continue
        seen[y][x] = True
        if not near(px[y][x][:3]):
            continue
        px[y][x] = (0, 0, 0, 0)
        stack += [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
    return px


def fit_square(w, h, px, coverage):
    """Trim to the glyph, then centre it on a square canvas at the target coverage."""
    xs = [x for y in range(h) for x in range(w) if px[y][x][3] > 8]
    ys = [y for y in range(h) for x in range(w) if px[y][x][3] > 8]
    if not xs:
        sys.exit("image is empty after background removal")
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    gw, gh = x1 - x0 + 1, y1 - y0 + 1
    side = int(max(gw, gh) / coverage)
    out = [[(0, 0, 0, 0)] * side for _ in range(side)]
    ox, oy = (side - gw) // 2, (side - gh) // 2
    for y in range(gh):
        for x in range(gw):
            out[oy + y][ox + x] = px[y0 + y][x0 + x]
    return side, out


def downsample(side, px, n):
    """Area-average down to the native grid, then harden alpha - no soft edges."""
    out = [[(0, 0, 0, 0)] * n for _ in range(n)]
    for j in range(n):
        for i in range(n):
            x0, x1 = i * side // n, max(i * side // n + 1, (i + 1) * side // n)
            y0, y1 = j * side // n, max(j * side // n + 1, (j + 1) * side // n)
            r = g = b = a = cnt = 0
            for y in range(y0, y1):
                for x in range(x0, x1):
                    p = px[y][x]
                    if p[3] > 8:
                        r += p[0]; g += p[1]; b += p[2]; a += p[3]; cnt += 1
            total = (y1 - y0) * (x1 - x0)
            if cnt and cnt * 2 >= total:                 # majority opaque -> solid pixel
                out[j][i] = (r // cnt, g // cnt, b // cnt, 255)
    return out


def house_palette(dirs):
    """Sample the shipped icons. Excludes the generated ones so it stays authentic."""
    skip = ('btn_sheet', 'btn_automap', 'btn_mode_', 'joystick_knob', 'cursor_', 'reticle')
    cnt = Counter()
    for d in dirs:
        for f in sorted(glob.glob(os.path.join(d, '*.png'))):
            if any(s in os.path.basename(f) for s in skip):
                continue
            w, h, px = load(f)
            for y in range(h):
                for x in range(w):
                    p = px[y][x]
                    if p[3] > 200:
                        cnt[(p[0] // 4 * 4, p[1] // 4 * 4, p[2] // 4 * 4)] += 1
    pal = [c for c, _ in cnt.most_common(64)]
    if not pal:
        sys.exit("no palette source icons found")
    return pal


def quantise(n, px, pal):
    for j in range(n):
        for i in range(n):
            p = px[j][i]
            if p[3] == 0:
                continue
            best = min(pal, key=lambda c: (c[0] - p[0]) ** 2 + (c[1] - p[1]) ** 2 + (c[2] - p[2]) ** 2)
            px[j][i] = (best[0], best[1], best[2], 255)
    return px


def upscale(n, px, s):
    out = [[(0, 0, 0, 0)] * (n * s) for _ in range(n * s)]
    for j in range(n):
        for i in range(n):
            p = px[j][i]
            for dy in range(s):
                for dx in range(s):
                    out[j * s + dy][i * s + dx] = p
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('src'); ap.add_argument('dst')
    ap.add_argument('--palette-from', action='append', default=[])
    ap.add_argument('--coverage', type=float, default=0.64,
                    help='fraction of the frame the glyph fills (shipped icons: ~0.64)')
    a = ap.parse_args()

    pal_dirs = a.palette_from or [os.path.dirname(os.path.abspath(a.dst))]
    pal = house_palette(pal_dirs)

    w, h, px = load(a.src)
    px = strip_background(w, h, px)
    side, px = fit_square(w, h, px, a.coverage)
    small = downsample(side, px, NATIVE)
    small = quantise(NATIVE, small, pal)
    big = upscale(NATIVE, small, SCALE)
    size = save(a.dst, OUT, OUT, big)

    opaque = sum(1 for r in big for p in r if p[3] > 8)
    print("%s -> %s  %dx%d  %.0f%% opaque  %d colours  %d bytes" % (
        os.path.basename(a.src), os.path.basename(a.dst), OUT, OUT,
        100.0 * opaque / (OUT * OUT),
        len({p[:3] for r in big for p in r if p[3] > 8}), size))


if __name__ == '__main__':
    main()

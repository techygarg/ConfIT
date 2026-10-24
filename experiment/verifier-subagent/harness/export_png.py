#!/usr/bin/env python3
"""Render results/figures/*.svg to PNG, for documents that need a bitmap (macOS).

    export_png.py EXPERIMENT_DIR [WIDTH]      (WIDTH in pixels, default 1600)

QuickLook (qlmanage) draws each SVG at the top of a square canvas, scaled to
WIDTH pixels wide, but clips whatever lies beyond about 768 px of the SVG's own
width. So each figure is rendered from a copy whose width attribute is
RENDER_WIDTH (the viewBox is unchanged, so the vector drawing is the same), and
this script then keeps only the rows the figure occupies.
PNG scanline filters refer only to rows above them, so the first N rows of the
image data form a valid image on their own, and no imaging library is needed.
Colour-profile and resolution chunks are kept. Writes results/figures/png/.
"""
import os
import re
import struct
import subprocess
import sys
import tempfile
import zlib

SIGNATURE = b"\x89PNG\r\n\x1a\n"
KEEP_BEFORE_DATA = (b"iCCP", b"sRGB", b"gAMA", b"cHRM", b"pHYs")
CHANNELS = {0: 1, 2: 3, 4: 2, 6: 4}
RENDER_WIDTH = 640


def read_chunks(data):
    pos = len(SIGNATURE)
    while pos < len(data):
        size, = struct.unpack(">I", data[pos:pos + 4])
        yield data[pos + 4:pos + 8], data[pos + 8:pos + 8 + size]
        pos += 12 + size


def chunk(kind, body):
    return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body) & 0xFFFFFFFF)


def keep_top_rows(src, dst, rows):
    chunks = list(read_chunks(open(src, "rb").read()))
    header = dict(chunks)[b"IHDR"]
    width, height, depth, colour, compression, filtering, interlace = struct.unpack(">IIBBBBB", header)
    if depth != 8 or interlace != 0 or colour not in CHANNELS:
        sys.exit(f"export_png: unexpected PNG layout in {src}")
    pixels = zlib.decompress(b"".join(body for kind, body in chunks if kind == b"IDAT"))
    rows = min(rows, height)
    stride = 1 + width * CHANNELS[colour]
    out = [SIGNATURE, chunk(b"IHDR", struct.pack(">IIBBBBB", width, rows, depth, colour, compression, filtering, interlace))]
    out += [chunk(kind, body) for kind, body in chunks if kind in KEEP_BEFORE_DATA]
    out += [chunk(b"IDAT", zlib.compress(pixels[:rows * stride], 9)), chunk(b"IEND", b"")]
    open(dst, "wb").write(b"".join(out))
    return width, rows


def main():
    exp = sys.argv[1]
    target = int(sys.argv[2]) if len(sys.argv) > 2 else 1600
    figures = os.path.join(exp, "results", "figures")
    out_dir = os.path.join(figures, "png")
    os.makedirs(out_dir, exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        for name in sorted(f for f in os.listdir(figures) if f.endswith(".svg")):
            text = open(os.path.join(figures, name)).read()
            size = re.search(r'(<svg [^>]*)width="([0-9.]+)" height="([0-9.]+)"', text)
            svg_w, svg_h = float(size.group(2)), float(size.group(3))
            svg = os.path.join(tmp, name)
            open(svg, "w").write(text[:size.start()] + f'{size.group(1)}width="{RENDER_WIDTH}" '
                                 f'height="{RENDER_WIDTH * svg_h / svg_w:.1f}"' + text[size.end():])
            subprocess.run(["qlmanage", "-t", "-s", str(target), "-o", tmp, svg], check=True, capture_output=True)
            w, h = keep_top_rows(os.path.join(tmp, name + ".png"), os.path.join(out_dir, name[:-4] + ".png"),
                                 round(svg_h * target / svg_w))
            print(f"{name[:-4]}.png  {w} x {h}")


if __name__ == "__main__":
    main()

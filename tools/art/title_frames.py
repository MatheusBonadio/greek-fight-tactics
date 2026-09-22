import argparse
import os
import sys

from images import OPAQUE, load_rgba
from palette import ROOT

DEFAULT_OUT = os.path.join(ROOT, "art", "placeholders", "ui")
TITLE = "bg_title"
FRAMES = 8
STILL_FRAME = 0


def opaque(*colours):
    return tuple(colour + (OPAQUE,) for colour in colours)


SEA = opaque((0x68, 0x70, 0x88), (0x78, 0x78, 0x80))
GLINT = opaque((0xF8, 0xF8, 0xF0), (0xF8, 0xD0, 0xA8), (0xD8, 0xD8, 0xC8), (0xA8, 0xA8, 0xA0), (0xD8, 0xA0, 0x78))
SKY = opaque((0x78, 0x18, 0x20), (0xC0, 0x38, 0x28), (0xF8, 0x78, 0x58), (0xF8, 0xA8, 0x70), (0xF8, 0xC0, 0x70))
CLOUD = opaque((0xF8, 0xD0, 0xA8), (0xF8, 0xF8, 0xF0), (0xD8, 0xA0, 0x78))
GLOW = opaque((0xF8, 0xD0, 0xA8))[0]
HALO = opaque((0xF8, 0xC0, 0x70), (0xF8, 0xA8, 0x70))
SUNLIT = {
    opaque((0xA8, 0x48, 0x30))[0]: opaque((0xC0, 0x38, 0x28))[0],
    opaque((0xC0, 0x38, 0x28))[0]: opaque((0xF8, 0x78, 0x58))[0],
    opaque((0xD8, 0x78, 0x48))[0]: opaque((0xF8, 0xA8, 0x70))[0],
    opaque((0x60, 0x40, 0x30))[0]: opaque((0xA8, 0x70, 0x48))[0],
    opaque((0xA8, 0x70, 0x48))[0]: opaque((0xD8, 0xA0, 0x78))[0],
    opaque((0x78, 0x78, 0x80))[0]: opaque((0xA8, 0xA8, 0xA0))[0],
}
FOLIAGE = opaque((0x08, 0x08, 0x10), (0x38, 0x28, 0x28))
CYPRESS = (0, 60, 48, 216)
HEDGES = (0, 166, 384, 216)
OPEN_WATER = 0.15
SMALLEST_WAVE = 8
SMALLEST_CLOUD = 10
LEAF_CLUMP = 3
STRAIGHT_EDGE = 4
RIPPLES = (
    (0, 1, 1, 0, -1, -1, 0, 0),
    (0, -1, -1, 0, 1, 1, 0, 0),
    (0, 0, 1, 1, 0, -1, -1, 0),
    (0, 0, 0, 1, 1, 0, -1, -1),
)
BREATH = (0, 1, 2, 2, 1, 0, -1, 0)
BILLOW = (0, 0, 1, 1, 0, -1, -1, 0)
FLARE = (0, 0, 1, 1, 0, 0, 0, 0)
SWAY = (0, 1, 1, 0, -1, -1, 0, 1)
NEIGHBOURS = ((1, 0), (-1, 0), (0, 1), (0, -1))


def source_path(folder):
    return os.path.join(folder, f"{TITLE}.png")


def file_name(frame):
    return f"{TITLE}.png" if frame == STILL_FRAME else f"{TITLE}_{frame}.png"


def open_water_rows(px, width, height):
    wetness = {y: sum(1 for x in range(width) if px[x, y] in SEA) / width for y in range(height)}
    deepest = max(wetness, key=wetness.get)
    rows = [deepest]
    for step in (-1, 1):
        y = deepest + step
        while 0 <= y < height and wetness[y] >= OPEN_WATER:
            rows.append(y)
            y += step
    return sorted(rows)


def runs_of(flags):
    runs = []
    start = None
    for index, lit in enumerate(list(flags) + [False]):
        if lit and start is None:
            start = index
        elif not lit and start is not None:
            runs.append((start, index - start))
            start = None
    return runs


def blobs_of(px, cells):
    seen = set()
    found = []
    for cell in cells:
        if cell in seen:
            continue

        seen.add(cell)
        stack = [cell]
        blob = set()
        while stack:
            x, y = stack.pop()
            blob.add((x, y))
            for dx, dy in NEIGHBOURS:
                neighbour = (x + dx, y + dy)
                if neighbour in cells and neighbour not in seen:
                    seen.add(neighbour)
                    stack.append(neighbour)

        found.append(blob)
    return found


def ripple(px, out, width, rows, frame):
    for y in rows:
        shift = RIPPLES[y % len(RIPPLES)][frame % FRAMES]
        if shift == 0:
            continue

        water = [px[x, y] in SEA or px[x, y] in GLINT for x in range(width)]
        for start, length in runs_of(water):
            if length < SMALLEST_WAVE:
                continue

            waves = [px[start + offset, y] for offset in range(length)]
            for offset in range(length):
                out[start + offset, y] = waves[(offset - shift) % length]


def sun_disc(px, width, sky):
    lit = {(x, y) for y in range(sky) for x in range(width) if px[x, y] == GLOW}
    blobs = blobs_of(px, lit)
    return max(blobs, key=len) if blobs else set()


def breathe(px, out, width, sky, disc, frame):
    step = BREATH[frame % FRAMES]
    if step == 0 or not disc:
        return

    if step < 0:
        for x, y in disc:
            ring = next((px[x + dx, y + dy] for dx, dy in NEIGHBOURS
                         if 0 <= x + dx < width and 0 <= y + dy < sky and px[x + dx, y + dy] in HALO), None)
            if ring:
                out[x, y] = ring
        return

    edge = disc
    grown = set(disc)
    for _ in range(step):
        ahead = set()
        for x, y in edge:
            for dx, dy in NEIGHBOURS:
                cell = (x + dx, y + dy)
                if cell in grown or not (0 <= cell[0] < width and 0 <= cell[1] < sky):
                    continue
                if px[cell] in HALO:
                    ahead.add(cell)
                    grown.add(cell)

        for cell in ahead:
            out[cell] = GLOW
        edge = ahead


def billow(px, out, width, sky, disc, frame):
    step = BILLOW[frame % FRAMES]
    if step == 0:
        return

    puffs = {(x, y) for y in range(sky) for x in range(width) if px[x, y] in CLOUD} - disc
    for blob in blobs_of(px, puffs):
        if len(blob) < SMALLEST_CLOUD:
            continue

        for x, y in blob:
            for dx, dy in NEIGHBOURS:
                cell = (x + dx, y + dy)
                if not (0 <= cell[0] < width and 0 <= cell[1] < sky) or px[cell] not in SKY:
                    continue

                if step > 0:
                    out[cell] = px[x, y]
                else:
                    out[x, y] = px[cell]


def flare(px, out, width, height, ground, frame):
    if FLARE[frame % FRAMES] == 0:
        return

    for y in range(ground, height):
        for x in range(width):
            brighter = SUNLIT.get(px[x, y])
            if brighter and any(0 <= x + dx < width and ground <= y + dy < height and px[x + dx, y + dy] == brighter
                                for dx, dy in NEIGHBOURS):
                out[x, y] = brighter


def is_jagged(edges, key):
    run = 1
    for step in (-1, 1):
        neighbour = key + step
        while edges.get(neighbour) == edges[key]:
            run += 1
            neighbour += step
    return run < STRAIGHT_EDGE


def leaf_edges(px, band, width, height, across):
    x0, y0, x1, y1 = band
    columns = range(max(0, x0), min(x1, width))
    rows = range(max(0, y0), min(y1, height))
    edges = {}
    if across:
        for y in rows:
            leaves = [x for x in columns if px[x, y] in FOLIAGE]
            if leaves and min(leaves) > x0:
                edges[y] = min(leaves)
    else:
        for x in columns:
            leaves = [y for y in rows if px[x, y] in FOLIAGE]
            if leaves and min(leaves) > y0:
                edges[x] = min(leaves)
    return edges


def sway(px, out, width, height, band, frame, across=False):
    step = SWAY[frame % FRAMES]
    if step == 0:
        return

    edges = leaf_edges(px, band, width, height, across)
    for key, edge in edges.items():
        if not is_jagged(edges, key):
            continue

        leaning = step if (key // LEAF_CLUMP) % 2 == 0 else -step
        outside = (edge - 1, key) if across else (key, edge - 1)
        tip = (edge, key) if across else (key, edge)
        if leaning > 0:
            out[outside] = px[tip]
        else:
            out[tip] = px[outside]


def draw(frame, source):
    image = source.copy()
    px = source.load()
    out = image.load()
    width, height = source.size
    rows = open_water_rows(px, width, height)
    sky = min(rows)
    disc = sun_disc(px, width, sky)
    ripple(px, out, width, rows, frame)
    breathe(px, out, width, sky, disc, frame)
    billow(px, out, width, sky, disc, frame)
    flare(px, out, width, height, max(rows) + 1, frame)
    sway(px, out, width, height, HEDGES, frame)
    sway(px, out, width, height, CYPRESS, frame, across=True)
    return image


def main(arguments=None):
    parser = argparse.ArgumentParser(description="Draw the sunset title screen breathing, billowing and glittering on the water.")
    parser.add_argument("--out", default=DEFAULT_OUT, help=f"folder holding {TITLE}.png and its frames")
    options = parser.parse_args(arguments)
    source = load_rgba(source_path(options.out))
    for frame in range(FRAMES):
        if frame == STILL_FRAME:
            continue

        path = os.path.join(options.out, file_name(frame))
        draw(frame, source).save(path)
        print(path)
    return 0


if __name__ == "__main__":
    sys.exit(main())

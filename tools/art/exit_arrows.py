import argparse
import os
import sys

from images import CLEAR, OPAQUE, from_pixels
from palette import ROOT, load_gpl
from tile_preview import CELL_HEIGHT, CELL_WIDTH, HALF_HEIGHT, HALF_WIDTH, in_footprint

DEFAULT_OUT = os.path.join(ROOT, "art", "placeholders", "tiles", "overlays")
HEADINGS = {"ne": (1, 0), "sw": (-1, 0), "nw": (0, 1), "se": (0, -1)}
PULSE = (0.0, 0.0625, 0.125, 0.0625)
TIP = -0.36
BASE = 0.14
BASE_HALF_WIDTH = 0.32
FILL = "gold_light"
SHADE = "gold_mid"
OUTLINE = "outline"
CENTER_X = HALF_WIDTH - 0.5
CENTER_Y = HALF_HEIGHT - 0.5


def to_grid(column, row):
    screen_x = (column - CENTER_X) / HALF_WIDTH
    screen_y = (row - CENTER_Y) / HALF_HEIGHT
    return (screen_x - screen_y) / 2, (-screen_x - screen_y) / 2


def inside_arrow(along, across):
    return TIP <= along <= BASE and abs(across) <= BASE_HALF_WIDTH * (along - TIP) / (BASE - TIP)


def draw(heading, frame, palette=None):
    palette = palette or load_gpl()
    colors = {name: color + (OPAQUE,) for name, color in palette.entries}
    toward_x, toward_y = HEADINGS[heading]
    shift = PULSE[frame]
    fill = set()
    across_of = {}
    for row in range(CELL_HEIGHT):
        for column in range(CELL_WIDTH):
            grid_x, grid_y = to_grid(column, row)
            along = -(grid_x * toward_x + grid_y * toward_y) + shift
            across = grid_x * toward_y - grid_y * toward_x
            if in_footprint(column, row) and inside_arrow(along, across):
                fill.add((column, row))
                across_of[(column, row)] = across

    pixels = [CLEAR] * (CELL_WIDTH * CELL_HEIGHT)
    for column, row in fill:
        pixels[row * CELL_WIDTH + column] = colors[SHADE if across_of[(column, row)] < 0 else FILL]
    for row in range(CELL_HEIGHT):
        for column in range(CELL_WIDTH):
            if (column, row) in fill or not in_footprint(column, row):
                continue
            if any((column + dx, row + dy) in fill for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                pixels[row * CELL_WIDTH + column] = colors[OUTLINE]
    return from_pixels((CELL_WIDTH, CELL_HEIGHT), pixels)


def file_name(heading, frame):
    return f"overlay_exit_{heading}.png" if frame == 0 else f"overlay_exit_{heading}_{frame}.png"


def main(arguments=None):
    parser = argparse.ArgumentParser(description="Draw the pulsing arrows that mark map exits.")
    parser.add_argument("--out", default=DEFAULT_OUT, help="folder for overlay_exit_<heading>[_<frame>].png")
    options = parser.parse_args(arguments)
    os.makedirs(options.out, exist_ok=True)
    palette = load_gpl()
    for heading in HEADINGS:
        for frame in range(len(PULSE)):
            path = os.path.join(options.out, file_name(heading, frame))
            draw(heading, frame, palette).save(path)
            print(path)
    return 0


if __name__ == "__main__":
    sys.exit(main())

import argparse
import os
import sys

from images import CLEAR, OPAQUE, from_pixels
from palette import ROOT, load_gpl
from tile_preview import CELL_HEIGHT, CELL_WIDTH

DEFAULT_OUT = os.path.join(ROOT, "art", "placeholders", "tiles", "overlays")
FILE_NAME = "overlay_shadow.png"
SHADOW_COLOR = "outline"
RADIUS_X = 9.0
RADIUS_Y = 4.5
DROP_ROWS = 0


def inside(column, row):
    x = (column - (CELL_WIDTH - 1) / 2) / RADIUS_X
    y = (row - (CELL_HEIGHT - 1) / 2 - DROP_ROWS) / RADIUS_Y
    return x * x + y * y <= 1.0


def draw(palette=None):
    palette = palette or load_gpl()
    color = dict(palette.entries)[SHADOW_COLOR] + (OPAQUE,)
    pixels = []
    for row in range(CELL_HEIGHT):
        for column in range(CELL_WIDTH):
            pixels.append(color if inside(column, row) else CLEAR)
    return from_pixels((CELL_WIDTH, CELL_HEIGHT), pixels)


def main(arguments=None):
    parser = argparse.ArgumentParser(description="Draw the solid ellipse that sits under a character.")
    parser.add_argument("--out", default=DEFAULT_OUT, help=f"folder for {FILE_NAME}")
    options = parser.parse_args(arguments)
    os.makedirs(options.out, exist_ok=True)
    path = os.path.join(options.out, FILE_NAME)
    draw().save(path)
    print(path)
    return 0


if __name__ == "__main__":
    sys.exit(main())
